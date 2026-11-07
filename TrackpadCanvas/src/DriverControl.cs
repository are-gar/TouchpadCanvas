using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace TrackpadCanvas
{
    // Talks to this repo's Magic Trackpad 2 drivers (AmtPtpDeviceUsbUm, AmtPtpHidFilter):
    //   USB:       UMDF driver named pipe "AmtPtpControlPanelUsbUmInterface"; 1-byte command -> [status][payload]
    //   Bluetooth: HID filter control device \\.\AmtPtpControlDeviceUm; IOCTLs 0x800 (reload) / 0x801 (battery)
    // The Bluetooth control device is admin-only. The USB pipe is admin-only unless the driver's
    // AllowNonAdminAccess setting is on.
    internal static class DriverControl
    {
        const string PipeName = "AmtPtpControlPanelUsbUmInterface";
        const string ControlDevice = @"\\.\AmtPtpControlDeviceUm";
        const byte CmdReloadSettings = 0x00, CmdGetBattery = 0x01, StatusOk = 0x00;
        const uint IOCTL_RELOAD_SETTINGS = (0x22u << 16) | (0x800u << 2); // CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800, METHOD_BUFFERED, FILE_ANY_ACCESS)
        const uint IOCTL_GET_BATTERY = (0x22u << 16) | (0x801u << 2);

        // True if either driver service is registered (the driver is installed, even if no trackpad is connected).
        public static bool IsInstalled()
        {
            return KeyExists(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\WUDF\Services\AmtPtpDeviceUsbUm")
                || KeyExists(@"SYSTEM\CurrentControlSet\Services\AmtPtpHidFilter");
        }

        // Returns the battery percent (1-100), or -1 if the driver isn't installed, isn't reachable or didn't answer.
        public static int TryReadBattery(out string source)
        {
            byte b;
            if (PipeCommand(CmdGetBattery, out b) && b > 0 && b <= 100) { source = "driver (USB)"; return b; }
            uint v;
            if (Ioctl(IOCTL_GET_BATTERY, true, out v) && v > 0 && v <= 100) { source = "driver (Bluetooth)"; return (int)v; }
            source = null;
            return -1;
        }

        // Tells the connected trackpad(s) to re-read their registry settings. True if any driver acknowledged.
        public static bool ReloadSettings()
        {
            byte unused;
            uint unused2;
            bool usb = PipeCommand(CmdReloadSettings, out unused);
            bool bt = Ioctl(IOCTL_RELOAD_SETTINGS, false, out unused2);
            return usb || bt;
        }

        static bool KeyExists(string path)
        {
            try { using (var k = Registry.LocalMachine.OpenSubKey(path)) return k != null; }
            catch (Exception) { return false; }
        }

        static bool PipeCommand(byte cmd, out byte payload)
        {
            payload = 0;
            try
            {
                using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                {
                    pipe.Connect(250);
                    pipe.WriteByte(cmd);
                    pipe.Flush();
                    var r = new byte[2];
                    int read = 0;
                    while (read < 2)
                    {
                        int n = pipe.Read(r, read, 2 - read);
                        if (n <= 0) return false;
                        read += n;
                    }
                    payload = r[1];
                    return r[0] == StatusOk;
                }
            }
            catch (Exception) { return false; }
        }

        static bool Ioctl(uint code, bool expectData, out uint value)
        {
            value = 0;
            IntPtr h = Native.CreateFile(ControlDevice, Native.GENERIC_READ | Native.GENERIC_WRITE,
                Native.FILE_SHARE_READ_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == Native.INVALID_HANDLE_VALUE) return false;
            try
            {
                uint returned;
                if (!DeviceIoControl(h, code, IntPtr.Zero, 0, out value, expectData ? 4u : 0u, out returned, IntPtr.Zero))
                    return false;
                return !expectData || returned >= 4;
            }
            finally { Native.CloseHandle(h); }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(IntPtr device, uint code, IntPtr inBuf, uint inSize,
            out uint outBuf, uint outSize, out uint returned, IntPtr overlapped);
    }
}

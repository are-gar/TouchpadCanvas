using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace TrackpadCanvas
{
    // Asks the MagicTrackpad2ForWindows driver for the battery level, using the same interfaces as its
    // control panel (see AmtPtpControlPanel.WinUI3/DeviceInterop.cs):
    //   USB:       UMDF driver named pipe "AmtPtpControlPanelUsbUmInterface", command 0x01 -> [status][percent]
    //   Bluetooth: HID filter control device \\.\AmtPtpControlDeviceUm, IOCTL 0x801 -> UInt32 percent
    // Used as a fallback when the HID Battery System collection can't be read (e.g. while wired over USB).
    internal static class DriverBattery
    {
        const string PipeName = "AmtPtpControlPanelUsbUmInterface";
        const byte CmdGetBattery = 0x01, StatusOk = 0x00;
        const uint IOCTL_GET_BATTERY = (0x22u << 16) | (0x801u << 2); // CTL_CODE(FILE_DEVICE_UNKNOWN, 0x801, METHOD_BUFFERED, FILE_ANY_ACCESS)

        // Returns the battery percent (1-100), or -1 if the driver isn't installed or didn't answer.
        public static int TryRead(out string source)
        {
            int p = FromPipe();
            if (p > 0) { source = "driver (USB)"; return p; }
            p = FromIoctl();
            if (p > 0) { source = "driver (Bluetooth)"; return p; }
            source = null;
            return -1;
        }

        static int FromPipe()
        {
            try
            {
                using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                {
                    pipe.Connect(250);
                    pipe.WriteByte(CmdGetBattery);
                    pipe.Flush();
                    var r = new byte[2];
                    int read = 0;
                    while (read < 2)
                    {
                        int n = pipe.Read(r, read, 2 - read);
                        if (n <= 0) return -1;
                        read += n;
                    }
                    return r[0] == StatusOk && r[1] <= 100 ? r[1] : -1;
                }
            }
            catch (Exception) { return -1; }
        }

        static int FromIoctl()
        {
            IntPtr h = Native.CreateFile(@"\\.\AmtPtpControlDeviceUm", Native.GENERIC_READ | Native.GENERIC_WRITE,
                Native.FILE_SHARE_READ_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == Native.INVALID_HANDLE_VALUE) return -1;
            try
            {
                uint value, returned;
                if (!DeviceIoControl(h, IOCTL_GET_BATTERY, IntPtr.Zero, 0, out value, 4, out returned, IntPtr.Zero)
                    || returned < 4 || value > 100) return -1;
                return (int)value;
            }
            finally { Native.CloseHandle(h); }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(IntPtr device, uint code, IntPtr inBuf, uint inSize,
            out uint outBuf, uint outSize, out uint returned, IntPtr overlapped);
    }
}

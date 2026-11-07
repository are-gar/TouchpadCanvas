using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace TrackpadCanvas
{
    internal sealed class BatteryStatus
    {
        public int Percent;
        public bool Charging;
        public bool FullyCharged;
        public string Flags = "";   // all active Battery System usages, for diagnostics
    }

    // Reads the Magic Trackpad's battery via its HID "Battery System" collection
    // (usage page 0x85: Absolute State Of Charge 0x65, Charging 0x44, Fully Charged 0x46).
    // On the USB-C Magic Trackpad this is report 0x90 on the "Apple Multi-touch Auxiliary Services" interface.
    internal static class BatteryReader
    {
        const ushort PAGE_BATTERY = 0x85;
        const ushort USAGE_CHARGE = 0x65, USAGE_CHARGING = 0x44, USAGE_FULL = 0x46;

        // Finds the HID path of an Apple device collection that reports battery charge, or null.
        public static string FindDevicePath(out byte reportId)
        {
            reportId = 0;
            uint n = 0;
            uint cb = (uint)Marshal.SizeOf(typeof(Native.RAWINPUTDEVICELIST));
            Native.GetRawInputDeviceList(null, ref n, cb);
            if (n == 0) return null;
            var list = new Native.RAWINPUTDEVICELIST[n];
            if (Native.GetRawInputDeviceList(list, ref n, cb) == unchecked((uint)-1)) return null;

            foreach (var d in list)
            {
                if (d.dwType != Native.RIM_TYPEHID) continue;
                string name = DeviceName(d.hDevice);
                string lower = name.ToLowerInvariant();
                if (!lower.Contains("vid&0001004c") && !lower.Contains("vid_05ac")) continue; // Apple (Bluetooth / USB)

                uint size = 0;
                Native.GetRawInputDeviceInfo(d.hDevice, Native.RIDI_PREPARSEDDATA, IntPtr.Zero, ref size);
                if (size == 0) continue;
                IntPtr pp = Marshal.AllocHGlobal((int)size);
                try
                {
                    if (Native.GetRawInputDeviceInfo(d.hDevice, Native.RIDI_PREPARSEDDATA, pp, ref size) == unchecked((uint)-1)) continue;
                    Native.HIDP_VALUE_CAPS cap;
                    if (FindChargeCap(pp, out cap))
                    {
                        reportId = cap.ReportID;
                        return name;
                    }
                }
                finally { Marshal.FreeHGlobal(pp); }
            }
            return null;
        }

        // Synchronously requests the battery report. May block briefly over Bluetooth: call off the UI thread.
        public static BatteryStatus Read(string path, byte reportId, out string error)
        {
            error = null;
            IntPtr h = Native.CreateFile(path, Native.GENERIC_READ | Native.GENERIC_WRITE, Native.FILE_SHARE_READ_WRITE,
                IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == Native.INVALID_HANDLE_VALUE)
            {
                error = "cannot open battery interface (error " + Marshal.GetLastWin32Error() + ")";
                return null;
            }
            IntPtr pp = IntPtr.Zero;
            try
            {
                if (!Native.HidD_GetPreparsedData(h, out pp)) { error = "no HID descriptor"; return null; }
                Native.HIDP_CAPS caps;
                if (Native.HidP_GetCaps(pp, out caps) != Native.HIDP_STATUS_SUCCESS || caps.InputReportByteLength == 0)
                {
                    error = "HidP_GetCaps failed";
                    return null;
                }
                var report = new byte[caps.InputReportByteLength];
                report[0] = reportId;
                if (!Native.HidD_GetInputReport(h, report, report.Length))
                {
                    error = "battery report request failed (error " + Marshal.GetLastWin32Error() + ")";
                    return null;
                }

                uint value;
                if (Native.HidP_GetUsageValue(Native.HidP_Input, PAGE_BATTERY, 0, USAGE_CHARGE, out value, pp,
                        report, (uint)report.Length) != Native.HIDP_STATUS_SUCCESS)
                {
                    error = "battery report has no charge value";
                    return null;
                }
                var status = new BatteryStatus { Percent = (int)Math.Min(100, value) };

                var usages = new ushort[16];
                uint count = (uint)usages.Length;
                if (Native.HidP_GetUsages(Native.HidP_Input, PAGE_BATTERY, 0, usages, ref count, pp,
                        report, (uint)report.Length) == Native.HIDP_STATUS_SUCCESS)
                {
                    var active = usages.Take((int)count).ToList();
                    status.Charging = active.Contains(USAGE_CHARGING);
                    // The trackpad sets 0x46 even when partly charged (seen at 77%), so only trust it at 100%.
                    status.FullyCharged = status.Percent >= 100 && active.Contains(USAGE_FULL);
                    status.Flags = string.Join(",", active.Select(u => "0x" + u.ToString("X2")));
                }
                return status;
            }
            finally
            {
                if (pp != IntPtr.Zero) Native.HidD_FreePreparsedData(pp);
                Native.CloseHandle(h);
            }
        }

        static bool FindChargeCap(IntPtr pp, out Native.HIDP_VALUE_CAPS found)
        {
            found = default(Native.HIDP_VALUE_CAPS);
            Native.HIDP_CAPS caps;
            if (Native.HidP_GetCaps(pp, out caps) != Native.HIDP_STATUS_SUCCESS) return false;
            ushort n = caps.NumberInputValueCaps;
            if (n == 0) return false;
            var vcaps = new Native.HIDP_VALUE_CAPS[n];
            if (Native.HidP_GetValueCaps(Native.HidP_Input, vcaps, ref n, pp) != Native.HIDP_STATUS_SUCCESS) return false;
            for (int i = 0; i < n; i++)
            {
                if (vcaps[i].UsagePage == PAGE_BATTERY && vcaps[i].IsRange == 0 && vcaps[i].Usage == USAGE_CHARGE)
                {
                    found = vcaps[i];
                    return true;
                }
            }
            return false;
        }

        static string DeviceName(IntPtr hDevice)
        {
            uint chars = 0;
            Native.GetRawInputDeviceInfo(hDevice, Native.RIDI_DEVICENAME, IntPtr.Zero, ref chars);
            if (chars == 0) return "";
            IntPtr buf = Marshal.AllocHGlobal((int)chars * 2 + 2);
            try
            {
                Native.GetRawInputDeviceInfo(hDevice, Native.RIDI_DEVICENAME, buf, ref chars);
                return Marshal.PtrToStringUni(buf) ?? "";
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
    }
}

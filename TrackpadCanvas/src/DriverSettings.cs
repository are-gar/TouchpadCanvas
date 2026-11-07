using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Principal;
using Microsoft.Win32;

namespace TrackpadCanvas
{
    // The Magic Trackpad 2 driver options, stored under HKLM in each driver's Parameters key
    // (same layout and defaults as the former AmtPtpControlPanel). Writing them requires admin.
    internal sealed class DriverSettings
    {
        public int ButtonDisabled;
        public int FeedbackClick = 0x060617;
        public int FeedbackRelease = 0x000014;
        public int StopPressure = -1;
        public int StopSize = -1;
        public int IgnoreButtonFinger;
        public int IgnoreNearFingers = 1;
        public int PalmRejection = 1;
        public int InvertMovement;
        public int AllowNonAdminAccess;

        // Haptic presets: click strength 0 = light, 1 = medium, 2 = firm. The high two bytes are the
        // vibration; clearing them keeps the click registered but silent.
        public static readonly int[] ClickPresets = { 0x040415, 0x060617, 0x08081e };
        public static readonly int[] ReleasePresets = { 0x000010, 0x000014, 0x020218 };
        public const int MaxFeedback = 0xffffff;

        const string UmdfParameters = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\WUDF\Services\AmtPtpDeviceUsbUm\Parameters";
        static readonly string[][] Targets =
        {
            new[] { @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\WUDF\Services", "AmtPtpDeviceUsbUm" },
            new[] { @"SYSTEM\CurrentControlSet\Services", "AmtPtpHidFilter" },
        };

        static readonly string[] Names =
        {
            "ButtonDisabled", "FeedbackClick", "FeedbackRelease", "StopPressure", "StopSize",
            "IgnoreButtonFinger", "IgnoreNearFingers", "PalmRejection", "InvertMovement", "AllowNonAdminAccess",
        };

        int Get(string name)
        {
            switch (name)
            {
                case "ButtonDisabled": return ButtonDisabled;
                case "FeedbackClick": return FeedbackClick;
                case "FeedbackRelease": return FeedbackRelease;
                case "StopPressure": return StopPressure;
                case "StopSize": return StopSize;
                case "IgnoreButtonFinger": return IgnoreButtonFinger;
                case "IgnoreNearFingers": return IgnoreNearFingers;
                case "PalmRejection": return PalmRejection;
                case "InvertMovement": return InvertMovement;
                case "AllowNonAdminAccess": return AllowNonAdminAccess;
            }
            throw new ArgumentException(name);
        }

        void Set(string name, int v)
        {
            switch (name)
            {
                case "ButtonDisabled": ButtonDisabled = v; break;
                case "FeedbackClick": FeedbackClick = v; break;
                case "FeedbackRelease": FeedbackRelease = v; break;
                case "StopPressure": StopPressure = v; break;
                case "StopSize": StopSize = v; break;
                case "IgnoreButtonFinger": IgnoreButtonFinger = v; break;
                case "IgnoreNearFingers": IgnoreNearFingers = v; break;
                case "PalmRejection": PalmRejection = v; break;
                case "InvertMovement": InvertMovement = v; break;
                case "AllowNonAdminAccess": AllowNonAdminAccess = v; break;
                default: throw new ArgumentException("unknown driver setting " + name);
            }
        }

        // Reads the current values (falls back to defaults when the driver has never been configured).
        public static DriverSettings Load()
        {
            var s = new DriverSettings();
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(UmdfParameters))
                {
                    if (key == null) return s;
                    foreach (string n in Names)
                    {
                        object v = key.GetValue(n);
                        if (v is int) s.Set(n, (int)v);
                    }
                }
            }
            catch (Exception) { }
            return s;
        }

        // Writes the values to both drivers' Parameters keys. Throws on failure (e.g. not elevated).
        public void Save()
        {
            foreach (var t in Targets)
            {
                using (var services = Registry.LocalMachine.OpenSubKey(t[0], true))
                {
                    if (services == null) throw new InvalidOperationException("Unable to open HKLM\\" + t[0] + " for writing.");
                    using (var device = services.CreateSubKey(t[1]))
                    using (var parameters = device.CreateSubKey("Parameters"))
                    {
                        foreach (string n in Names) parameters.SetValue(n, Get(n), RegistryValueKind.DWord);
                    }
                }
            }
        }

        // Compact one-argument form used to hand the settings to an elevated copy of this exe.
        public string ToArgument()
        {
            var parts = new List<string>();
            foreach (string n in Names) parts.Add(n + "=" + Get(n).ToString(CultureInfo.InvariantCulture));
            return string.Join(",", parts.ToArray());
        }

        public static DriverSettings FromArgument(string arg)
        {
            var s = new DriverSettings();
            foreach (string part in arg.Split(','))
            {
                int eq = part.IndexOf('=');
                int v;
                if (eq <= 0 || !int.TryParse(part.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                    throw new FormatException("bad driver setting '" + part + "'");
                s.Set(part.Substring(0, eq), v);
            }
            return s;
        }

        public static bool IsElevated()
        {
            using (var id = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}

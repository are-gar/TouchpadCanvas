using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TrackpadCanvas
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // Per-monitor DPI awareness so cursor and touch coordinates are real screen pixels.
            try
            {
                if (!Native.SetProcessDpiAwarenessContext(Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                    Native.SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException) { Native.SetProcessDPIAware(); }

            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string logPath = Path.Combine(dir, "TrackpadCanvas.log");

            // --write-icon <path>: save the app icon as a .ico (used by build.ps1 to embed it in the exe).
            int wi = Array.IndexOf(args, "--write-icon");
            if (wi >= 0 && wi + 1 < args.Length)
            {
                AppIcon.Write(args[wi + 1]);
                return 0;
            }

            // --probe: list precision touchpads to the log and exit (no UI, no injection).
            if (Array.Exists(args, a => a == "--probe"))
            {
                using (var w = Program.OpenLog(logPath))
                {
                    w.WriteLine("---- probe " + DateTime.Now + " ----");
                    var found = EnumerateTouchpads(w.WriteLine);
                    w.WriteLine(found.Count + " precision touchpad(s) found.");
                    foreach (var d in found) d.Dispose();
                    byte rid;
                    string bpath = BatteryReader.FindDevicePath(out rid);
                    if (bpath == null) w.WriteLine("No battery interface found.");
                    else
                    {
                        string err;
                        var s = BatteryReader.Read(bpath, rid, out err);
                        w.WriteLine(s == null ? "Battery read failed: " + err
                            : string.Format("Battery {0}% charging={1} full={2} flags=[{3}]", s.Percent, s.Charging, s.FullyCharged, s.Flags));
                    }
                    string dsrc;
                    int dp = DriverControl.TryReadBattery(out dsrc);
                    w.WriteLine("Magic Trackpad 2 driver installed: " + DriverControl.IsInstalled());
                    w.WriteLine(dp > 0 ? string.Format("Driver battery {0}% via {1}", dp, dsrc)
                        : "Magic Trackpad 2 driver did not report a battery level.");
                }
                return 0;
            }

            // --apply-driver-settings <k=v,...>: elevated helper started by the "Trackpad driver" tab.
            // Writes the driver settings to HKLM and asks the driver to reload them. No UI.
            int ai = Array.IndexOf(args, "--apply-driver-settings");
            if (ai >= 0 && ai + 1 < args.Length)
                return ApplyDriverSettings(args[ai + 1], logPath);

            bool created;
            using (var mutex = new Mutex(true, "TrackpadCanvas.SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("Trackpad Canvas is already running (check the system tray).", "Trackpad Canvas");
                    return 1;
                }
                var settings = new Settings(Path.Combine(dir, "TrackpadCanvas.ini"));
                settings.Load();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool hidden = Array.Exists(args, a => a == "--minimized");
                Application.Run(new MainForm(settings, logPath, hidden));
            }
            return 0;
        }

        // Scales a layout size designed at 96 DPI to the system DPI (the process is DPI-aware, so
        // WinForms doesn't stretch the window, but the system font already is larger on high-DPI screens).
        static float dpiScale;
        public static int Px(int px)
        {
            if (dpiScale == 0)
            {
                try { dpiScale = GetDpiForSystem() / 96f; }
                catch (EntryPointNotFoundException) { dpiScale = 1; }
                if (dpiScale <= 0) dpiScale = 1;
            }
            return (int)Math.Round(px * dpiScale);
        }
        public static System.Drawing.Size Sz(int w, int h) { return new System.Drawing.Size(Px(w), Px(h)); }

        [DllImport("user32.dll")]
        static extern uint GetDpiForSystem();

        // Appends to the log while letting the running app and its elevated helper write to it at the same time.
        public static StreamWriter OpenLog(string path)
        {
            return new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
        }

        static int ApplyDriverSettings(string arg, string logPath)
        {
            int code;
            string message;
            try
            {
                DriverSettings.FromArgument(arg).Save();
                bool reloaded = DriverControl.ReloadSettings();
                code = reloaded ? 0 : DriverPanel.ExitSavedNotReloaded;
                message = reloaded ? "saved and reloaded" : "saved; no trackpad acknowledged the reload";
            }
            catch (Exception ex)
            {
                code = 2;
                message = "failed: " + ex.Message;
            }
            try
            {
                using (var w = Program.OpenLog(logPath))
                    w.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Driver settings " + message + " [" + arg + "]");
            }
            catch (IOException) { }
            return code;
        }

        public static List<PtpDevice> EnumerateTouchpads(Action<string> log)
        {
            var result = new List<PtpDevice>();
            uint n = 0;
            uint cb = (uint)Marshal.SizeOf(typeof(Native.RAWINPUTDEVICELIST));
            Native.GetRawInputDeviceList(null, ref n, cb);
            if (n == 0) return result;
            var list = new Native.RAWINPUTDEVICELIST[n];
            if (Native.GetRawInputDeviceList(list, ref n, cb) == unchecked((uint)-1)) return result;
            for (int i = 0; i < n; i++)
            {
                if (list[i].dwType != Native.RIM_TYPEHID) continue;
                string err;
                var d = PtpDevice.TryCreate(list[i].hDevice, out err);
                if (d == null) continue;
                log("Trackpad found: " + d.Name);
                log("  " + d.Description);
                result.Add(d);
            }
            return result;
        }
    }
}

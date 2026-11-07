using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrackpadCanvas
{
    internal sealed class MainForm : Form
    {
        readonly Settings settings;
        readonly TouchInjector injector = new TouchInjector();
        readonly GestureEngine engine;
        readonly Dictionary<IntPtr, PtpDevice> devices = new Dictionary<IntPtr, PtpDevice>();
        readonly Timer timer = new Timer();
        readonly StreamWriter logFile;
        readonly bool startHidden;

        NotifyIcon tray;
        ToolStripMenuItem trayEnabled;
        CheckBox chkEnabled, chkOnlyApps, chkReverse, chkScrollElsewhere, chkReverseScroll;
        TextBox txtApps, txtLog;
        NumericUpDown numScale, numScroll;
        Label lblScroll;
        readonly WheelInjector wheel = new WheelInjector();
        Label lblDevice, lblLive, lblBattery;

        readonly Timer batteryTimer = new Timer();
        string batteryPath;
        byte batteryReportId;
        bool batteryBusy;
        BatteryStatus lastBattery, lastGoodBattery;
        DateTime lastGoodTime;
        string lastBatteryError;

        bool IsWired()
        {
            return devices.Values.Any(d => d != null && d.Name.IndexOf("VID_05AC", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        int warnedAt = 101; // lowest warning threshold already shown

        IntPtr rawBuf = IntPtr.Zero;
        int rawBufSize;
        int lastLiveTick;
        int lastContactCount = -1;
        IntPtr lastFg = IntPtr.Zero;
        bool lastFgAllowed;
        bool loggedFirstFrame;

        public MainForm(Settings settings, string logPath, bool startHidden)
        {
            this.settings = settings;
            this.startHidden = startHidden;
            try { logFile = Program.OpenLog(logPath); logFile.AutoFlush = true; } catch (IOException) { }

            engine = new GestureEngine(injector, wheel);
            engine.ChooseMode = ChooseGestureMode;
            engine.Log += Log;
            injector.Log += Log;
            wheel.Log += Log;
            ApplySettings();
            if (settings.LastBatteryPercent >= 0)
            {
                lastGoodBattery = new BatteryStatus { Percent = settings.LastBatteryPercent };
                lastGoodTime = settings.LastBatteryTime;
            }

            BuildUi();

            timer.Interval = 30;
            timer.Tick += (s, e) => engine.Tick();
            timer.Start();

            batteryTimer.Interval = 5 * 60 * 1000;
            batteryTimer.Tick += (s, e) => RefreshBattery();
            batteryTimer.Start();
        }

        void BuildUi()
        {
            Text = "Trackpad Canvas";
            Icon = AppIcon.Load();
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = Program.Sz(640, 600);
            MinimumSize = Program.Sz(500, 480);

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(10),
                AutoSize = false,
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            chkEnabled = new CheckBox { Text = "Enabled", Checked = settings.Enabled, AutoSize = true };
            chkEnabled.CheckedChanged += (s, e) => { settings.Enabled = chkEnabled.Checked; SettingsChanged(); };
            table.Controls.Add(chkEnabled, 0, 0);
            table.SetColumnSpan(chkEnabled, 2);

            chkOnlyApps = new CheckBox { Text = "Touch gestures (pan, zoom, rotate) only in:", Checked = settings.OnlyInApps, AutoSize = true };
            chkOnlyApps.CheckedChanged += (s, e) => { settings.OnlyInApps = chkOnlyApps.Checked; SettingsChanged(); };
            table.Controls.Add(chkOnlyApps, 0, 1);
            txtApps = new TextBox { Text = settings.Apps, Dock = DockStyle.Fill };
            txtApps.Leave += (s, e) => { settings.Apps = txtApps.Text; SettingsChanged(); };
            table.Controls.Add(txtApps, 1, 1);

            var lblScale = new Label { Text = "Canvas movement (pixels per mm):", AutoSize = true, Anchor = AnchorStyles.Left };
            table.Controls.Add(lblScale, 0, 2);
            numScale = new NumericUpDown
            {
                Minimum = 1, Maximum = 40, DecimalPlaces = 1, Increment = 0.5m,
                Value = (decimal)Math.Max(1, Math.Min(40, settings.PxPerMm)), Width = Program.Px(80),
            };
            numScale.ValueChanged += (s, e) => { settings.PxPerMm = (double)numScale.Value; SettingsChanged(); };
            table.Controls.Add(numScale, 1, 2);

            chkReverse = new CheckBox { Text = "Reverse pan direction", Checked = settings.ReversePan, AutoSize = true };
            chkReverse.CheckedChanged += (s, e) => { settings.ReversePan = chkReverse.Checked; SettingsChanged(); };
            table.Controls.Add(chkReverse, 0, 3);
            table.SetColumnSpan(chkReverse, 2);

            chkScrollElsewhere = new CheckBox
            {
                Text = "Everywhere else: two-finger scroll and pinch-zoom like a normal touchpad",
                Checked = settings.ScrollElsewhere, AutoSize = true, Margin = new Padding(3, 12, 3, 3),
            };
            chkScrollElsewhere.CheckedChanged += (s, e) => { settings.ScrollElsewhere = chkScrollElsewhere.Checked; SettingsChanged(); };
            table.Controls.Add(chkScrollElsewhere, 0, 4);
            table.SetColumnSpan(chkScrollElsewhere, 2);

            lblScroll = new Label { Text = "Scroll speed:", AutoSize = true, Anchor = AnchorStyles.Left };
            table.Controls.Add(lblScroll, 0, 5);
            numScroll = new NumericUpDown
            {
                Minimum = 2, Maximum = 100, DecimalPlaces = 0, Increment = 2,
                Value = (decimal)Math.Max(2, Math.Min(100, settings.ScrollSpeed)), Width = Program.Px(80),
            };
            numScroll.ValueChanged += (s, e) => { settings.ScrollSpeed = (double)numScroll.Value; SettingsChanged(); };
            table.Controls.Add(numScroll, 1, 5);

            chkReverseScroll = new CheckBox { Text = "Reverse scroll direction", Checked = settings.ReverseScroll, AutoSize = true };
            chkReverseScroll.CheckedChanged += (s, e) => { settings.ReverseScroll = chkReverseScroll.Checked; SettingsChanged(); };
            table.Controls.Add(chkReverseScroll, 0, 6);
            table.SetColumnSpan(chkReverseScroll, 2);

            lblDevice = new Label { Text = "Trackpad: waiting for input...", AutoSize = true, MaximumSize = Program.Sz(560, 0), Margin = new Padding(3, 12, 3, 3) };
            table.Controls.Add(lblDevice, 0, 7);
            table.SetColumnSpan(lblDevice, 2);

            lblBattery = new Label { Text = "Battery: checking...", AutoSize = true };
            table.Controls.Add(lblBattery, 0, 8);
            table.SetColumnSpan(lblBattery, 2);

            lblLive = new Label { Text = "Fingers: -", AutoSize = true };
            table.Controls.Add(lblLive, 0, 9);
            table.SetColumnSpan(lblLive, 2);

            txtLog = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericMonospace, 8.5f),
            };
            table.Controls.Add(txtLog, 0, 10);
            table.SetColumnSpan(txtLog, 2);
            for (int i = 0; i < 10; i++) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var gestureTab = new TabPage("Gestures") { UseVisualStyleBackColor = true };
            gestureTab.Controls.Add(table);
            var driverTab = new TabPage("Trackpad driver") { UseVisualStyleBackColor = true };
            var driver = new DriverPanel { Dock = DockStyle.Fill };
            driver.Log += Log;
            driverTab.Controls.Add(driver);
            tabs.TabPages.Add(gestureTab);
            tabs.TabPages.Add(driverTab);
            Controls.Add(tabs);
            UpdateControlStates();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Show", null, (s, e) => ShowWindow());
            trayEnabled = new ToolStripMenuItem("Enabled", null, (s, e) => chkEnabled.Checked = !chkEnabled.Checked);
            trayEnabled.Checked = settings.Enabled;
            menu.Items.Add(trayEnabled);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => Close());
            tray = new NotifyIcon { Icon = AppIcon.Load(SystemInformation.SmallIconSize), Text = "Trackpad Canvas", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (s, e) => ShowWindow();

            Resize += (s, e) => { if (WindowState == FormWindowState.Minimized) Hide(); };
        }

        void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            RefreshBattery();
        }

        // Reads the battery on a worker thread (the HID request can block over Bluetooth).
        void RefreshBattery()
        {
            if (batteryBusy || !IsHandleCreated) return;
            batteryBusy = true;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                string path = batteryPath;
                byte rid = batteryReportId;
                string error = null;
                BatteryStatus status = null;
                try
                {
                    if (path == null) path = BatteryReader.FindDevicePath(out rid);
                    if (path == null) error = "no battery interface found";
                    else status = BatteryReader.Read(path, rid, out error);
                }
                catch (Exception ex) { error = ex.Message; }
                bool fromDriver = false;
                if (status == null)
                {
                    // Fall back to the Magic Trackpad 2 driver, which can also report the level over USB.
                    string source;
                    int p = DriverControl.TryReadBattery(out source);
                    if (p > 0)
                    {
                        status = new BatteryStatus { Percent = p, Flags = source };
                        fromDriver = true;
                    }
                }
                try
                {
                    BeginInvoke(new Action(() => ApplyBattery(status, status != null && !fromDriver ? path : null, rid, error)));
                }
                catch (InvalidOperationException) { } // form closed
            });
        }

        void ApplyBattery(BatteryStatus s, string path, byte rid, string error)
        {
            batteryBusy = false;
            batteryPath = path;
            batteryReportId = rid;
            if (s == null && IsWired())
            {
                // Over USB, Windows fails to start the trackpad's battery interface (MI_00, Code 10),
                // so the level can't be read; the cable is charging it anyway.
                string last = lastGoodBattery != null
                    ? string.Format(" (last reading {0}% at {1:HH:mm})", lastGoodBattery.Percent, lastGoodTime)
                    : "";
                lblBattery.Text = "Battery: charging over USB cable - level not readable while wired" + last;
                tray.Text = "Trackpad Canvas - charging over USB";
                warnedAt = 101;
                if (lastBattery != null || lastBatteryError != "wired") Log("Trackpad is on USB; battery level not readable while wired.");
                lastBatteryError = "wired";
                lastBattery = null;
                return;
            }
            if (s == null)
            {
                lblBattery.Text = "Battery: unavailable";
                tray.Text = "Trackpad Canvas";
                if (error != lastBatteryError) Log("Battery read failed: " + error);
                lastBatteryError = error;
                lastBattery = null;
                return;
            }
            lastBatteryError = null;
            if (s.Flags.StartsWith("driver") && IsWired()) s.Charging = true; // driver reports level only

            string state = s.FullyCharged ? " (fully charged)" : s.Charging ? " (charging)" : "";
            lblBattery.Text = "Battery: " + s.Percent + "%" + state;
            tray.Text = "Trackpad Canvas - battery " + s.Percent + "%" + state;
            if (lastBattery == null || lastBattery.Percent != s.Percent || lastBattery.Flags != s.Flags)
                Log(string.Format("Battery {0}%{1} [flags {2}]", s.Percent, state, s.Flags.Length > 0 ? s.Flags : "none"));
            lastBattery = s;
            lastGoodBattery = s;
            lastGoodTime = DateTime.Now;
            if (settings.LastBatteryPercent != s.Percent)
            {
                settings.LastBatteryPercent = s.Percent;
                settings.LastBatteryTime = lastGoodTime;
                settings.Save();
            }

            if (s.Charging || s.FullyCharged || s.Percent > 25) warnedAt = 101;
            else
            {
                int threshold = s.Percent <= 10 ? 10 : s.Percent <= 20 ? 20 : 101;
                if (threshold < warnedAt)
                {
                    warnedAt = threshold;
                    tray.ShowBalloonTip(10000, "Magic Trackpad battery low",
                        "Battery is at " + s.Percent + "%. Plug in the USB-C cable to charge it.", ToolTipIcon.Warning);
                }
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            if (startHidden && !IsHandleCreated)
            {
                CreateHandle();
                value = false;
            }
            base.SetVisibleCore(value);
        }

        void SettingsChanged()
        {
            ApplySettings();
            if (trayEnabled != null) trayEnabled.Checked = settings.Enabled;
            lastFg = IntPtr.Zero;
            settings.Save();
            if (!settings.Enabled) engine.Cancel();
        }

        void ApplySettings()
        {
            engine.PxPerMm = settings.PxPerMm;
            engine.ReversePan = settings.ReversePan;
            engine.ScrollSpeed = settings.ScrollSpeed;
            engine.ReverseScroll = settings.ReverseScroll;
            UpdateControlStates();
        }

        // The "everywhere else" options only matter when touch gestures are limited to a list of apps.
        void UpdateControlStates()
        {
            if (chkScrollElsewhere == null) return;
            chkScrollElsewhere.Enabled = settings.OnlyInApps;
            bool scrolling = settings.OnlyInApps && settings.ScrollElsewhere;
            lblScroll.Enabled = numScroll.Enabled = chkReverseScroll.Enabled = scrolling;
        }

        // Touch gestures in the listed art apps; ordinary wheel scrolling and Ctrl+wheel zoom everywhere else.
        GestureMode ChooseGestureMode()
        {
            if (!settings.Enabled) return GestureMode.None;
            if (!settings.OnlyInApps || IsListedAppFocused()) return GestureMode.Touch;
            return settings.ScrollElsewhere ? GestureMode.Wheel : GestureMode.None;
        }

        bool IsListedAppFocused()
        {
            IntPtr fg = Native.GetForegroundWindow();
            if (fg == lastFg) return lastFgAllowed;
            lastFg = fg;
            lastFgAllowed = false;
            try
            {
                uint pid;
                Native.GetWindowThreadProcessId(fg, out pid);
                string name = Process.GetProcessById((int)pid).ProcessName;
                lastFgAllowed = settings.AppList.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            return lastFgAllowed;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Log("Trackpad Canvas started (" + (Environment.Is64BitProcess ? "x64" : "x86") + ")");
            var rid = new[]
            {
                new Native.RAWINPUTDEVICE
                {
                    usUsagePage = 0x0D, usUsage = 0x05,
                    dwFlags = Native.RIDEV_INPUTSINK | Native.RIDEV_DEVNOTIFY,
                    hwndTarget = Handle,
                },
            };
            if (Native.RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(Native.RAWINPUTDEVICE))))
                Log("Listening for precision touchpad input.");
            else
                Log("RegisterRawInputDevices failed, error " + Marshal.GetLastWin32Error());
            if (injector.Initialize()) Log("Touch injection ready.");
            foreach (var d in Program.EnumerateTouchpads(Log)) AddDevice(d);
            RefreshBattery();
        }

        void AddDevice(PtpDevice d)
        {
            devices[d.Handle] = d;
            lblDevice.Text = "Trackpad: " + d.Description;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_INPUT) HandleRawInput(m.LParam);
            else if (m.Msg == Native.WM_INPUT_DEVICE_CHANGE && (int)m.WParam == 1) // GIDC_ARRIVAL
            {
                batteryPath = null;
                RefreshBattery();
            }
            else if (m.Msg == Native.WM_INPUT_DEVICE_CHANGE && (int)m.WParam == Native.GIDC_REMOVAL)
            {
                PtpDevice d;
                if (devices.TryGetValue(m.LParam, out d))
                {
                    devices.Remove(m.LParam);
                    if (d != null) { d.Dispose(); Log("Trackpad disconnected."); }
                    engine.Cancel();
                }
            }
            base.WndProc(ref m);
        }

        void HandleRawInput(IntPtr hRawInput)
        {
            int headerSize = Marshal.SizeOf(typeof(Native.RAWINPUTHEADER));
            uint size = 0;
            Native.GetRawInputData(hRawInput, Native.RID_INPUT, IntPtr.Zero, ref size, (uint)headerSize);
            if (size == 0) return;
            if (size > rawBufSize)
            {
                if (rawBuf != IntPtr.Zero) Marshal.FreeHGlobal(rawBuf);
                rawBufSize = (int)size;
                rawBuf = Marshal.AllocHGlobal(rawBufSize);
            }
            if (Native.GetRawInputData(hRawInput, Native.RID_INPUT, rawBuf, ref size, (uint)headerSize) == unchecked((uint)-1)) return;

            var header = (Native.RAWINPUTHEADER)Marshal.PtrToStructure(rawBuf, typeof(Native.RAWINPUTHEADER));
            if (header.dwType != Native.RIM_TYPEHID) return;

            PtpDevice dev;
            if (!devices.TryGetValue(header.hDevice, out dev))
            {
                string err;
                dev = PtpDevice.TryCreate(header.hDevice, out err);
                devices[header.hDevice] = dev; // null is cached so unsupported devices are not re-probed
                if (dev != null) { Log("Trackpad found: " + dev.Description); AddDevice(dev); }
                else Log("Ignoring HID device: " + err);
            }
            if (dev == null) return;

            int sizeHid = Marshal.ReadInt32(rawBuf, headerSize);
            int count = Marshal.ReadInt32(rawBuf, headerSize + 4);
            if (sizeHid <= 0 || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                var report = new byte[sizeHid];
                Marshal.Copy(IntPtr.Add(rawBuf, headerSize + 8 + i * sizeHid), report, 0, sizeHid);
                var frame = dev.Parse(report);
                if (frame == null) continue;
                if (!loggedFirstFrame && frame.Count > 0)
                {
                    loggedFirstFrame = true;
                    Log(string.Format("First touch received: {0} finger(s), first at {1:0.0},{2:0.0} mm", frame.Count, frame[0].X, frame[0].Y));
                }
                engine.OnFrame(frame);
                UpdateLive(frame);
            }
        }

        void UpdateLive(List<Contact> frame)
        {
            int now = Environment.TickCount;
            if (frame.Count == lastContactCount && now - lastLiveTick < 100) return;
            lastLiveTick = now;
            lastContactCount = frame.Count;
            if (!Visible) return;
            string pts = string.Join("  ", frame.Select(c => string.Format("#{0} ({1:0.0}, {2:0.0})", c.Id, c.X, c.Y)));
            lblLive.Text = string.Format("Fingers: {0}   Gesture: {1}   {2}", frame.Count, engine.StateName, pts);
        }

        void Log(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg;
            if (logFile != null) logFile.WriteLine(line);
            if (txtLog == null) return;
            if (txtLog.TextLength > 60000) txtLog.Text = txtLog.Text.Substring(30000);
            txtLog.AppendText(line + Environment.NewLine);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            engine.Cancel();
            timer.Stop();
            batteryTimer.Stop();
            settings.Save();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            foreach (var d in devices.Values) if (d != null) d.Dispose();
            if (rawBuf != IntPtr.Zero) Marshal.FreeHGlobal(rawBuf);
            if (logFile != null) logFile.Dispose();
            base.OnFormClosed(e);
        }
    }
}

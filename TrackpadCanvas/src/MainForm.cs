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
        PixelTextBox txtApps;
        LogView txtLog;
        RetroSlider numScale, numScroll;
        RetroTextField appsField;
        RetroButton btnAddApp;
        readonly WheelInjector wheel = new WheelInjector();
        RetroLabel lblDevice, lblBattery;
        HeaderBar header;
        RetroTabs tabs;
        Control[] pages;
        TouchView touchView;
        readonly string logPath;
        const int LivePage = 2;

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
            this.logPath = logPath;
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
            AutoScaleMode = AutoScaleMode.None; // every size below is already scaled with Theme.Px
            BackColor = Theme.Paper;
            // Compact by default, and never more than most of the screen.
            var work = Screen.FromPoint(Cursor.Position).WorkingArea;
            ClientSize = new Size(Math.Min(Theme.Px(Theme.WindowWU), work.Width * 8 / 10), Math.Min(Theme.Px(Theme.WindowHU), work.Height * 75 / 100));
            MinimumSize = new Size(Math.Min(Theme.Px(Math.Min(400, Theme.WindowWU)), work.Width * 8 / 10), Math.Min(Theme.Px(320), work.Height * 6 / 10));

            header = new HeaderBar { Dock = DockStyle.Top };
            chkEnabled = header.Switch;
            chkEnabled.Checked = settings.Enabled;
            chkEnabled.CheckedChanged += (s, e) => { settings.Enabled = chkEnabled.Checked; SettingsChanged(); };
            header.SetBattery(-1, false, "…", "Battery: checking…");

            tabs = new RetroTabs(new[] { "Gestures", "Trackpad driver", "Live view & log" },
                                 new[] { Theme.Pink, Theme.Yellow, Theme.Mint })
                { Dock = Theme.TabsAt == 1 ? DockStyle.Left : Theme.TabsAt == 2 ? DockStyle.Bottom : DockStyle.Top };
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Paper };
            pages = new[] { BuildGesturesPage(), BuildDriverPage(), BuildLivePage() };
            foreach (var p in pages) { p.Dock = DockStyle.Fill; host.Controls.Add(p); }
            tabs.SelectedIndexChanged += (s, e) => ShowPage(tabs.SelectedIndex);
            ShowPage(0);

            // Docking runs from the last-added control: header first, then the tabs, then the page area.
            Controls.Add(host);
            Controls.Add(tabs);
            if (Theme.StatusBar) Controls.Add(new PixelStatusBar(header) { Dock = DockStyle.Bottom });
            if (Theme.HeaderMode == 2)
            {
                // Preview variant: no header; the master switch sits at the right end of the tab bar.
                header.Controls.Remove(chkEnabled);
                header.Switch.PlateColor = tabs.BackColor;
                tabs.Trailing = chkEnabled;
                header.Visible = false;
            }
            Controls.Add(header);
            UpdateControlStates();
            UpdateHeader();

            var menu = new ContextMenuStrip();
            Theme.StyleMenu(menu);
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

        Control BuildGesturesPage()
        {
            var page = new ScrollPage();
            int indent = ToggleArt.TextX;

            var tip = new RetroNote(
                "Setup tip: in Windows touchpad settings, turn off “Drag two fingers to scroll” and “Pinch to zoom”. " +
                "Otherwise Windows scrolls and zooms at the same time as Trackpad Canvas.", Theme.Yellow, "i");
            tip.ActionText = "Open Windows touchpad settings ›";
            tip.ActionClicked += (s, e) => OpenTouchpadSettings();
            page.Stack.Controls.Add(tip);

            var where = new Card("WHERE GESTURES WORK", Theme.Blue);
            chkOnlyApps = new RetroCheck("Only use touch gestures in these apps",
                "Pan, pinch-zoom and rotate become real touchscreen gestures in the apps listed below. Turn this off to use them in every app.")
                { Checked = settings.OnlyInApps, Accent = Theme.Blue };
            chkOnlyApps.CheckedChanged += (s, e) => { settings.OnlyInApps = chkOnlyApps.Checked; SettingsChanged(); };
            where.Controls.Add(chkOnlyApps);

            appsField = new RetroTextField("App names", "Process names separated by commas, without “.exe”. Press Enter to save.", settings.Apps)
                { Margin = new Padding(indent, 0, 0, 0) };
            txtApps = appsField.Box;
            txtApps.Leave += (s, e) => CommitApps();
            txtApps.Commit += (s, e) => CommitApps();
            where.Controls.Add(appsField);

            btnAddApp = new RetroButton("+  Add a running app…", Theme.Field);
            btnAddApp.Click += (s, e) => ShowRunningApps();
            var addRow = new HRow { Margin = new Padding(indent, 0, 0, 0) };
            addRow.Controls.Add(btnAddApp);
            where.Controls.Add(addRow);
            page.Stack.Controls.Add(where);

            var canvas = new Card("CANVAS GESTURES", Theme.Pink);
            numScale = new RetroSlider("Canvas movement", "How far the canvas moves for each millimetre your fingers travel.",
                1, 40, 0.5, settings.PxPerMm) { Accent = Theme.Pink, Format = v => v.ToString("0.0") + " px/mm" };
            numScale.ValueChanged += (s, e) => { settings.PxPerMm = numScale.Value; SettingsChanged(); };
            canvas.Controls.Add(numScale);
            chkReverse = new RetroCheck("Reverse pan direction", "Flips panning only; zoom and rotate are unaffected.")
                { Checked = settings.ReversePan, Accent = Theme.Pink };
            chkReverse.CheckedChanged += (s, e) => { settings.ReversePan = chkReverse.Checked; SettingsChanged(); };
            canvas.Controls.Add(chkReverse);
            page.Stack.Controls.Add(canvas);

            var elsewhere = new Card("EVERYWHERE ELSE", Theme.Mint);
            chkScrollElsewhere = new RetroCheck("Scroll and pinch-zoom like a normal touchpad",
                "In apps that aren't on the list, two fingers scroll the mouse wheel and a pinch zooms with Ctrl + wheel. " +
                "Only used when touch gestures are limited to the apps above.")
                { Checked = settings.ScrollElsewhere, Accent = Theme.Mint };
            chkScrollElsewhere.CheckedChanged += (s, e) => { settings.ScrollElsewhere = chkScrollElsewhere.Checked; SettingsChanged(); };
            elsewhere.Controls.Add(chkScrollElsewhere);
            numScroll = new RetroSlider("Scroll speed", "Wheel units per mm of finger movement (120 = one wheel notch).",
                2, 100, 2, settings.ScrollSpeed) { Accent = Theme.Mint, Format = v => v.ToString("0"), Margin = new Padding(indent, 0, 0, 0) };
            numScroll.ValueChanged += (s, e) => { settings.ScrollSpeed = numScroll.Value; SettingsChanged(); };
            elsewhere.Controls.Add(numScroll);
            chkReverseScroll = new RetroCheck("Reverse scroll direction", "Off = natural scrolling: the content follows your fingers.")
                { Checked = settings.ReverseScroll, Accent = Theme.Mint, Margin = new Padding(indent, 0, 0, 0) };
            chkReverseScroll.CheckedChanged += (s, e) => { settings.ReverseScroll = chkReverseScroll.Checked; SettingsChanged(); };
            elsewhere.Controls.Add(chkReverseScroll);
            page.Stack.Controls.Add(elsewhere);
            return page;
        }

        Control BuildDriverPage()
        {
            var driver = new DriverPanel();
            driver.Log += Log;
            return driver;
        }

        Control BuildLivePage()
        {
            var page = new ScrollPage();

            var live = new Card("LIVE VIEW", Theme.Mint);
            live.Controls.Add(new RetroLabel("Rest your fingers on the trackpad to check it is being read. " +
                "With two fingers down you can see which gesture Trackpad Canvas is sending.", Theme.Text, Theme.InkSoft));
            touchView = new TouchView();
            live.Controls.Add(touchView);
            page.Stack.Controls.Add(live);

            var status = new Card("STATUS", Theme.Peach);
            lblDevice = new RetroLabel("Trackpad: waiting for input…", Theme.Text, Theme.Ink);
            lblBattery = new RetroLabel("Battery: checking…", Theme.Text, Theme.Ink);
            status.Controls.Add(lblDevice);
            status.Controls.Add(lblBattery);
            page.Stack.Controls.Add(status);

            var logCard = new Card("ACTIVITY LOG", Theme.Lavender);
            txtLog = new LogView();
            logCard.Controls.Add(txtLog);
            var row = new HRow();
            var copy = new RetroButton("Copy log", Theme.Field);
            copy.Click += (s, e) => { try { if (!txtLog.IsEmpty) Clipboard.SetText(txtLog.AllText); } catch (Exception) { } };
            var open = new RetroButton("Open log file", Theme.Field);
            open.Click += (s, e) => { try { Process.Start(logPath); } catch (Exception) { } };
            var clear = new RetroButton("Clear", Theme.Field);
            clear.Click += (s, e) => txtLog.Clear();
            row.Controls.Add(copy);
            row.Controls.Add(open);
            row.Controls.Add(clear);
            logCard.Controls.Add(row);
            page.Stack.Controls.Add(logCard);
            return page;
        }

        void ShowPage(int index)
        {
            for (int i = 0; i < pages.Length; i++) pages[i].Visible = i == index;
            if (index == LivePage && touchView != null) touchView.SetFrame(null, engine.StateName);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (tabs != null && (keyData == (Keys.Control | Keys.Tab) || keyData == (Keys.Control | Keys.Shift | Keys.Tab)))
            {
                int step = (keyData & Keys.Shift) != 0 ? tabs.Count - 1 : 1;
                tabs.SelectedIndex = (tabs.SelectedIndex + step) % tabs.Count;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        static void OpenTouchpadSettings()
        {
            try { Process.Start("ms-settings:devices-touchpad"); } catch (Exception) { }
        }

        void CommitApps()
        {
            if (settings.Apps == txtApps.Text) return;
            settings.Apps = txtApps.Text;
            SettingsChanged();
        }

        // Offers the apps that currently have a window, so users don't have to know process names.
        void ShowRunningApps()
        {
            CommitApps();
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            int self = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcesses())
            {
                try { if (p.Id != self && p.MainWindowHandle != IntPtr.Zero) names.Add(p.ProcessName); }
                catch (Exception) { }
                finally { p.Dispose(); }
            }
            var menu = new ContextMenuStrip();
            Theme.StyleMenu(menu);
            var listed = settings.AppList;
            foreach (var name in names)
            {
                string n = name;
                bool already = listed.Any(a => string.Equals(a, n, StringComparison.OrdinalIgnoreCase));
                var item = new ToolStripMenuItem(n) { Checked = already, Enabled = !already };
                item.Click += (s, e) =>
                {
                    string cur = txtApps.Text.Trim().TrimEnd(',').Trim();
                    txtApps.Text = cur.Length == 0 ? n : cur + ", " + n;
                    CommitApps();
                };
                menu.Items.Add(item);
            }
            if (menu.Items.Count == 0) menu.Items.Add(new ToolStripMenuItem("No apps with windows found") { Enabled = false });
            menu.Closed += (s, e) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(btnAddApp, new Point(0, btnAddApp.Height));
        }

        static string FriendlyName(PtpDevice d)
        {
            string n = d.Name ?? "";
            bool apple = n.IndexOf("05AC", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("004C", StringComparison.OrdinalIgnoreCase) >= 0;
            return apple ? "Magic Trackpad" : "Precision touchpad";
        }

        void UpdateHeader()
        {
            if (header == null) return;
            var dev = devices.Values.FirstOrDefault(d => d != null);
            string pad = dev != null ? FriendlyName(dev) + " connected" : "Waiting for a trackpad…";
            header.Status = settings.Enabled ? pad : "Paused  ·  " + pad;
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
                header.SetBattery(-1, true, "USB", lblBattery.Text);
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
                header.SetBattery(-1, false, "--", lblBattery.Text);
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
            header.SetBattery(s.Percent, s.Charging && !s.FullyCharged, s.Percent + "%", lblBattery.Text);
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
            UpdateHeader();
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
            appsField.Enabled = btnAddApp.Enabled = settings.OnlyInApps;
            chkScrollElsewhere.Enabled = settings.OnlyInApps;
            bool scrolling = settings.OnlyInApps && settings.ScrollElsewhere;
            numScroll.Enabled = chkReverseScroll.Enabled = scrolling;
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
            lblDevice.Text = "Trackpad: " + FriendlyName(d) + " — " + d.Description;
            touchView.SetPad(d.WidthMm, d.HeightMm);
            UpdateHeader();
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
                    if (d != null)
                    {
                        d.Dispose();
                        Log("Trackpad disconnected.");
                        if (!devices.Values.Any(x => x != null)) lblDevice.Text = "Trackpad: disconnected";
                        UpdateHeader();
                    }
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
            if (frame.Count == lastContactCount && now - lastLiveTick < 33) return;
            lastLiveTick = now;
            lastContactCount = frame.Count;
            if (!Visible || tabs.SelectedIndex != LivePage) return;
            touchView.SetFrame(frame, engine.StateName);
        }

        void Log(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg;
            if (logFile != null) logFile.WriteLine(line);
            if (txtLog == null) return;
            txtLog.Append(line);
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

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrackpadCanvas
{
    // "Trackpad driver" tab: the Magic Trackpad 2 driver options (formerly AmtPtpControlPanel).
    // Trackpad Canvas runs as a normal user; Apply writes HKLM through an elevated copy of this exe.
    internal sealed class DriverPanel : UserControl
    {
        public event Action<string> Log = delegate { };

        RadioButton rbMac, rbDisable, rbMax, rbStopNone, rbStopPressure, rbStopSize;
        TrackBar trkStrength;
        CheckBox chkSilent, chkNearFingers, chkPalm, chkButtonFinger, chkInvert, chkNonAdmin;
        NumericUpDown numPressure, numSize;
        Label lblStatus;
        Button btnApply;
        Panel content;

        public DriverPanel()
        {
            AutoScroll = true;
            var flow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
                Dock = DockStyle.Top, Padding = new Padding(10),
            };

            lblStatus = new Label { AutoSize = true, MaximumSize = Program.Sz(560, 0), Margin = new Padding(3, 0, 3, 8) };
            flow.Controls.Add(lblStatus);

            content = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = Padding.Empty };
            content.Controls.Add(BuildFeedbackGroup());
            content.Controls.Add(BuildStopGroup());
            content.Controls.Add(BuildOtherGroup());
            flow.Controls.Add(content);

            var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            btnApply = new Button { Text = "Apply", AutoSize = true, FlatStyle = FlatStyle.System, MinimumSize = Program.Sz(90, 0) };
            btnApply.Click += (s, e) => Apply();
            buttons.Controls.Add(btnApply);
            var btnRevert = new Button { Text = "Revert", AutoSize = true, MinimumSize = Program.Sz(90, 0) };
            btnRevert.Click += (s, e) => LoadFromRegistry();
            buttons.Controls.Add(btnRevert);
            var btnWin = new Button { Text = "Windows touchpad settings", AutoSize = true };
            btnWin.Click += (s, e) => { try { Process.Start("ms-settings:devices-touchpad"); } catch (Exception) { } };
            buttons.Controls.Add(btnWin);
            flow.Controls.Add(buttons);

            Controls.Add(flow);
            HandleCreated += (s, e) => { if (!DriverSettings.IsElevated()) SendMessage(btnApply.Handle, BCM_SETSHIELD, IntPtr.Zero, new IntPtr(1)); };
            LoadFromRegistry();
        }

        GroupBox Group(string title, Control inner)
        {
            var g = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = Program.Sz(560, 0), Padding = new Padding(8) };
            inner.Dock = DockStyle.Fill;
            g.Controls.Add(inner);
            return g;
        }

        static TableLayoutPanel Table(int columns)
        {
            var t = new TableLayoutPanel { ColumnCount = columns, AutoSize = true };
            for (int i = 0; i < columns; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            return t;
        }

        Control BuildFeedbackGroup()
        {
            var t = Table(2);
            rbMac = new RadioButton { Text = "macOS-style click:", AutoSize = true };
            t.Controls.Add(rbMac, 0, 0);
            t.SetColumnSpan(rbMac, 2);

            // One-row table: cells anchored Left are vertically centred, so the labels line up with the slider.
            var strength = new TableLayoutPanel { AutoSize = true, ColumnCount = 4, RowCount = 1, Margin = new Padding(Program.Px(20), 0, 0, 0) };
            strength.Controls.Add(new Label { Text = "Click strength:   Light", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            trkStrength = new TrackBar
            {
                Minimum = 0, Maximum = 2, LargeChange = 1, Width = Program.Px(110), TickStyle = TickStyle.BottomRight,
                Anchor = AnchorStyles.Left, BackColor = SystemColors.Window,
            };
            strength.Controls.Add(trkStrength, 1, 0);
            strength.Controls.Add(new Label { Text = "Firm", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, Program.Px(24), 0) }, 2, 0);
            chkSilent = new CheckBox { Text = "Silent clicking", AutoSize = true, Anchor = AnchorStyles.Left };
            strength.Controls.Add(chkSilent, 3, 0);
            t.Controls.Add(strength, 0, 1);
            t.SetColumnSpan(strength, 2);

            rbDisable = new RadioButton { Text = "Disable haptic feedback and the force touch button completely", AutoSize = true };
            t.Controls.Add(rbDisable, 0, 2);
            t.SetColumnSpan(rbDisable, 2);
            rbMax = new RadioButton { Text = "Maximum haptic feedback (very clicky and loud!)", AutoSize = true };
            t.Controls.Add(rbMax, 0, 3);
            t.SetColumnSpan(rbMax, 2);

            rbMac.CheckedChanged += (s, e) => { trkStrength.Enabled = chkSilent.Enabled = rbMac.Checked; };
            return Group("Click feedback", t);
        }

        Control BuildStopGroup()
        {
            var t = Table(3);
            rbStopNone = new RadioButton { Text = "Do nothing", AutoSize = true };
            t.Controls.Add(rbStopNone, 0, 0);
            t.SetColumnSpan(rbStopNone, 3);

            rbStopPressure = new RadioButton { Text = "Stop the pointer if the pressure is at most", AutoSize = true };
            numPressure = new NumericUpDown { Minimum = 0, Maximum = 255, Width = Program.Px(60) };
            t.Controls.Add(rbStopPressure, 0, 1);
            t.Controls.Add(numPressure, 1, 1);
            t.Controls.Add(new Label { Text = "(0 = no pressure; 0 is a good value)", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 1);

            rbStopSize = new RadioButton { Text = "Stop the pointer if the touch area size is at most", AutoSize = true };
            numSize = new NumericUpDown { Minimum = 0, Maximum = 255, Width = Program.Px(60) };
            t.Controls.Add(rbStopSize, 0, 2);
            t.Controls.Add(numSize, 1, 2);
            t.Controls.Add(new Label { Text = "(7 is a good value)", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 2);

            EventHandler sync = (s, e) => { numPressure.Enabled = rbStopPressure.Checked; numSize.Enabled = rbStopSize.Checked; };
            rbStopPressure.CheckedChanged += sync;
            rbStopSize.CheckedChanged += sync;
            return Group("When you lift your finger from the trackpad", t);
        }

        Control BuildOtherGroup()
        {
            var t = Table(1);
            chkNearFingers = new CheckBox { Text = "Ignore fingers that are near but not touching the surface", AutoSize = true };
            chkPalm = new CheckBox { Text = "Palm rejection", AutoSize = true };
            chkButtonFinger = new CheckBox
            {
                Text = "Ignore the finger that presses the button (for dragging: click with thumb, move with index)",
                AutoSize = true, MaximumSize = Program.Sz(520, 0),
            };
            chkInvert = new CheckBox { Text = "Invert up/down and left/right (for upside-down use)", AutoSize = true };
            chkNonAdmin = new CheckBox
            {
                Text = "Let non-admin apps talk to the driver (lets Trackpad Canvas read the battery level over USB)",
                AutoSize = true, MaximumSize = Program.Sz(520, 0),
            };
            foreach (var c in new Control[] { chkNearFingers, chkPalm, chkButtonFinger, chkInvert, chkNonAdmin }) t.Controls.Add(c);
            return Group("Other options", t);
        }

        void LoadFromRegistry()
        {
            bool installed = DriverControl.IsInstalled();
            content.Enabled = btnApply.Enabled = installed;
            lblStatus.Text = installed
                ? "These settings apply to the Magic Trackpad 2 driver from this project. Applying them needs administrator rights."
                : "The Magic Trackpad 2 driver from this project is not installed, so these settings are unavailable. " +
                  "The gesture features work with any Precision Touchpad driver.";

            var s = DriverSettings.Load();
            if (s.ButtonDisabled != 0) rbDisable.Checked = true;
            else if (s.FeedbackClick == DriverSettings.MaxFeedback && s.FeedbackRelease == DriverSettings.MaxFeedback) rbMax.Checked = true;
            else rbMac.Checked = true;
            chkSilent.Checked = (s.FeedbackClick & 0xffff00) == 0 && (s.FeedbackRelease & 0xffff00) == 0;
            int click = s.FeedbackClick & 0xff, release = s.FeedbackRelease & 0xff;
            trkStrength.Value = 1;
            for (int i = 0; i < 3; i++)
                if (click == (DriverSettings.ClickPresets[i] & 0xff) && release == (DriverSettings.ReleasePresets[i] & 0xff)) trkStrength.Value = i;
            trkStrength.Enabled = chkSilent.Enabled = rbMac.Checked;

            numPressure.Value = 0;
            numSize.Value = 7;
            if (s.StopPressure >= 0) { rbStopPressure.Checked = true; numPressure.Value = Math.Min(255, s.StopPressure); }
            else if (s.StopSize >= 0) { rbStopSize.Checked = true; numSize.Value = Math.Min(255, s.StopSize); }
            else rbStopNone.Checked = true;
            numPressure.Enabled = rbStopPressure.Checked;
            numSize.Enabled = rbStopSize.Checked;

            chkNearFingers.Checked = s.IgnoreNearFingers != 0;
            chkPalm.Checked = s.PalmRejection != 0;
            chkButtonFinger.Checked = s.IgnoreButtonFinger != 0;
            chkInvert.Checked = s.InvertMovement != 0;
            chkNonAdmin.Checked = s.AllowNonAdminAccess != 0;
        }

        DriverSettings FromUi()
        {
            var s = new DriverSettings();
            if (rbDisable.Checked) { s.ButtonDisabled = 1; s.FeedbackClick = 0; s.FeedbackRelease = 0; }
            else if (rbMax.Checked) { s.FeedbackClick = DriverSettings.MaxFeedback; s.FeedbackRelease = DriverSettings.MaxFeedback; }
            else
            {
                s.FeedbackClick = DriverSettings.ClickPresets[trkStrength.Value];
                s.FeedbackRelease = DriverSettings.ReleasePresets[trkStrength.Value];
                if (chkSilent.Checked) { s.FeedbackClick &= 0xff; s.FeedbackRelease &= 0xff; }
            }
            s.StopPressure = rbStopPressure.Checked ? (int)numPressure.Value : -1;
            s.StopSize = rbStopSize.Checked ? (int)numSize.Value : -1;
            s.IgnoreNearFingers = chkNearFingers.Checked ? 1 : 0;
            s.PalmRejection = chkPalm.Checked ? 1 : 0;
            s.IgnoreButtonFinger = chkButtonFinger.Checked ? 1 : 0;
            s.InvertMovement = chkInvert.Checked ? 1 : 0;
            s.AllowNonAdminAccess = chkNonAdmin.Checked ? 1 : 0;
            return s;
        }

        void Apply()
        {
            var s = FromUi();
            if (DriverSettings.IsElevated())
            {
                try
                {
                    s.Save();
                    Finish(DriverControl.ReloadSettings() ? 0 : ExitSavedNotReloaded, null);
                }
                catch (Exception ex) { Finish(-1, ex.Message); }
                return;
            }

            // Hand the settings to an elevated copy of this exe (one UAC prompt); see Program.ApplyDriverSettings.
            btnApply.Enabled = false;
            var psi = new ProcessStartInfo(Application.ExecutablePath, "--apply-driver-settings " + s.ToArgument())
            {
                UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden,
            };
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                int code;
                string error = null;
                try
                {
                    using (var p = Process.Start(psi))
                    {
                        p.WaitForExit();
                        code = p.ExitCode;
                    }
                }
                catch (Exception ex)
                {
                    var w = ex as Win32Exception;
                    if (w != null && w.NativeErrorCode == 1223) code = -2; // UAC prompt cancelled
                    else { code = -1; error = ex.Message; }
                }
                try { BeginInvoke(new Action(() => { btnApply.Enabled = true; Finish(code, error); })); }
                catch (InvalidOperationException) { }
            });
        }

        public const int ExitSavedNotReloaded = 3;

        void Finish(int code, string error)
        {
            switch (code)
            {
                case 0:
                    Log("Driver settings applied.");
                    break;
                case ExitSavedNotReloaded:
                    Log("Driver settings saved. No trackpad acknowledged the reload; they take effect when it reconnects.");
                    break;
                case -2:
                    Log("Driver settings not applied (administrator permission was declined).");
                    break;
                default:
                    string msg = error ?? "the elevated helper failed (exit code " + code + "); see TrackpadCanvas.log";
                    Log("Driver settings not applied: " + msg);
                    MessageBox.Show(this, "Could not apply the driver settings:\n" + msg, "Trackpad Canvas", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }
        }

        const int BCM_SETSHIELD = 0x160C;
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TrackpadCanvas
{
    // "Trackpad driver" tab: the Magic Trackpad 2 driver options (formerly AmtPtpControlPanel).
    // Trackpad Canvas runs as a normal user; Apply writes HKLM through an elevated copy of this exe.
    internal sealed class DriverPanel : UserControl
    {
        public event Action<string> Log = delegate { };

        static readonly string[] StrengthNames = { "Light", "Medium", "Firm" };

        RetroRadio rbMac, rbDisable, rbMax, rbStopNone, rbStopPressure, rbStopSize;
        RetroSlider trkStrength, numPressure, numSize;
        RetroCheck chkSilent, chkNearFingers, chkPalm, chkButtonFinger, chkInvert, chkNonAdmin;
        RetroNote lblStatus;
        RetroButton btnApply;
        Pill pill;
        VStack content;
        bool loading;

        public DriverPanel()
        {
            BackColor = Theme.Paper;
            var page = new ScrollPage { Dock = DockStyle.Fill };

            lblStatus = new RetroNote("", Theme.Yellow, "i");
            page.Stack.Controls.Add(lblStatus);

            content = new VStack { Gap = Theme.Sp(9), Checker = true, BackColor = Theme.Paper, Columns = Theme.TwoColumns ? 2 : 1 };
            content.Controls.Add(BuildFeedbackCard());
            content.Controls.Add(BuildStopCard());
            content.Controls.Add(BuildOtherCard());
            page.Stack.Controls.Add(content);

            // Footer stays visible while the page scrolls, so Apply is always in reach.
            var footer = new HRow
            {
                Dock = DockStyle.Bottom, TopRule = true, BackColor = Theme.Bezel,
                Padding = new Padding(Theme.Px(10), Theme.Px(6), Theme.Px(10), Theme.Px(6)),
            };
            btnApply = new RetroButton("Apply", Theme.Yellow) { ShowShield = !DriverSettings.IsElevated() };
            btnApply.Click += (s, e) => Apply();
            var btnRevert = new RetroButton("Revert", Theme.Field);
            btnRevert.Click += (s, e) => LoadFromRegistry();
            var btnWin = new RetroButton("Windows touchpad settings", Theme.Field);
            btnWin.Click += (s, e) => { try { Process.Start("ms-settings:devices-touchpad"); } catch (Exception) { } };
            pill = new Pill();
            footer.Controls.Add(btnApply);
            footer.Controls.Add(btnRevert);
            footer.Controls.Add(btnWin);
            footer.Controls.Add(pill);
            footer.RightControl = pill;
            footer.Height = footer.GetPreferredSize(Size.Empty).Height;

            Controls.Add(page);
            Controls.Add(footer);

            foreach (var r in new RadioButton[] { rbMac, rbDisable, rbMax, rbStopNone, rbStopPressure, rbStopSize })
                r.CheckedChanged += (s, e) => { if (((RadioButton)s).Checked) MarkDirty(); };
            foreach (var c in new CheckBox[] { chkSilent, chkNearFingers, chkPalm, chkButtonFinger, chkInvert, chkNonAdmin })
                c.CheckedChanged += (s, e) => MarkDirty();
            foreach (var sl in new[] { trkStrength, numPressure, numSize })
                sl.ValueChanged += (s, e) => MarkDirty();

            LoadFromRegistry();
        }

        static Padding Indent { get { return new Padding(ToggleArt.TextX, 0, 0, 0); } }

        Control BuildFeedbackCard()
        {
            var c = new Card("CLICK FEEDBACK", Theme.Yellow);
            rbMac = new RetroRadio("macOS-style click", "A haptic click you can feel, like on a Mac.") { Accent = Theme.Yellow };
            c.Controls.Add(rbMac);
            trkStrength = new RetroSlider("Click strength", null, 0, 2, 1, 1)
            {
                TickLabels = StrengthNames, Format = v => StrengthNames[(int)v], Accent = Theme.Yellow, Margin = Indent,
            };
            c.Controls.Add(trkStrength);
            chkSilent = new RetroCheck("Silent clicking", "Keeps the click but drops the vibration sound.") { Accent = Theme.Yellow, Margin = Indent };
            c.Controls.Add(chkSilent);
            rbDisable = new RetroRadio("No haptic feedback", "Turns off the haptic motor and the force touch button completely.") { Accent = Theme.Yellow };
            c.Controls.Add(rbDisable);
            rbMax = new RetroRadio("Maximum haptic feedback", "The strongest possible click. Very clicky and loud!") { Accent = Theme.Yellow };
            c.Controls.Add(rbMax);

            rbMac.CheckedChanged += (s, e) => { trkStrength.Enabled = chkSilent.Enabled = rbMac.Checked; };
            return c;
        }

        Control BuildStopCard()
        {
            var c = new Card("WHEN YOU LIFT YOUR FINGER", Theme.Blue);
            rbStopNone = new RetroRadio("Do nothing", "The pointer follows your finger until it leaves the surface.") { Accent = Theme.Blue };
            c.Controls.Add(rbStopNone);
            rbStopPressure = new RetroRadio("Freeze the pointer at low pressure",
                "Stops the pointer from jumping as you lift. 0 (no pressure) is a good value.") { Accent = Theme.Blue };
            c.Controls.Add(rbStopPressure);
            numPressure = new RetroSlider("Pressure threshold", null, 0, 255, 1, 0) { Accent = Theme.Blue, Margin = Indent, Format = v => v.ToString("0") };
            c.Controls.Add(numPressure);
            rbStopSize = new RetroRadio("Freeze the pointer at a small touch size",
                "Uses the contact area instead of pressure. 7 is a good value.") { Accent = Theme.Blue };
            c.Controls.Add(rbStopSize);
            numSize = new RetroSlider("Touch size threshold", null, 0, 255, 1, 7) { Accent = Theme.Blue, Margin = Indent, Format = v => v.ToString("0") };
            c.Controls.Add(numSize);

            EventHandler sync = (s, e) => { numPressure.Enabled = rbStopPressure.Checked; numSize.Enabled = rbStopSize.Checked; };
            rbStopPressure.CheckedChanged += sync;
            rbStopSize.CheckedChanged += sync;
            return c;
        }

        Control BuildOtherCard()
        {
            var c = new Card("OTHER OPTIONS", Theme.Lavender);
            chkNearFingers = new RetroCheck("Ignore hovering fingers", "Fingers that are near the surface but not touching it are ignored.");
            chkPalm = new RetroCheck("Palm rejection", "Ignores large contacts, such as a resting palm.");
            chkButtonFinger = new RetroCheck("Ignore the finger that presses the button", "For dragging: click with your thumb and move with your index finger.");
            chkInvert = new RetroCheck("Invert up/down and left/right", "For using the trackpad upside down.");
            chkNonAdmin = new RetroCheck("Let non-admin apps talk to the driver", "Lets Trackpad Canvas read the battery level over USB.");
            foreach (var k in new[] { chkNearFingers, chkPalm, chkButtonFinger, chkInvert, chkNonAdmin })
            {
                k.Accent = Theme.Lavender;
                c.Controls.Add(k);
            }
            return c;
        }

        void MarkDirty()
        {
            if (loading) return;
            pill.Fill = Theme.Pink;
            pill.Text = "Unsaved changes";
        }

        void ShowSaved(string text)
        {
            pill.Fill = Theme.Mint;
            pill.Text = text;
        }

        void LoadFromRegistry()
        {
            loading = true;
            bool installed = DriverControl.IsInstalled();
            content.Enabled = btnApply.Enabled = installed;
            if (installed)
            {
                lblStatus.SetLook(Theme.Yellow, "i");
                lblStatus.Text = "These settings control the Magic Trackpad 2 driver from this project. Applying them asks for administrator permission once.";
            }
            else
            {
                lblStatus.SetLook(Theme.Pink, "!");
                lblStatus.Text = "The Magic Trackpad 2 driver from this project is not installed, so these settings are unavailable. " +
                                 "The gesture features still work with any Precision Touchpad driver.";
            }

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
            loading = false;
            pill.Text = "";
        }

        DriverSettings FromUi()
        {
            var s = new DriverSettings();
            if (rbDisable.Checked) { s.ButtonDisabled = 1; s.FeedbackClick = 0; s.FeedbackRelease = 0; }
            else if (rbMax.Checked) { s.FeedbackClick = DriverSettings.MaxFeedback; s.FeedbackRelease = DriverSettings.MaxFeedback; }
            else
            {
                int strength = (int)trkStrength.Value;
                s.FeedbackClick = DriverSettings.ClickPresets[strength];
                s.FeedbackRelease = DriverSettings.ReleasePresets[strength];
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
                    ShowSaved("Applied");
                    break;
                case ExitSavedNotReloaded:
                    Log("Driver settings saved. No trackpad acknowledged the reload; they take effect when it reconnects.");
                    ShowSaved("Saved — reconnect trackpad");
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
    }
}

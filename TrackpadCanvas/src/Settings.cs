using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TrackpadCanvas
{
    // Simple key=value settings stored next to the executable.
    internal sealed class Settings
    {
        public bool Enabled = true;
        public bool OnlyInApps = true;
        public string Apps = "CLIPStudioPaint, Photoshop";
        public double PxPerMm = 6;
        public bool ReversePan = false;
        public bool ScrollElsewhere = true;
        public double ScrollSpeed = 20;
        public bool ReverseScroll = false;
        public int LastBatteryPercent = -1;
        public DateTime LastBatteryTime;

        readonly string path;

        public Settings(string path)
        {
            this.path = path;
        }

        public string[] AppList
        {
            get
            {
                return Apps.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Select(s => s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s.Substring(0, s.Length - 4) : s)
                    .Where(s => s.Length > 0)
                    .ToArray();
            }
        }

        public void Load()
        {
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0 || line.TrimStart().StartsWith("#")) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();
                bool b;
                double d;
                switch (key)
                {
                    case "enabled": if (bool.TryParse(val, out b)) Enabled = b; break;
                    case "onlyinapps": if (bool.TryParse(val, out b)) OnlyInApps = b; break;
                    case "apps": Apps = val; break;
                    case "pxpermm":
                        if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && d > 0) PxPerMm = d;
                        break;
                    case "reversepan": if (bool.TryParse(val, out b)) ReversePan = b; break;
                    case "scrollelsewhere": if (bool.TryParse(val, out b)) ScrollElsewhere = b; break;
                    case "scrollspeed":
                        if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && d > 0) ScrollSpeed = d;
                        break;
                    case "reversescroll": if (bool.TryParse(val, out b)) ReverseScroll = b; break;
                    case "lastbatterypercent":
                        int p;
                        if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out p)) LastBatteryPercent = p;
                        break;
                    case "lastbatterytime":
                        DateTime t;
                        if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t)) LastBatteryTime = t;
                        break;
                }
            }
        }

        public void Save()
        {
            var lines = new List<string>
            {
                "# Trackpad Canvas settings",
                "Enabled=" + Enabled,
                "OnlyInApps=" + OnlyInApps,
                "Apps=" + Apps,
                "PxPerMm=" + PxPerMm.ToString(CultureInfo.InvariantCulture),
                "ReversePan=" + ReversePan,
                "ScrollElsewhere=" + ScrollElsewhere,
                "ScrollSpeed=" + ScrollSpeed.ToString(CultureInfo.InvariantCulture),
                "ReverseScroll=" + ReverseScroll,
                "LastBatteryPercent=" + LastBatteryPercent.ToString(CultureInfo.InvariantCulture),
                "LastBatteryTime=" + LastBatteryTime.ToString("o", CultureInfo.InvariantCulture),
            };
            try { File.WriteAllLines(path, lines); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}

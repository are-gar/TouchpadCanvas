using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TrackpadCanvas
{
    // One finger contact, position in millimetres from the trackpad's top-left corner.
    internal struct Contact
    {
        public int Id;
        public double X;
        public double Y;
    }

    // Parses Windows Precision Touchpad HID reports (Digitizer page 0x0D, usage 0x05) into finger contacts.
    internal sealed class PtpDevice : IDisposable
    {
        const ushort PAGE_GENERIC = 0x01, PAGE_DIGITIZER = 0x0D;
        const ushort USAGE_X = 0x30, USAGE_Y = 0x31;
        const ushort USAGE_TIP = 0x42, USAGE_CONTACT_ID = 0x51, USAGE_CONTACT_COUNT = 0x54;
        const double FallbackWidthMm = 160, FallbackHeightMm = 115; // Magic Trackpad (USB-C)

        sealed class FingerSlot
        {
            public ushort Link;
            public bool HasX, HasY, HasId;
            public Native.HIDP_VALUE_CAPS X, Y;
        }

        public readonly IntPtr Handle;
        public readonly string Name;
        public double WidthMm { get; private set; }
        public double HeightMm { get; private set; }
        public string Description { get; private set; }

        IntPtr preparsed;
        readonly List<FingerSlot> slots = new List<FingerSlot>();
        bool hasContactCount;
        ushort contactCountLink;
        readonly ushort[] usageBuf = new ushort[32];

        // Hybrid-mode frame assembly (a frame may span several reports).
        readonly List<Contact> pending = new List<Contact>();
        int expected, seen;

        PtpDevice(IntPtr handle, string name)
        {
            Handle = handle;
            Name = name;
        }

        public static PtpDevice TryCreate(IntPtr hDevice, out string error)
        {
            error = null;
            uint size = 0;
            Native.GetRawInputDeviceInfo(hDevice, Native.RIDI_PREPARSEDDATA, IntPtr.Zero, ref size);
            if (size == 0) { error = "no preparsed data"; return null; }
            IntPtr pp = Marshal.AllocHGlobal((int)size);
            if (Native.GetRawInputDeviceInfo(hDevice, Native.RIDI_PREPARSEDDATA, pp, ref size) == unchecked((uint)-1))
            {
                Marshal.FreeHGlobal(pp);
                error = "GetRawInputDeviceInfo failed";
                return null;
            }

            Native.HIDP_CAPS caps;
            if (Native.HidP_GetCaps(pp, out caps) != Native.HIDP_STATUS_SUCCESS)
            {
                Marshal.FreeHGlobal(pp);
                error = "HidP_GetCaps failed";
                return null;
            }
            if (caps.UsagePage != PAGE_DIGITIZER || caps.Usage != 0x05)
            {
                Marshal.FreeHGlobal(pp);
                error = string.Format("not a precision touchpad (page 0x{0:X2} usage 0x{1:X2})", caps.UsagePage, caps.Usage);
                return null;
            }

            var dev = new PtpDevice(hDevice, GetDeviceName(hDevice));
            dev.preparsed = pp;

            ushort n = caps.NumberInputValueCaps;
            var vcaps = new Native.HIDP_VALUE_CAPS[n];
            if (n == 0 || Native.HidP_GetValueCaps(Native.HidP_Input, vcaps, ref n, pp) != Native.HIDP_STATUS_SUCCESS)
            {
                dev.Dispose();
                error = "HidP_GetValueCaps failed";
                return null;
            }

            var byLink = new Dictionary<ushort, FingerSlot>();
            for (int i = 0; i < n; i++)
            {
                var vc = vcaps[i];
                if (vc.IsRange != 0) continue;
                if (vc.UsagePage == PAGE_DIGITIZER && vc.Usage == USAGE_CONTACT_COUNT)
                {
                    dev.hasContactCount = true;
                    dev.contactCountLink = vc.LinkCollection;
                    continue;
                }
                bool isX = vc.UsagePage == PAGE_GENERIC && vc.Usage == USAGE_X;
                bool isY = vc.UsagePage == PAGE_GENERIC && vc.Usage == USAGE_Y;
                bool isId = vc.UsagePage == PAGE_DIGITIZER && vc.Usage == USAGE_CONTACT_ID;
                if (!isX && !isY && !isId) continue;
                FingerSlot s;
                if (!byLink.TryGetValue(vc.LinkCollection, out s))
                {
                    s = new FingerSlot { Link = vc.LinkCollection };
                    byLink[vc.LinkCollection] = s;
                }
                if (isX) { s.X = vc; s.HasX = true; }
                if (isY) { s.Y = vc; s.HasY = true; }
                if (isId) s.HasId = true;
            }
            foreach (var s in byLink.Values)
                if (s.HasX && s.HasY) dev.slots.Add(s);
            dev.slots.Sort((a, b) => a.Link.CompareTo(b.Link));

            if (dev.slots.Count == 0)
            {
                dev.Dispose();
                error = "no finger collections with X/Y found";
                return null;
            }

            var first = dev.slots[0];
            double w = PhysicalMm(first.X), h = PhysicalMm(first.Y);
            bool physOk = w >= 20 && w <= 500 && h >= 20 && h <= 500;
            dev.WidthMm = physOk ? w : FallbackWidthMm;
            dev.HeightMm = physOk ? h : FallbackHeightMm;

            var sb = new StringBuilder();
            sb.AppendFormat("{0} finger slots, contact count {1}, size {2:0.#} x {3:0.#} mm{4}",
                dev.slots.Count, dev.hasContactCount ? "yes" : "no", dev.WidthMm, dev.HeightMm,
                physOk ? "" : " (assumed; device reported none)");
            sb.AppendFormat("; X logical {0}..{1} phys {2}..{3} units 0x{4:X} exp {5}",
                first.X.LogicalMin, first.X.LogicalMax, first.X.PhysicalMin, first.X.PhysicalMax, first.X.Units, first.X.UnitsExp);
            sb.AppendFormat("; Y logical {0}..{1}", first.Y.LogicalMin, first.Y.LogicalMax);
            dev.Description = sb.ToString();
            return dev;
        }

        static double PhysicalMm(Native.HIDP_VALUE_CAPS c)
        {
            int span = c.PhysicalMax - c.PhysicalMin;
            if (span <= 0) return 0;
            int system = (int)(c.Units & 0xF);
            int length = (int)((c.Units >> 4) & 0xF);
            if (length != 1) return 0;
            double unitMm = system == 1 ? 10.0 : system == 3 ? 25.4 : 0;
            if (unitMm == 0) return 0;
            int code = (int)(c.UnitsExp & 0xF);
            int exp = code < 8 ? code : code - 16;
            return span * Math.Pow(10, exp) * unitMm;
        }

        static string GetDeviceName(IntPtr hDevice)
        {
            uint chars = 0;
            Native.GetRawInputDeviceInfo(hDevice, Native.RIDI_DEVICENAME, IntPtr.Zero, ref chars);
            if (chars == 0) return "(unknown)";
            IntPtr buf = Marshal.AllocHGlobal((int)chars * 2 + 2);
            try
            {
                Native.GetRawInputDeviceInfo(hDevice, Native.RIDI_DEVICENAME, buf, ref chars);
                return Marshal.PtrToStringUni(buf) ?? "(unknown)";
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        // Returns the completed frame's touching contacts, or null if the frame continues in later reports.
        public List<Contact> Parse(byte[] report)
        {
            uint len = (uint)report.Length;
            uint count = 0;
            bool countOk = hasContactCount &&
                Native.HidP_GetUsageValue(Native.HidP_Input, PAGE_DIGITIZER, contactCountLink, USAGE_CONTACT_COUNT,
                    out count, preparsed, report, len) == Native.HIDP_STATUS_SUCCESS;

            if (!countOk)
            {
                pending.Clear(); expected = slots.Count; seen = 0;
            }
            else if (count > 0)
            {
                pending.Clear(); expected = (int)count; seen = 0;
            }
            else if (expected == 0)
            {
                return new List<Contact>();
            }

            for (int i = 0; i < slots.Count && seen < expected; i++)
            {
                var s = slots[i];
                seen++;
                uint nUsages = (uint)usageBuf.Length;
                bool tip = false;
                if (Native.HidP_GetUsages(Native.HidP_Input, PAGE_DIGITIZER, s.Link, usageBuf, ref nUsages,
                        preparsed, report, len) == Native.HIDP_STATUS_SUCCESS)
                {
                    for (int u = 0; u < nUsages; u++)
                        if (usageBuf[u] == USAGE_TIP) { tip = true; break; }
                }
                if (!tip) continue;

                uint x, y, id = (uint)i;
                if (Native.HidP_GetUsageValue(Native.HidP_Input, PAGE_GENERIC, s.Link, USAGE_X, out x, preparsed, report, len) != Native.HIDP_STATUS_SUCCESS) continue;
                if (Native.HidP_GetUsageValue(Native.HidP_Input, PAGE_GENERIC, s.Link, USAGE_Y, out y, preparsed, report, len) != Native.HIDP_STATUS_SUCCESS) continue;
                if (s.HasId)
                    Native.HidP_GetUsageValue(Native.HidP_Input, PAGE_DIGITIZER, s.Link, USAGE_CONTACT_ID, out id, preparsed, report, len);

                pending.Add(new Contact
                {
                    Id = (int)id,
                    X = Normalize(x, s.X) * WidthMm,
                    Y = Normalize(y, s.Y) * HeightMm,
                });
            }

            if (seen < expected) return null;
            var frame = new List<Contact>(pending);
            pending.Clear(); expected = 0; seen = 0;
            return frame;
        }

        static double Normalize(uint raw, Native.HIDP_VALUE_CAPS c)
        {
            double span = (double)c.LogicalMax - c.LogicalMin;
            if (span <= 0) return 0;
            return ((double)raw - c.LogicalMin) / span;
        }

        public void Dispose()
        {
            if (preparsed != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(preparsed);
                preparsed = IntPtr.Zero;
            }
        }
    }
}

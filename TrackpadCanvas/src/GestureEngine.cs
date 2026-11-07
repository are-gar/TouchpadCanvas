using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TrackpadCanvas
{
    // What a two-finger gesture turns into; chosen when the fingers land, from the focused app.
    internal enum GestureMode { None, Touch, Wheel }

    // Turns two-finger trackpad frames into either
    //  - Touch: a virtual two-finger touch gesture centred on the mouse cursor (pan, zoom and rotate in art apps), or
    //  - Wheel: ordinary touchpad behaviour (wheel scrolling, or Ctrl+wheel for a pinch) for every other app.
    //
    //  Idle   --2 fingers down, mode chosen-->           Armed
    //  Armed  --fingers move past a small threshold-->  Active (touch DOWN injected, or scroll/zoom begins)
    //  Armed  --fingers lift before moving-->           Idle   (a plain two-finger tap/click, nothing injected)
    //  Active --finger count or IDs change-->           Idle   (touch UP injected / Ctrl released)
    internal sealed class GestureEngine
    {
        enum State { Idle, Armed, Active }
        enum WheelKind { Scroll, Zoom }
        enum Axis { Both, Vertical, Horizontal }

        const double StartMoveMm = 1.0;
        const double StartScale = 0.05;
        const double StartAngleRad = 4 * Math.PI / 180;
        const int RefreshMs = 60;     // re-send the last positions if the pad goes quiet
        const int TimeoutMs = 300;    // end the gesture if the pad stays quiet this long
        const double WheelStartSpreadMm = 2.0; // finger spacing change that starts a pinch in Wheel mode
        const double ZoomGain = 800;           // wheel units per unit of ln(spacing ratio); ~15% spread = 1 notch
        const double AxisLockRatio = 2.0;      // lock scrolling to one axis when it dominates this much at the start

        readonly TouchInjector injector;
        readonly WheelInjector wheel;
        State state = State.Idle;
        GestureMode mode;
        WheelKind wheelKind;
        Axis axis;
        Contact a0, b0;
        Contact aPrev, bPrev;
        Point anchor;
        int lastFrameTick, lastSendTick;

        public double PxPerMm = 6;
        public bool ReversePan;
        public double ScrollSpeed = 20; // wheel units per mm of finger movement (120 = one notch)
        public bool ReverseScroll;
        public Func<GestureMode> ChooseMode = () => GestureMode.Touch;
        public event Action<string> Log;

        public GestureEngine(TouchInjector injector, WheelInjector wheel)
        {
            this.injector = injector;
            this.wheel = wheel;
        }

        public string StateName
        {
            get
            {
                if (state == State.Idle) return "Idle";
                string m = mode == GestureMode.Wheel ? (state == State.Active ? wheelKind.ToString() : "Wheel") : "Touch";
                return state + " (" + m + ")";
            }
        }

        public void OnFrame(List<Contact> contacts)
        {
            lastFrameTick = Environment.TickCount;
            Contact a, b;
            bool two = PickTwo(contacts, out a, out b);

            switch (state)
            {
                case State.Idle:
                    if (two)
                    {
                        mode = ChooseMode();
                        if (mode == GestureMode.None) break;
                        a0 = a; b0 = b;
                        Native.POINT p;
                        Native.GetCursorPos(out p);
                        anchor = new Point(p.X, p.Y);
                        state = State.Armed;
                    }
                    break;

                case State.Armed:
                    if (!two || a.Id != a0.Id || b.Id != b0.Id)
                    {
                        state = State.Idle;
                        if (two) OnFrame(contacts);
                        break;
                    }
                    if (mode == GestureMode.Wheel)
                    {
                        ArmWheel(a, b);
                        break;
                    }
                    if (MovedEnough(a, b))
                    {
                        if (injector.Down(Map(a0, a0, b0), Map(b0, a0, b0)))
                        {
                            injector.Move(Map(a, a, b), Map(b, a, b));
                            lastSendTick = Environment.TickCount;
                            state = State.Active;
                            Emit(string.Format("gesture start at {0},{1}", anchor.X, anchor.Y));
                        }
                        else
                        {
                            state = State.Idle;
                        }
                    }
                    break;

                case State.Active:
                    if (two && a.Id == a0.Id && b.Id == b0.Id && mode == GestureMode.Wheel)
                    {
                        WheelMove(a, b);
                    }
                    else if (two && a.Id == a0.Id && b.Id == b0.Id)
                    {
                        injector.Move(Map(a, a, b), Map(b, a, b));
                        lastSendTick = Environment.TickCount;
                    }
                    else
                    {
                        End("fingers changed");
                    }
                    break;
            }
        }

        // Called from a UI timer.
        public void Tick()
        {
            int now = Environment.TickCount;
            if (state == State.Idle) return;
            if (now - lastFrameTick > TimeoutMs)
            {
                if (state == State.Active) End("pad went quiet");
                else state = State.Idle;
                return;
            }
            if (state == State.Active && mode == GestureMode.Touch && now - lastSendTick > RefreshMs)
            {
                injector.Refresh();
                lastSendTick = now;
            }
        }

        public void Cancel()
        {
            if (state == State.Active) End("cancelled");
            state = State.Idle;
        }

        void End(string why)
        {
            if (mode == GestureMode.Wheel)
            {
                wheel.End();
                state = State.Idle;
                Emit(wheelKind.ToString().ToLowerInvariant() + " end (" + why + ")");
                return;
            }
            injector.Up();
            state = State.Idle;
            Emit("gesture end (" + why + ")");
        }

        // Wheel mode: decide between scrolling and pinch-zoom once the fingers have moved a little.
        // A pinch changes the finger spacing much more than it moves their midpoint; a scroll is the opposite.
        void ArmWheel(Contact a, Contact b)
        {
            double cdx = ((a.X + b.X) - (a0.X + b0.X)) / 2;
            double cdy = ((a.Y + b.Y) - (a0.Y + b0.Y)) / 2;
            double move = Math.Sqrt(cdx * cdx + cdy * cdy);
            double spread = Math.Abs(Dist(a, b) - Dist(a0, b0));
            if (move <= StartMoveMm && spread <= WheelStartSpreadMm) return;

            wheelKind = spread > move ? WheelKind.Zoom : WheelKind.Scroll;
            axis = Axis.Both;
            if (wheelKind == WheelKind.Scroll)
            {
                if (Math.Abs(cdy) > AxisLockRatio * Math.Abs(cdx)) axis = Axis.Vertical;
                else if (Math.Abs(cdx) > AxisLockRatio * Math.Abs(cdy)) axis = Axis.Horizontal;
            }
            wheel.Begin();
            aPrev = a; bPrev = b;
            state = State.Active;
            Emit(wheelKind == WheelKind.Zoom ? "zoom start" : "scroll start (" + axis.ToString().ToLowerInvariant() + ")");
        }

        void WheelMove(Contact a, Contact b)
        {
            if (wheelKind == WheelKind.Zoom)
            {
                double d0 = Dist(aPrev, bPrev), d1 = Dist(a, b);
                if (d0 > 1 && d1 > 1) wheel.Zoom(Math.Log(d1 / d0) * ZoomGain);
            }
            else
            {
                // Pad Y grows downward. "Natural" scrolling, like Windows' default: content follows the fingers,
                // so fingers moving down scroll up (positive wheel) and fingers moving right scroll left.
                double dx = ((a.X + b.X) - (aPrev.X + bPrev.X)) / 2;
                double dy = ((a.Y + b.Y) - (aPrev.Y + bPrev.Y)) / 2;
                double sign = ReverseScroll ? -1 : 1;
                double v = axis == Axis.Horizontal ? 0 : sign * dy * ScrollSpeed;
                double h = axis == Axis.Vertical ? 0 : -sign * dx * ScrollSpeed;
                wheel.Scroll(v, h);
            }
            aPrev = a; bPrev = b;
        }

        static bool PickTwo(List<Contact> contacts, out Contact a, out Contact b)
        {
            a = default(Contact);
            b = default(Contact);
            if (contacts.Count != 2) return false;
            if (contacts[0].Id <= contacts[1].Id) { a = contacts[0]; b = contacts[1]; }
            else { a = contacts[1]; b = contacts[0]; }
            return true;
        }

        bool MovedEnough(Contact a, Contact b)
        {
            double cdx = ((a.X + b.X) - (a0.X + b0.X)) / 2;
            double cdy = ((a.Y + b.Y) - (a0.Y + b0.Y)) / 2;
            if (Math.Sqrt(cdx * cdx + cdy * cdy) > StartMoveMm) return true;

            double d0 = Dist(a0, b0), d1 = Dist(a, b);
            if (d0 > 1 && Math.Abs(d1 / d0 - 1) > StartScale) return true;

            double ang = Math.Atan2(b.Y - a.Y, b.X - a.X) - Math.Atan2(b0.Y - a0.Y, b0.X - a0.X);
            while (ang > Math.PI) ang -= 2 * Math.PI;
            while (ang < -Math.PI) ang += 2 * Math.PI;
            return Math.Abs(ang) > StartAngleRad;
        }

        static double Dist(Contact p, Contact q)
        {
            double dx = p.X - q.X, dy = p.Y - q.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // Maps finger c (with current pair a,b) to screen pixels around the anchor.
        // Finger spacing and angle are preserved exactly; ReversePan flips only the shared movement.
        Point Map(Contact c, Contact a, Contact b)
        {
            double cx0 = (a0.X + b0.X) / 2, cy0 = (a0.Y + b0.Y) / 2;
            double dx, dy;
            if (!ReversePan)
            {
                dx = c.X - cx0;
                dy = c.Y - cy0;
            }
            else
            {
                double cx = (a.X + b.X) / 2, cy = (a.Y + b.Y) / 2;
                dx = (c.X - cx) - (cx - cx0);
                dy = (c.Y - cy) - (cy - cy0);
            }
            Rectangle vs = SystemInformation.VirtualScreen;
            int x = anchor.X + (int)Math.Round(dx * PxPerMm);
            int y = anchor.Y + (int)Math.Round(dy * PxPerMm);
            x = Math.Max(vs.Left, Math.Min(vs.Right - 1, x));
            y = Math.Max(vs.Top, Math.Min(vs.Bottom - 1, y));
            return new Point(x, y);
        }

        void Emit(string msg)
        {
            var h = Log;
            if (h != null) h(msg);
        }
    }
}

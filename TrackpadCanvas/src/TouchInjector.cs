using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TrackpadCanvas
{
    // Drives a virtual two-finger touchscreen through InjectTouchInput.
    internal sealed class TouchInjector
    {
        bool initialized, initTried;
        bool down;
        Point lastA, lastB;
        bool reportedFailure;
        public event Action<string> Log;

        public bool IsDown { get { return down; } }

        public bool Initialize()
        {
            if (initTried) return initialized;
            initTried = true;
            initialized = Native.InitializeTouchInjection(2, Native.TOUCH_FEEDBACK_NONE);
            if (!initialized) Emit("InitializeTouchInjection failed, error " + Marshal.GetLastWin32Error());
            return initialized;
        }

        public bool Down(Point a, Point b)
        {
            if (!Initialize()) return false;
            reportedFailure = false;
            down = Send(a, b, Native.POINTER_FLAG_DOWN | Native.POINTER_FLAG_INRANGE | Native.POINTER_FLAG_INCONTACT);
            return down;
        }

        public void Move(Point a, Point b)
        {
            if (!down) return;
            Send(a, b, Native.POINTER_FLAG_UPDATE | Native.POINTER_FLAG_INRANGE | Native.POINTER_FLAG_INCONTACT);
        }

        public void Refresh()
        {
            Move(lastA, lastB);
        }

        public void Up()
        {
            if (!down) return;
            Send(lastA, lastB, Native.POINTER_FLAG_UP);
            down = false;
        }

        bool Send(Point a, Point b, uint flags)
        {
            if (a == b) b.X += 1;
            var contacts = new[] { Make(0, a, flags), Make(1, b, flags) };
            bool ok = Native.InjectTouchInput(2, contacts);
            if (ok)
            {
                lastA = a;
                lastB = b;
            }
            else if (!reportedFailure)
            {
                reportedFailure = true;
                Emit(string.Format("InjectTouchInput failed (flags 0x{0:X}), error {1}", flags, Marshal.GetLastWin32Error()));
            }
            return ok;
        }

        static Native.POINTER_TOUCH_INFO Make(uint id, Point p, uint flags)
        {
            var t = new Native.POINTER_TOUCH_INFO();
            t.pointerInfo.pointerType = Native.PT_TOUCH;
            t.pointerInfo.pointerId = id;
            t.pointerInfo.pointerFlags = flags;
            t.pointerInfo.ptPixelLocation.X = p.X;
            t.pointerInfo.ptPixelLocation.Y = p.Y;
            t.touchMask = Native.TOUCH_MASK_CONTACTAREA | Native.TOUCH_MASK_ORIENTATION | Native.TOUCH_MASK_PRESSURE;
            t.rcContact.Left = p.X - 4;
            t.rcContact.Right = p.X + 4;
            t.rcContact.Top = p.Y - 4;
            t.rcContact.Bottom = p.Y + 4;
            t.orientation = 90;
            t.pressure = 32000;
            return t;
        }

        void Emit(string msg)
        {
            var h = Log;
            if (h != null) h(msg);
        }
    }
}

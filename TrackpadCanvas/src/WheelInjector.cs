using System;
using System.Runtime.InteropServices;

namespace TrackpadCanvas
{
    // Sends mouse-wheel input the way Windows' own touchpad scrolling does:
    // small (fractional-notch) wheel deltas for scrolling, and Ctrl + wheel for pinch-zoom.
    // Wheel messages go to the window under the cursor.
    internal sealed class WheelInjector
    {
        public const int Notch = 120; // WHEEL_DELTA

        double vRemainder, hRemainder, zoomRemainder;
        bool ctrlDown;
        bool reportedFailure;
        public event Action<string> Log;

        public void Begin()
        {
            vRemainder = hRemainder = zoomRemainder = 0;
            reportedFailure = false;
        }

        // Positive vertical = scroll up (content moves down); positive horizontal = scroll right.
        public void Scroll(double vertical, double horizontal)
        {
            vRemainder += vertical;
            hRemainder += horizontal;
            int v = (int)vRemainder, h = (int)hRemainder; // truncates toward zero
            vRemainder -= v;
            hRemainder -= h;
            if (v != 0) SendWheel(Native.MOUSEEVENTF_WHEEL, v);
            if (h != 0) SendWheel(Native.MOUSEEVENTF_HWHEEL, h);
        }

        // Positive = zoom in. Sent in whole notches only: many apps treat any Ctrl+wheel message as
        // one zoom step, so fractional deltas would zoom far too fast in them.
        public void Zoom(double amount)
        {
            zoomRemainder += amount;
            int notches = (int)(zoomRemainder / Notch);
            if (notches == 0) return;
            zoomRemainder -= notches * Notch;
            if (!ctrlDown)
            {
                // Respect a Ctrl the user is physically holding; only press it if it isn't already down.
                if ((Native.GetAsyncKeyState(Native.VK_CONTROL) & 0x8000) == 0)
                {
                    SendKey(Native.VK_CONTROL, false);
                    ctrlDown = true;
                }
            }
            int step = notches > 0 ? Notch : -Notch;
            for (int i = 0; i < Math.Abs(notches); i++) SendWheel(Native.MOUSEEVENTF_WHEEL, step);
        }

        public void End()
        {
            if (ctrlDown)
            {
                SendKey(Native.VK_CONTROL, true);
                ctrlDown = false;
            }
        }

        void SendWheel(uint flag, int delta)
        {
            var input = new Native.INPUT { type = Native.INPUT_MOUSE };
            input.u.mi.dwFlags = flag;
            input.u.mi.mouseData = delta;
            Send(input);
        }

        void SendKey(ushort vk, bool up)
        {
            var input = new Native.INPUT { type = Native.INPUT_KEYBOARD };
            input.u.ki.wVk = vk;
            input.u.ki.dwFlags = up ? Native.KEYEVENTF_KEYUP : 0;
            Send(input);
        }

        void Send(Native.INPUT input)
        {
            uint sent = Native.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Native.INPUT)));
            if (sent == 0 && !reportedFailure)
            {
                reportedFailure = true;
                var h = Log;
                if (h != null) h("SendInput failed, error " + Marshal.GetLastWin32Error() +
                    " (the window under the cursor may be running as administrator)");
            }
        }
    }
}

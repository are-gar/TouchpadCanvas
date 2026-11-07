using System;
using System.Runtime.InteropServices;

namespace TrackpadCanvas
{
    // Win32 declarations: Raw Input, HID parsing, touch injection, misc.
    internal static class Native
    {
        // ---- Raw Input ----
        public const int WM_INPUT = 0x00FF;
        public const int WM_INPUT_DEVICE_CHANGE = 0x00FE;
        public const int GIDC_REMOVAL = 2;
        public const uint RIDEV_INPUTSINK = 0x00000100;
        public const uint RIDEV_DEVNOTIFY = 0x00002000;
        public const uint RID_INPUT = 0x10000003;
        public const uint RIDI_PREPARSEDDATA = 0x20000005;
        public const uint RIDI_DEVICENAME = 0x20000007;
        public const uint RIM_TYPEHID = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICELIST
        {
            public IntPtr hDevice;
            public uint dwType;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] devices, uint numDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "GetRawInputDeviceInfoW")]
        public static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputDeviceList([Out] RAWINPUTDEVICELIST[] list, ref uint numDevices, uint cbSize);

        // ---- HID parsing (hid.dll) ----
        public const int HIDP_STATUS_SUCCESS = 0x00110000;
        public const int HidP_Input = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        // 72 bytes. The trailing union is read as its NotRange form (Usage = UsageMin when IsRange).
        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_VALUE_CAPS
        {
            public ushort UsagePage;
            public byte ReportID;
            public byte IsAlias;
            public ushort BitField;
            public ushort LinkCollection;
            public ushort LinkUsage;
            public ushort LinkUsagePage;
            public byte IsRange;
            public byte IsStringRange;
            public byte IsDesignatorRange;
            public byte IsAbsolute;
            public byte HasNull;
            public byte Reserved;
            public ushort BitSize;
            public ushort ReportCount;
            public ushort Reserved2a, Reserved2b, Reserved2c, Reserved2d, Reserved2e;
            public uint UnitsExp;
            public uint Units;
            public int LogicalMin;
            public int LogicalMax;
            public int PhysicalMin;
            public int PhysicalMax;
            public ushort Usage;
            public ushort UsageMax;
            public ushort StringIndex;
            public ushort Reserved3;
            public ushort DesignatorIndex;
            public ushort Reserved4;
            public ushort DataIndex;
            public ushort Reserved5;
        }

        [DllImport("hid.dll")]
        public static extern bool HidD_GetPreparsedData(IntPtr hidDevice, out IntPtr preparsedData);

        [DllImport("hid.dll")]
        public static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        public static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS caps);

        [DllImport("hid.dll")]
        public static extern int HidP_GetValueCaps(int reportType, [In, Out] HIDP_VALUE_CAPS[] valueCaps, ref ushort valueCapsLength, IntPtr preparsedData);

        [DllImport("hid.dll")]
        public static extern int HidP_GetUsageValue(int reportType, ushort usagePage, ushort linkCollection, ushort usage,
            out uint usageValue, IntPtr preparsedData, byte[] report, uint reportLength);

        [DllImport("hid.dll")]
        public static extern int HidP_GetUsages(int reportType, ushort usagePage, ushort linkCollection,
            [In, Out] ushort[] usageList, ref uint usageLength, IntPtr preparsedData, byte[] report, uint reportLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetInputReport(IntPtr hidDevice, byte[] reportBuffer, int reportBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr security,
            uint creationDisposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr handle);

        public const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ_WRITE = 3, OPEN_EXISTING = 3;
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        // ---- Touch injection ----
        public const uint PT_TOUCH = 2;
        public const uint POINTER_FLAG_INRANGE = 0x00000002;
        public const uint POINTER_FLAG_INCONTACT = 0x00000004;
        public const uint POINTER_FLAG_DOWN = 0x00010000;
        public const uint POINTER_FLAG_UPDATE = 0x00020000;
        public const uint POINTER_FLAG_UP = 0x00040000;
        public const uint TOUCH_MASK_CONTACTAREA = 0x1;
        public const uint TOUCH_MASK_ORIENTATION = 0x2;
        public const uint TOUCH_MASK_PRESSURE = 0x4;
        public const uint TOUCH_FEEDBACK_NONE = 3;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINTER_INFO
        {
            public uint pointerType;
            public uint pointerId;
            public uint frameId;
            public uint pointerFlags;
            public IntPtr sourceDevice;
            public IntPtr hwndTarget;
            public POINT ptPixelLocation;
            public POINT ptHimetricLocation;
            public POINT ptPixelLocationRaw;
            public POINT ptHimetricLocationRaw;
            public uint dwTime;
            public uint historyCount;
            public int InputData;
            public uint dwKeyStates;
            public ulong PerformanceCount;
            public int ButtonChangeType;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINTER_TOUCH_INFO
        {
            public POINTER_INFO pointerInfo;
            public uint touchFlags;
            public uint touchMask;
            public RECT rcContact;
            public RECT rcContactRaw;
            public uint orientation;
            public uint pressure;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool InitializeTouchInjection(uint maxCount, uint dwMode);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool InjectTouchInput(uint count, [In] POINTER_TOUCH_INFO[] contacts);

        // ---- SendInput (mouse wheel and keyboard) ----
        public const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
        public const uint MOUSEEVENTF_WHEEL = 0x0800, MOUSEEVENTF_HWHEEL = 0x01000;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const ushort VK_CONTROL = 0x11;

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint count, [In] INPUT[] inputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        // ---- Misc ----
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT pt);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);
    }
}

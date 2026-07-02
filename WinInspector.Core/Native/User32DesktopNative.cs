using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace WinInspector.Core.Native;

internal static class User32DesktopNative
{
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    internal const int GwlStyle = -16;
    internal const int GwlExStyle = -20;
    internal const uint GaRoot = 2;
    internal const uint WmGetText = 0x000D;
    internal const uint WmGetTextLength = 0x000E;
    internal const uint WmSetText = 0x000C;
    internal const uint BmClick = 0x00F5;
    internal const uint CbGetCurSel = 0x0147;
    internal const uint CbGetCount = 0x0146;
    internal const uint CbGetLbText = 0x0148;
    internal const uint CbGetLbTextLen = 0x0149;
    internal const uint CbSetCurSel = 0x014E;
    internal const uint CbSelectString = 0x014D;
    internal const uint LbGetCurSel = 0x0188;
    internal const uint LbGetCount = 0x018B;
    internal const uint LbGetText = 0x0189;
    internal const uint LbGetTextLen = 0x018A;
    internal const uint LbSetCurSel = 0x0186;
    internal const uint LbSelectString = 0x018C;
    internal const uint TcmFirst = 0x1300;
    internal const uint TcmGetItemCount = TcmFirst + 4;
    internal const uint TcmGetCurSel = TcmFirst + 11;
    internal const uint TcmSetCurSel = TcmFirst + 12;
    internal const uint LvmFirst = 0x1000;
    internal const uint LvmGetItemCount = LvmFirst + 4;
    internal const uint LvmGetItemRect = LvmFirst + 14;
    internal const uint LvmEnsureVisible = LvmFirst + 19;
    internal const uint LvmSetItemState = LvmFirst + 43;
    internal const uint LvmGetItemTextW = LvmFirst + 115;
    internal const uint TvmFirst = 0x1100;
    internal const uint TvmGetItemRect = TvmFirst + 4;
    internal const uint TvmGetCount = TvmFirst + 5;
    internal const uint TvmGetNextItem = TvmFirst + 10;
    internal const uint TvmSelectItem = TvmFirst + 11;
    internal const uint TvmEnsureVisible = TvmFirst + 20;
    internal const uint TvmGetItemW = TvmFirst + 62;
    internal const uint SmtoAbortIfHung = 0x0002;
    internal const int LvisFocused = 0x0001;
    internal const int LvisSelected = 0x0002;
    internal const int LvirBounds = 0x0000;
    internal const int TvgnRoot = 0x0;
    internal const int TvgnNext = 0x1;
    internal const int TvgnChild = 0x4;
    internal const int TvgnCaret = 0x9;

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint
    {
        public int X;
        public int Y;

        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowEnabled(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ScreenToClient(IntPtr hwnd, ref NativePoint point);

    [DllImport("user32.dll")]
    internal static extern int GetDlgCtrlID(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    internal static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    internal static extern IntPtr RealChildWindowFromPoint(IntPtr hwndParent, NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetFocus(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, string lParam);

    [DllImport("user32.dll")]
    internal static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SendMessageTimeout(
        IntPtr hwnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result);

    internal static long GetWindowLongPtr(IntPtr hwnd, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index).ToInt64() : GetWindowLong32(hwnd, index);
}

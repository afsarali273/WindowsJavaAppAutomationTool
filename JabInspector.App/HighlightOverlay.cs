using System.Runtime.InteropServices;
using System.Windows.Threading;
using JabInspector.Core.Models;

namespace JabInspector.App;

/// <summary>
/// Native physical-pixel overlay. Four no-activate border windows avoid WPF's
/// coordinate virtualization on mixed-DPI, negative-origin monitor layouts.
/// </summary>
public static class HighlightOverlay
{
    private const string ClassName = "JabInspectorHighlightBorder";
    private const uint WsPopup = 0x80000000;
    private const uint WsExTopmost = 0x00000008;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExNoActivate = 0x08000000;
    private const uint LwaAlpha = 0x00000002;
    private const int SwShowNoActivate = 4;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);
    private static readonly WindowProc Proc = WindowProcedure;
    private static readonly IntPtr BackgroundBrush = CreateSolidBrush(0x00303BFF); // #FF3B30
    private static bool _registered;
    private static readonly List<IntPtr> TransientWindows = [];
    private static DispatcherTimer? _transientTimer;
    private static readonly List<IntPtr> PersistentWindows = [];
    private static ElementBounds? _persistentBounds;

    public static void Show(ElementBounds bounds, TimeSpan? duration = null)
    {
        ClearTransient();
        EnsureWindowClass();
        var previousContext = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        try
        {
            TransientWindows.AddRange(CreateBorders(bounds));
        }
        finally
        {
            if (previousContext != IntPtr.Zero) SetThreadDpiAwarenessContext(previousContext);
        }

        _transientTimer = new DispatcherTimer { Interval = duration ?? TimeSpan.FromSeconds(2) };
        _transientTimer.Tick += (_, _) =>
        {
            ClearTransient();
        };
        _transientTimer.Start();
    }

    public static void ShowPersistent(ElementBounds bounds)
    {
        ClearTransient();
        if (_persistentBounds == bounds) return;
        HidePersistent();
        EnsureWindowClass();
        var previousContext = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        try { PersistentWindows.AddRange(CreateBorders(bounds)); }
        finally { if (previousContext != IntPtr.Zero) SetThreadDpiAwarenessContext(previousContext); }
        _persistentBounds = bounds;
    }

    public static void HidePersistent()
    {
        foreach (var hwnd in PersistentWindows) if (hwnd != IntPtr.Zero) DestroyWindow(hwnd);
        PersistentWindows.Clear(); _persistentBounds = null;
    }

    public static void HideTransient() => ClearTransient();

    public static void ClearAll()
    {
        ClearTransient();
        HidePersistent();
    }

    private static void ClearTransient()
    {
        _transientTimer?.Stop();
        _transientTimer = null;
        foreach (var hwnd in TransientWindows) if (hwnd != IntPtr.Zero) DestroyWindow(hwnd);
        TransientWindows.Clear();
    }

    private static IEnumerable<IntPtr> CreateBorders(ElementBounds bounds)
    {
        const int thickness = 6;
        yield return CreateBorder(bounds.X, bounds.Y, bounds.Width, thickness);
        yield return CreateBorder(bounds.X, bounds.Y + Math.Max(0, bounds.Height - thickness), bounds.Width, thickness);
        yield return CreateBorder(bounds.X, bounds.Y, thickness, bounds.Height);
        yield return CreateBorder(bounds.X + Math.Max(0, bounds.Width - thickness), bounds.Y, thickness, bounds.Height);
    }

    private static IntPtr CreateBorder(int x, int y, int width, int height)
    {
        var hwnd = CreateWindowEx(WsExTopmost | WsExTransparent | WsExToolWindow | WsExLayered | WsExNoActivate,
            ClassName, "", WsPopup, x, y, Math.Max(width, 1), Math.Max(height, 1), IntPtr.Zero, IntPtr.Zero,
            GetModuleHandle(null), IntPtr.Zero);
        if (hwnd == IntPtr.Zero) return IntPtr.Zero;
        SetLayeredWindowAttributes(hwnd, 0, 235, LwaAlpha);
        SetWindowPos(hwnd, HwndTopmost, x, y, Math.Max(width, 1), Math.Max(height, 1), 0x0010 | 0x0040);
        ShowWindow(hwnd, SwShowNoActivate);
        return hwnd;
    }

    private static void EnsureWindowClass()
    {
        if (_registered) return;
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            WindowProcedure = Proc,
            Instance = GetModuleHandle(null),
            Background = BackgroundBrush,
            ClassName = ClassName
        };
        _registered = RegisterClassEx(ref windowClass) != 0;
        if (!_registered) throw new InvalidOperationException("Could not register the native highlight window class.");
    }

    private static IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam) =>
        DefWindowProc(hwnd, message, wParam, lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size, Style;
        public WindowProc WindowProcedure;
        public int ClassExtra, WindowExtra;
        public IntPtr Instance, Icon, Cursor, Background;
        [MarshalAs(UnmanagedType.LPWStr)] public string? MenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string ClassName;
        public IntPtr SmallIcon;
    }

    private delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref WindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? moduleName);
}

using System.Runtime.InteropServices;
using Accessibility;

namespace WinInspector.Core.Native;

internal static class OleAccNative
{
    internal const uint ObjidWindow = 0x00000000;
    internal const uint ObjidClient = 0xFFFFFFFC;
    internal const int ChildidSelf = 0;
    internal const int SelFlagTakeFocus = 0x1;

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(
        IntPtr hwnd,
        uint dwId,
        ref Guid riid,
        [In, Out, MarshalAs(UnmanagedType.Interface)] ref IAccessible? ppvObject);

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromPoint(
        User32DesktopNative.NativePoint pt,
        [In, Out, MarshalAs(UnmanagedType.Interface)] ref IAccessible? ppacc,
        [In, Out] ref object? pvarChild);

    internal static bool TryAccessibleObjectFromWindow(IntPtr hwnd, uint objectId, out IAccessible? accessible)
    {
        accessible = null;
        var iid = typeof(IAccessible).GUID;
        return AccessibleObjectFromWindow(hwnd, objectId, ref iid, ref accessible) >= 0 && accessible is not null;
    }

    internal static bool TryAccessibleObjectFromPoint(User32DesktopNative.NativePoint point, out IAccessible? accessible, out object? child)
    {
        accessible = null;
        child = OleAccNative.ChildidSelf;
        return AccessibleObjectFromPoint(point, ref accessible, ref child) >= 0 && accessible is not null;
    }
}

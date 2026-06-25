using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;

namespace JabInspector.Native;

public static class AccessBridgeNative
{
    public const string DllName = "WindowsAccessBridge-64.dll";

    static AccessBridgeNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(AccessBridgeNative).Assembly, ResolveLibrary);
    }

    private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, DllName, StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero;
        var bridgePath = NativeEnvironment.FindAccessBridgeDll();
        return bridgePath is null ? IntPtr.Zero : NativeLibrary.Load(bridgePath);
    }

    // initializeAccessBridge/shutdownAccessBridge are helper functions from
    // AccessBridgeCalls.c and are not exported by WindowsAccessBridge-64.dll.
    // Direct consumers initialize the bridge through its Windows_run export.
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Windows_run")]
    public static extern void WindowsRun();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool isJavaWindow(IntPtr hwnd);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool isSameObject(int vmId, long first, long second);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool getAccessibleContextFromHWND(IntPtr hwnd, out int vmId, out long context);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool getAccessibleContextInfo(int vmId, long context, out AccessibleContextInfo info);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long getAccessibleChildFromContext(int vmId, long context, int index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long getAccessibleParentFromContext(int vmId, long context);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool getAccessibleContextAt(int vmId, long parentContext, int x, int y, out long context);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void releaseJavaObject(int vmId, long javaObject);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool getAccessibleActions(int vmId, long context, ref AccessibleActions actions);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool doAccessibleActions(int vmId, long context, ref AccessibleActionsToDo actions, out int failure);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool requestFocus(int vmId, long context);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool setTextContents(int vmId, long context, string text);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool getAccessibleTextInfo(int vmId, long context, out AccessibleTextInfo info, int x, int y);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool getAccessibleTextItems(int vmId, long context, out AccessibleTextItemsInfo textItems, int index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool getAccessibleTextSelectionInfo(int vmId, long context, out AccessibleTextSelectionInfo textSelection);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool getAccessibleTextRange(int vmId, long context, int start, int end, StringBuilder text, short length);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool getCurrentAccessibleValueFromContext(int vmId, long context, StringBuilder value, short length);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool getMaximumAccessibleValueFromContext(int vmId, long context, StringBuilder value, short length);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool getMinimumAccessibleValueFromContext(int vmId, long context, StringBuilder value, short length);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int getAccessibleSelectionCountFromContext(int vmId, long context);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long getAccessibleSelectionFromContext(int vmId, long context, int index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long getActiveDescendent(int vmId, long context);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool getVirtualAccessibleName(int vmId, long context, StringBuilder name, int length);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int getObjectDepth(int vmId, long context);
}

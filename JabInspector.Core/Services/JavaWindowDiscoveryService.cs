using System.Text;
using JabInspector.Core.Diagnostics;
using JabInspector.Core.Models;
using JabInspector.Native;

namespace JabInspector.Core.Services;

public sealed class JavaWindowDiscoveryService(AccessBridgeService bridge, InspectorLogger logger)
{
    public IReadOnlyList<JavaWindowInfo> GetJavaWindows()
    {
        var result = new List<JavaWindowInfo>();
        var scanned = 0;
        if (!bridge.Initialize()) return result;
        User32Native.EnumWindows((hwnd, _) =>
        {
            if (!User32Native.IsWindowVisible(hwnd)) return true;
            scanned++;
            var title = new StringBuilder(512); User32Native.GetWindowText(hwnd, title, title.Capacity);
            if (!bridge.IsJavaWindow(hwnd)) return true;
            bridge.TryGetAccessibleContextFromHwnd(hwnd, out var vmId, out var root);
            var className = new StringBuilder(256); User32Native.GetClassName(hwnd, className, className.Capacity);
            User32Native.GetWindowThreadProcessId(hwnd, out var pid);
            result.Add(new JavaWindowInfo { Hwnd = hwnd, Title = string.IsNullOrWhiteSpace(title.ToString()) ? "Untitled Java window" : title.ToString(), VmId = vmId, RootContext = root, ClassName = className.ToString(), ProcessId = (int)pid });
            return true;
        }, IntPtr.Zero);
        logger.Log($"Scanned {scanned} visible windows; found {result.Count} Java window(s).");
        if (result.Count == 0) logger.Log("No Java windows detected. Ensure the target app is running, Java Access Bridge is enabled, and the app uses Swing/AWT.");
        return result;
    }
}

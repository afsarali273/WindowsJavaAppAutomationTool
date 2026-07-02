using System.Diagnostics;
using System.Text;
using WinInspector.Core.Models;
using WinInspector.Core.Native;
using WinInspector.Core.Services.ActiveX;

namespace WinInspector.Core.Services;

public sealed class WindowsWindowDiscoveryService
{
    private readonly WindowsPrivilegeService _privilegeService = new();
    private readonly ActiveXModuleInspector _activeXModuleInspector = new();

    public IReadOnlyList<DesktopWindowInfo> GetTopLevelWindows()
    {
        var windows = new List<DesktopWindowInfo>();
        User32DesktopNative.EnumWindows((hwnd, _) =>
        {
            if (!User32DesktopNative.IsWindowVisible(hwnd)) return true;
            var title = ReadWindowText(hwnd);
            var className = ReadClassName(hwnd);
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(className)) return true;
            User32DesktopNative.GetWindowThreadProcessId(hwnd, out var processId);
            User32DesktopNative.GetWindowRect(hwnd, out var rect);
            var processName = TryGetProcessName(processId);
            var moduleInspection = _activeXModuleInspector.Inspect(processId);

            windows.Add(new DesktopWindowInfo
            {
                Hwnd = hwnd,
                Title = title,
                ClassName = className,
                ProcessId = processId,
                ProcessName = processName,
                Bounds = rect.ToRectangle(),
                IsVisible = true,
                ApplicationKind = WindowsTechnologyClassifier.Classify(className, processName, title),
                IsElevated = _privilegeService.TryIsProcessElevated(processId, out var isElevated) && isElevated,
                HasLegacyModules = moduleInspection.HasLegacyModules,
                HasOcxModules = moduleInspection.HasOcxModules,
                LegacyModuleSummary = moduleInspection.Summary,
                KnownLegacyModules = moduleInspection.KnownLegacyModules
            });
            return true;
        }, IntPtr.Zero);

        return windows
            .OrderByDescending(x => x.ApplicationKind == WindowsApplicationKind.JavaHosted)
            .ThenBy(x => x.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ReadWindowText(IntPtr hwnd)
    {
        var builder = new StringBuilder(512);
        User32DesktopNative.GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string ReadClassName(IntPtr hwnd)
    {
        var builder = new StringBuilder(256);
        User32DesktopNative.GetClassName(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string TryGetProcessName(uint processId)
    {
        try { return Process.GetProcessById((int)processId).ProcessName; }
        catch { return "(unknown)"; }
    }
}

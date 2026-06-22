using WinInspector.Core.Models;

namespace WinInspector.Core.Services;

public static class WindowsTechnologyClassifier
{
    public static WindowsApplicationKind Classify(string className, string processName, string title)
    {
        if (className.StartsWith("SunAwt", StringComparison.OrdinalIgnoreCase) ||
            processName.Contains("java", StringComparison.OrdinalIgnoreCase) ||
            processName.Contains("freeplane", StringComparison.OrdinalIgnoreCase))
            return WindowsApplicationKind.JavaHosted;

        if (processName.Contains("pos", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("pos", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("point of sale", StringComparison.OrdinalIgnoreCase))
            return WindowsApplicationKind.PosShell;

        if (className.StartsWith("Windows.UI", StringComparison.OrdinalIgnoreCase) ||
            className.StartsWith("ApplicationFrameWindow", StringComparison.OrdinalIgnoreCase))
            return WindowsApplicationKind.MixedDesktop;

        return WindowsApplicationKind.NativeWin32;
    }
}

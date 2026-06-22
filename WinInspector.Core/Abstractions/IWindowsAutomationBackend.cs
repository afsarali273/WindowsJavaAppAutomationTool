using WinInspector.Core.Models;

namespace WinInspector.Core.Abstractions;

public interface IWindowsAutomationBackend
{
    WindowsAutomationBackendKind Kind { get; }
    string DisplayName { get; }
    bool IsAvailable();
    bool CanInspect(DesktopWindowInfo window);
    WindowsAutomationResult InspectWindow(DesktopWindowInfo window, int maxDepth = 4, int maxChildren = 200);
}

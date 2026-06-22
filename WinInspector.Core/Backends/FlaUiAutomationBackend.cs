using WinInspector.Core.Abstractions;
using WinInspector.Core.Models;

namespace WinInspector.Core.Backends;

public sealed class FlaUiAutomationBackend : IWindowsAutomationBackend
{
    public WindowsAutomationBackendKind Kind => WindowsAutomationBackendKind.FlaUi;
    public string DisplayName => "FlaUI Adapter";

    public bool IsAvailable() => false;

    public bool CanInspect(DesktopWindowInfo window) => false;

    public WindowsAutomationResult InspectWindow(DesktopWindowInfo window, int maxDepth = 4, int maxChildren = 200) =>
        WindowsAutomationResult.Failure(Kind, "FlaUI adapter is scaffolded but not linked yet. Integrate the package when you are ready to use it in the Windows inspector.");
}

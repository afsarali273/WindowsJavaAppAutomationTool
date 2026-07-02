using WinInspector.Core.Abstractions;
using WinInspector.Core.Models;
using WinInspector.Core.Services;

namespace WinInspector.Core.Backends;

public sealed class Win32AutomationBackend : IWindowsAutomationBackend
{
    private readonly Win32Scanner _scanner;

    public Win32AutomationBackend()
        : this(new Win32Scanner())
    {
    }

    public Win32AutomationBackend(Win32Scanner scanner)
    {
        _scanner = scanner;
    }

    public WindowsAutomationBackendKind Kind => WindowsAutomationBackendKind.Win32;
    public string DisplayName => "Raw Win32";

    public bool IsAvailable() => true;

    public bool CanInspect(DesktopWindowInfo window) => true;

    public WindowsAutomationResult InspectWindow(DesktopWindowInfo window, int maxDepth = 4, int maxChildren = 200)
    {
        try
        {
            var root = _scanner.InspectWindow(window, maxDepth, maxChildren);
            return WindowsAutomationResult.Success(Kind, root);
        }
        catch (Exception ex)
        {
            return WindowsAutomationResult.Failure(Kind, ex.Message);
        }
    }
}

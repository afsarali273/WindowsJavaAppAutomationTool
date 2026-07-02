using WinInspector.Core.Abstractions;
using WinInspector.Core.Models;
using WinInspector.Core.Services;

namespace WinInspector.Core.Backends;

public sealed class MsaaAutomationBackend : IWindowsAutomationBackend
{
    private readonly MsaaScanner _scanner;

    public MsaaAutomationBackend()
        : this(new MsaaScanner())
    {
    }

    public MsaaAutomationBackend(MsaaScanner scanner)
    {
        _scanner = scanner;
    }

    public WindowsAutomationBackendKind Kind => WindowsAutomationBackendKind.Msaa;
    public string DisplayName => "Microsoft Active Accessibility";

    public bool IsAvailable() => true;

    public bool CanInspect(DesktopWindowInfo window) =>
        window.ApplicationKind is WindowsApplicationKind.NativeWin32 or WindowsApplicationKind.MixedDesktop or WindowsApplicationKind.PosShell;

    public WindowsAutomationResult InspectWindow(DesktopWindowInfo window, int maxDepth = 4, int maxChildren = 200)
    {
        try
        {
            var root = _scanner.InspectWindow(window, maxDepth, maxChildren);
            return root is null
                ? WindowsAutomationResult.Failure(Kind, "MSAA could not resolve the window handle.")
                : WindowsAutomationResult.Success(Kind, root);
        }
        catch (Exception ex)
        {
            return WindowsAutomationResult.Failure(Kind, ex.Message);
        }
    }
}

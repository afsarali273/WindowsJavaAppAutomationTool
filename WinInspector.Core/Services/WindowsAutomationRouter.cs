using WinInspector.Core.Abstractions;
using WinInspector.Core.Backends;
using WinInspector.Core.Models;

namespace WinInspector.Core.Services;

public sealed class WindowsAutomationRouter
{
    private readonly IReadOnlyList<IWindowsAutomationBackend> _backends;

    public WindowsAutomationRouter()
        : this([
            new UiaAutomationBackend(),
            new FlaUiAutomationBackend(),
            new Win32AutomationBackend()
        ])
    {
    }

    public WindowsAutomationRouter(IReadOnlyList<IWindowsAutomationBackend> backends)
    {
        _backends = backends;
    }

    public IReadOnlyList<IWindowsAutomationBackend> Backends => _backends;

    public WindowsAutomationResult Inspect(DesktopWindowInfo window, int maxDepth = 4, int maxChildren = 200)
    {
        var failures = new List<string>();
        foreach (var backend in _backends)
        {
            if (!backend.IsAvailable())
            {
                failures.Add($"{backend.DisplayName}: unavailable");
                continue;
            }

            if (!backend.CanInspect(window))
            {
                failures.Add($"{backend.DisplayName}: skipped for {window.ApplicationKind}");
                continue;
            }

            var result = backend.InspectWindow(window, maxDepth, maxChildren);
            if (result.Succeeded) return result;
            failures.Add($"{backend.DisplayName}: {result.FailureReason}");
        }

        return WindowsAutomationResult.Failure(WindowsAutomationBackendKind.Win32, string.Join(" | ", failures));
    }
}

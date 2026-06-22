using JabInspector.Core.Diagnostics;
using JabInspector.Core.Models;
using JabInspector.Core.Services;
using WinInspector.Core.Abstractions;
using WinInspector.Core.Models;
using WinInspector.Core.Services;

var tests = new (string Name, Action Run)[]
{
    ("Locator path generation", () =>
    {
        var root = new AccessibleNode { Role = "frame", RoleEnUs = "frame" };
        var panel = new AccessibleNode { Role = "panel", RoleEnUs = "panel", Parent = root }; root.Children.Add(panel);
        var first = new AccessibleNode { Role = "push button", RoleEnUs = "push button", Parent = panel };
        var second = new AccessibleNode { Role = "push button", RoleEnUs = "push button", Parent = panel }; panel.Children.Add(first); panel.Children.Add(second);
        Assert(LocatorGenerator.BuildPath(second) == "frame[0]/panel[0]/push button[1]", "Unexpected locator path");
    }),
    ("JSON serialization", () =>
    {
        var json = JsonExportService.Serialize(new InspectorSnapshot(DateTime.UnixEpoch, "Demo", "0x1", 7, new AccessibleNode { Role = "frame" }));
        Assert(json.Contains("\"windowTitle\": \"Demo\""), "Snapshot fields missing"); Assert(!json.Contains("\"parent\":", StringComparison.OrdinalIgnoreCase), "Circular parent serialized");
    }),
    ("Bounds validation", () =>
    {
        Assert(new AccessibleNode { Width = 1, Height = 1 }.HasValidBounds, "Valid bounds rejected"); Assert(!new AccessibleNode { Width = 0, Height = 10 }.HasValidBounds, "Invalid bounds accepted");
    }),
    ("Diagnostics bitness", () => Assert(StartupDiagnostics.Generate().Any(x => x.Contains("64-bit process")), "Bitness diagnostic missing")),
    ("Windows classifier detects Java", () =>
    {
        var kind = WindowsTechnologyClassifier.Classify("SunAwtFrame", "freeplane", "Map1 - Freeplane");
        Assert(kind == WindowsApplicationKind.JavaHosted, "Java window was not classified correctly");
    }),
    ("Windows router falls back", () =>
    {
        var window = new DesktopWindowInfo
        {
            Hwnd = new IntPtr(1),
            Title = "Demo",
            ClassName = "DemoClass",
            ProcessId = 1,
            ProcessName = "demo",
            Bounds = System.Drawing.Rectangle.Empty,
            IsVisible = true,
            ApplicationKind = WindowsApplicationKind.NativeWin32
        };
        var router = new WindowsAutomationRouter([
            new FakeBackend(WindowsAutomationBackendKind.Uia, available: true, canInspect: true, result: WindowsAutomationResult.Failure(WindowsAutomationBackendKind.Uia, "uia failed")),
            new FakeBackend(WindowsAutomationBackendKind.FlaUi, available: false, canInspect: false, result: WindowsAutomationResult.Failure(WindowsAutomationBackendKind.FlaUi, "flaui unavailable")),
            new FakeBackend(WindowsAutomationBackendKind.Win32, available: true, canInspect: true, result: WindowsAutomationResult.Success(WindowsAutomationBackendKind.Win32, new WindowsAutomationNode { BackendKind = WindowsAutomationBackendKind.Win32, Role = "window" }))
        ]);
        var result = router.Inspect(window);
        Assert(result.Succeeded && result.BackendKind == WindowsAutomationBackendKind.Win32, "Router did not fall back to Win32");
    })
};

var failures = 0;
foreach (var test in tests) try { test.Run(); Console.WriteLine($"PASS  {test.Name}"); } catch (Exception ex) { failures++; Console.WriteLine($"FAIL  {test.Name}: {ex.Message}"); }
return failures;
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

file sealed class FakeBackend(WindowsAutomationBackendKind kind, bool available, bool canInspect, WindowsAutomationResult result) : IWindowsAutomationBackend
{
    public WindowsAutomationBackendKind Kind => kind;
    public string DisplayName => kind.ToString();
    public bool IsAvailable() => available;
    public bool CanInspect(DesktopWindowInfo window) => canInspect;
    public WindowsAutomationResult InspectWindow(DesktopWindowInfo window, int maxDepth = 4, int maxChildren = 200) => result;
}

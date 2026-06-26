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
    ("Java resolver distinguishes adjacent buttons", () =>
    {
        var root = new AccessibleNode { Role = "frame", RoleEnUs = "frame", IndexInParent = 0, X = 100, Y = 100, Width = 500, Height = 300 };
        var panel = new AccessibleNode { Role = "panel", RoleEnUs = "panel", Parent = root, IndexInParent = 0, X = 100, Y = 100, Width = 500, Height = 300 };
        root.Children.Add(panel);

        var left = new AccessibleNode
        {
            Role = "push button",
            RoleEnUs = "push button",
            Parent = panel,
            IndexInParent = 0,
            X = 120,
            Y = 140,
            Width = 80,
            Height = 28
        };
        var right = new AccessibleNode
        {
            Role = "push button",
            RoleEnUs = "push button",
            Parent = panel,
            IndexInParent = 1,
            X = 220,
            Y = 140,
            Width = 80,
            Height = 28
        };
        panel.Children.Add(left);
        panel.Children.Add(right);
        LocatorGenerator.AssignPaths(root);

        var entry = new JavaObjectRepositoryEntry
        {
            Role = right.Role,
            RoleEnUs = right.RoleEnUs,
            Path = right.Path,
            IndexPath = LocatorGenerator.BuildIndexPath(right),
            ParentRole = panel.Role,
            ParentName = panel.Name,
            IndexInParent = right.IndexInParent,
            X = right.X,
            Y = right.Y,
            Width = right.Width,
            Height = right.Height
        };

        var step = new JavaRecordedStep
        {
            ObjectPath = right.Path,
            ObjectRole = right.Role,
            RecordedScreenX = right.X + right.Width / 2,
            RecordedScreenY = right.Y + right.Height / 2
        };

        var resolver = new JavaNodeResolverService();
        var resolved = resolver.Resolve(root, entry, step);
        Assert(ReferenceEquals(resolved, right), "Resolver picked the wrong adjacent button");
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
    ("Virtual keypad planner resolves pane keys", () =>
    {
        var keyboard = new AccessibleNode { Role = "layered pane", RoleEnUs = "layered pane", ChildrenCount = 4 };
        keyboard.Children.Add(new AccessibleNode { Role = "push button", RoleEnUs = "push button", Name = "1", Parent = keyboard, X = 10, Y = 10, Width = 20, Height = 20 });
        keyboard.Children.Add(new AccessibleNode { Role = "push button", RoleEnUs = "push button", Name = "2", Parent = keyboard, X = 40, Y = 10, Width = 20, Height = 20 });
        keyboard.Children.Add(new AccessibleNode { Role = "push button", RoleEnUs = "push button", Name = "Space", Parent = keyboard, X = 70, Y = 10, Width = 50, Height = 20 });
        keyboard.Children.Add(new AccessibleNode { Role = "push button", RoleEnUs = "push button", Name = "OK", Parent = keyboard, X = 130, Y = 10, Width = 40, Height = 20 });

        var service = new JavaVirtualKeypadService();
        Assert(service.ShouldUseVirtualKeypad(keyboard, "12 \n"), "Layered-pane keypad was not detected");
        Assert(service.TryBuildPlan(keyboard, "12 \n", out var plan, out var message), message);
        Assert(plan.Steps.Count == 4, "Unexpected virtual keypad step count");
        Assert(plan.Steps[0].KeyNode.Name == "1", "Digit key was not resolved");
        Assert(plan.Steps[2].KeyNode.Name == "Space", "Space key was not resolved");
        Assert(plan.Steps[3].KeyNode.Name == "OK", "Enter/OK key was not resolved");
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

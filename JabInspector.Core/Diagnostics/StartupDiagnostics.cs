using JabInspector.Native;

namespace JabInspector.Core.Diagnostics;

public static class StartupDiagnostics
{
    public static IReadOnlyList<string> Generate() => NativeEnvironment.GetDiagnostics()
        .Concat(new[]
        {
            $"jabswitch: {InspectorRequirementsService.FindJabSwitch() ?? "Not found"}",
            "Tip: open Settings in the inspector to review JAB requirements and run enable/disable actions."
        }).ToArray();
}

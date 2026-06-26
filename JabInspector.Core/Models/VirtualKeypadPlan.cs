namespace JabInspector.Core.Models;

public sealed record VirtualKeypadPlan(
    AccessibleNode KeyboardRoot,
    string Text,
    IReadOnlyList<VirtualKeypadStep> Steps);

public sealed record VirtualKeypadStep(
    char Character,
    string Label,
    AccessibleNode KeyNode);

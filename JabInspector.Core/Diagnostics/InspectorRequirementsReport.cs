namespace JabInspector.Core.Diagnostics;

public sealed class InspectorRequirementsReport
{
    public required string Summary { get; init; }
    public required string JavaHome { get; init; }
    public required string BridgeDllPath { get; init; }
    public required string JabSwitchPath { get; init; }
    public required string AccessibilityRegistrationPath { get; init; }
    public required IReadOnlyList<RequirementCheck> Checks { get; init; }
}

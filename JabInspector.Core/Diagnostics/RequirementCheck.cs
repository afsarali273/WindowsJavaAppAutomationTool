namespace JabInspector.Core.Diagnostics;

public sealed class RequirementCheck
{
    public required string Title { get; init; }
    public required string Status { get; init; }
    public required string Details { get; init; }
    public required bool IsOk { get; init; }
    public bool IsWarning => !IsOk && !string.Equals(Status, "Missing", StringComparison.OrdinalIgnoreCase);
}

namespace JabInspector.Core.Models;

public sealed record JavaActionExecutionResult(
    bool Success,
    string Message,
    string? Text = null);

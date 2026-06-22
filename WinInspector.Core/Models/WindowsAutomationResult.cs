namespace WinInspector.Core.Models;

public sealed class WindowsAutomationResult
{
    public required WindowsAutomationBackendKind BackendKind { get; init; }
    public required bool Succeeded { get; init; }
    public string FailureReason { get; init; } = "";
    public WindowsAutomationNode? Root { get; init; }

    public static WindowsAutomationResult Success(WindowsAutomationBackendKind kind, WindowsAutomationNode root) =>
        new() { BackendKind = kind, Succeeded = true, Root = root };

    public static WindowsAutomationResult Failure(WindowsAutomationBackendKind kind, string reason) =>
        new() { BackendKind = kind, Succeeded = false, FailureReason = reason };
}

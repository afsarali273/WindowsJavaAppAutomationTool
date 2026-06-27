using JabInspector.Core.Models;

namespace JabInspector.Api.Services;

public sealed record JavaWindowDto(
    string Hwnd,
    string Title,
    string ClassName,
    int ProcessId,
    int VmId)
{
    public static JavaWindowDto From(JavaWindowInfo window) => new(
        window.HwndDisplay,
        window.Title,
        window.ClassName,
        window.ProcessId,
        window.VmId);
}

public sealed record CreateSessionRequest(
    string? Hwnd = null,
    string? Title = null,
    int? ProcessId = null,
    bool RefreshTree = true);

public sealed record LoadRepositoryRequest(
    string? Path = null,
    IReadOnlyList<string>? Paths = null);

public sealed record JavaWindowSelector(
    string? Hwnd = null,
    string? Title = null,
    string? ClassName = null,
    int? ProcessId = null,
    int? VmId = null,
    bool ExactTitle = false);

public sealed record SwitchWindowRequest(
    string? Hwnd = null,
    string? Title = null,
    string? ClassName = null,
    int? ProcessId = null,
    int? VmId = null,
    bool ExactTitle = false,
    bool RefreshTree = true);

public sealed record ResolveElementRequest(
    string? ObjectKey = null,
    LocatorSuggestion? Locator = null,
    JavaWindowSelector? Window = null,
    ResolutionPolicy? ResolutionPolicy = null,
    bool AutoSwitchWindow = true,
    bool RefreshTree = false);

public sealed record JavaActionRequest(
    string Action,
    string? ObjectKey = null,
    LocatorSuggestion? Locator = null,
    string? Text = null,
    JavaWindowSelector? Window = null,
    ResolutionPolicy? ResolutionPolicy = null,
    bool AutoSwitchWindow = true,
    bool RefreshTree = false,
    bool PreferAccessibleAction = true);

public sealed record JavaOneShotActionRequest(
    string Action,
    string? RepositoryPath = null,
    IReadOnlyList<string>? RepositoryPaths = null,
    string? ObjectKey = null,
    LocatorSuggestion? Locator = null,
    string? Text = null,
    JavaWindowSelector? Window = null,
    ResolutionPolicy? ResolutionPolicy = null,
    bool AutoSwitchWindow = true,
    bool RefreshTree = true,
    bool PreferAccessibleAction = true,
    bool KeepSession = false);

public sealed record DriverResult(
    bool Success,
    string Message,
    string? SessionId = null,
    object? Data = null);

public sealed record JavaSessionSummary(
    string SessionId,
    string WindowHwnd,
    string WindowTitle,
    string WindowClassName,
    int ProcessId,
    int VmId,
    int NodeCount,
    int RepositoryObjectCount,
    DateTime CreatedAtUtc,
    DateTime LastRefreshedAtUtc);

public sealed record ResolvedElementDto(
    string DisplayName,
    LocatorSuggestion Locator,
    IReadOnlyList<string> Actions);

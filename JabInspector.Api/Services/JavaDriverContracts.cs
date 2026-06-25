using JabInspector.Core.Models;

namespace JabInspector.Api.Services;

public sealed record CreateSessionRequest(
    string? Hwnd = null,
    string? Title = null,
    int? ProcessId = null,
    bool RefreshTree = true);

public sealed record LoadRepositoryRequest(string Path);

public sealed record ResolveElementRequest(
    string? ObjectKey = null,
    LocatorSuggestion? Locator = null,
    bool RefreshTree = false);

public sealed record JavaActionRequest(
    string Action,
    string? ObjectKey = null,
    LocatorSuggestion? Locator = null,
    string? Text = null,
    bool RefreshTree = false,
    bool PreferAccessibleAction = true);

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

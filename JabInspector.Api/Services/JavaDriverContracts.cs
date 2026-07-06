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

public sealed record LaunchApplicationRequest(
    string ApplicationPath,
    IReadOnlyList<string>? Arguments = null,
    string? ArgumentsText = null,
    string? WorkingDirectory = null,
    string? JavaExecutablePath = null,
    JavaWindowSelector? WaitForWindow = null,
    int? WaitTimeoutMs = null,
    int? WaitPollIntervalMs = null,
    bool UseShellExecute = false,
    bool CreateNoWindow = false);

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

public sealed record JavaNavigationRequest(
    string Command,
    int Count = 1);

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

public sealed record JavaValidationRequest(
    string? RepositoryPath = null,
    IReadOnlyList<string>? RepositoryPaths = null,
    string? ObjectKey = null,
    LocatorSuggestion? Locator = null,
    string? ExpectedText = null,
    JavaWindowSelector? Window = null,
    ResolutionPolicy? ResolutionPolicy = null,
    bool AutoSwitchWindow = true,
    bool RefreshTree = true);

public sealed record JavaFindElementsRequest(
    string? RepositoryPath = null,
    IReadOnlyList<string>? RepositoryPaths = null,
    string? ObjectKey = null,
    LocatorSuggestion? Locator = null,
    JavaWindowSelector? Window = null,
    ResolutionPolicy? ResolutionPolicy = null,
    bool AutoSwitchWindow = true,
    bool RefreshTree = true,
    int? MinimumScore = null,
    int? MaxResults = null);

public sealed record JavaFindChildElementsRequest(
    string? RepositoryPath = null,
    IReadOnlyList<string>? RepositoryPaths = null,
    string? ParentObjectKey = null,
    LocatorSuggestion? ParentLocator = null,
    JavaWindowSelector? Window = null,
    ResolutionPolicy? ResolutionPolicy = null,
    bool AutoSwitchWindow = true,
    bool RefreshTree = true,
    bool IncludeSelf = false,
    int? MaxDepth = null,
    int? MaxResults = null);

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

public sealed record JavaElementValidationDto(
    bool Exists,
    bool IsVisible,
    bool IsShowing,
    bool IsEnabled,
    bool IsFocusable,
    bool IsSelected,
    bool HasText,
    bool TextMatches,
    string DisplayName,
    string Role,
    string Name,
    string States,
    string Text,
    ElementBounds? Bounds,
    LocatorSuggestion? Locator,
    IReadOnlyList<string> Actions,
    string Message);

public sealed record JavaElementSnapshotDto(
    string DisplayName,
    string Role,
    string RoleEnUs,
    string Name,
    string VirtualAccessibleName,
    string Description,
    string States,
    string StatesEnUs,
    int IndexInParent,
    int ObjectDepth,
    int ChildrenCount,
    string Path,
    string IndexPath,
    string XPath,
    string ParentRole,
    string ParentName,
    bool IsTableLikeContainer,
    bool IsTableLikeRow,
    bool IsTableLikeCell,
    string TableLikeKind,
    string TableLikeContainerPath,
    string TableLikeColumnHeader,
    int TableLikeRowIndex,
    int TableLikeColumnIndex,
    int TableLikeRowCount,
    int TableLikeColumnCount,
    bool IsFormsLikeScope,
    bool IsFormsViewportLikeContainer,
    string FormsScopePath,
    string FormsScopeRole,
    string FormsScopeName,
    string FormsViewportPath,
    string FormsViewportRole,
    string FormsViewportName,
    string TextPreview,
    string CurrentValue,
    ElementBounds Bounds,
    LocatorSuggestion Locator,
    IReadOnlyList<string> Actions,
    int Score);

public sealed record JavaLaunchResultDto(
    string ApplicationPath,
    string LaunchTarget,
    string LaunchArguments,
    string WorkingDirectory,
    int ProcessId,
    DateTime StartedAtUtc,
    bool WaitedForWindow,
    JavaWindowDto? Window);

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using JabInspector.Core.Diagnostics;
using JabInspector.Core.Models;
using JabInspector.Core.Services;

namespace JabInspector.Api.Services;

public sealed class JavaDriverService : IDisposable
{
    private readonly AccessBridgeService _bridge;
    private readonly InspectorLogger _logger;
    private readonly JavaObjectRepositoryService _repositoryService = new();
    private readonly JavaNodeResolverService _resolver = new();
    private readonly JavaActionExecutionService _javaActions = new();
    private readonly AutomationService _automation;
    private readonly ConcurrentDictionary<string, JavaDriverSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public JavaDriverService(AccessBridgeService bridge, InspectorLogger logger)
    {
        _bridge = bridge;
        _logger = logger;
        _automation = new AutomationService(_bridge, _logger);
    }

    public bool Initialize() => _bridge.Initialize();

    public IReadOnlyList<JavaWindowInfo> GetWindows()
    {
        lock (_sync)
        {
            var discovery = new JavaWindowDiscoveryService(_bridge, _logger);
            return discovery.GetJavaWindows();
        }
    }

    public DriverResult CreateSession(CreateSessionRequest request)
    {
        lock (_sync)
        {
            var window = FindWindow(request);
            if (window is null)
                return Fail("No matching Java window was found. Provide hwnd, title, or processId.");

            var session = new JavaDriverSession
            {
                Id = Guid.NewGuid().ToString("N"),
                Window = window
            };

            if (request.RefreshTree)
            {
                var refreshed = RefreshSessionTree(session);
                if (!refreshed.Success) return refreshed with { SessionId = session.Id };
            }

            _sessions[session.Id] = session;
            _logger.Log($"API session created. SessionId={session.Id}, Window='{window.Title}', Hwnd={window.HwndDisplay}.");
            return Ok("Session created.", session.Id, ToSummary(session));
        }
    }

    public IReadOnlyList<JavaSessionSummary> GetSessions()
    {
        lock (_sync)
        {
            return _sessions.Values.Select(ToSummary).OrderBy(x => x.CreatedAtUtc).ToList();
        }
    }

    public bool DeleteSession(string sessionId)
    {
        lock (_sync)
        {
            var removed = _sessions.TryRemove(sessionId, out _);
            if (removed) _logger.Log($"API session deleted. SessionId={sessionId}.");
            return removed;
        }
    }

    public DriverResult RefreshSession(string sessionId)
    {
        if (!TryGetSession(sessionId, out var session, out var result)) return result;
        lock (_sync) return RefreshSessionTree(session);
    }

    public DriverResult GetSessionWindows(string sessionId)
    {
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session, out var result)) return result;
            var windows = GetRelatedWindows(session)
                .Select(window => new
                {
                    isActive = window.Hwnd == session.Window.Hwnd,
                    window = JavaWindowDto.From(window)
                })
                .ToList();

            return Ok("Session windows returned.", sessionId, new
            {
                activeWindow = JavaWindowDto.From(session.Window),
                repositoryWindows = session.Windows,
                count = windows.Count,
                windows
            });
        }
    }

    public DriverResult SwitchSessionWindow(string sessionId, SwitchWindowRequest request)
    {
        if (!TryGetSession(sessionId, out var session, out var result)) return result;
        lock (_sync)
        {
            var window = FindRelatedWindow(session, new JavaWindowSelector(
                request.Hwnd,
                request.Title,
                request.ClassName,
                request.ProcessId,
                request.VmId,
                request.ExactTitle));

            if (window is null)
                return Fail("No matching Java window/modal was found for this session.", sessionId);

            session.Window = window;
            session.Root = null;
            session.NodeCount = 0;
            session.LastRefreshedAtUtc = DateTime.UtcNow;
            _logger.Log($"API session switched active Java window. SessionId={sessionId}, Window='{window.Title}', Hwnd={window.HwndDisplay}.");

            return request.RefreshTree
                ? RefreshSessionTree(session)
                : Ok("Session active window switched.", sessionId, ToSummary(session));
        }
    }

    public DriverResult GetTree(string sessionId)
    {
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session, out var result)) return result;
            if (session.Root is null) return Fail("Session has no tree yet. Call refresh first.", sessionId);
            return Ok("Tree returned.", sessionId, new
            {
                session = ToSummary(session),
                root = session.Root
            });
        }
    }

    public DriverResult LoadRepository(string sessionId, LoadRepositoryRequest request)
    {
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session, out var result)) return result;
            if (string.IsNullOrWhiteSpace(request.Path) || !File.Exists(request.Path))
                return Fail($"Repository/project file was not found: {request.Path}", sessionId);

            try
            {
                var project = _repositoryService.LoadProject(request.Path);
                session.Repository.Clear();
                session.Repository.AddRange(project.Repository);
                session.Windows.Clear();
                session.Windows.AddRange(project.Windows);
                _logger.Log($"API repository loaded. SessionId={sessionId}, Path='{request.Path}', Objects={session.Repository.Count}, Windows={session.Windows.Count}.");
                return Ok("Repository loaded.", sessionId, new
                {
                    project.SessionName,
                    project.ApplicationAlias,
                    windowCount = session.Windows.Count,
                    windows = session.Windows,
                    objectCount = session.Repository.Count,
                    objects = session.Repository.Select(ToRepositorySummary).ToList()
                });
            }
            catch (Exception ex)
            {
                return Fail($"Could not load repository: {ex.Message}", sessionId);
            }
        }
    }

    public DriverResult GetRepository(string sessionId)
    {
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session, out var result)) return result;
            return Ok("Repository returned.", sessionId, session.Repository.Select(ToRepositorySummary).ToList());
        }
    }

    public DriverResult ResolveElement(string sessionId, ResolveElementRequest request)
    {
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session, out var result)) return result;
            var routed = RouteSessionWindow(session, request.ObjectKey, request.Window, request.AutoSwitchWindow);
            if (!routed.Success) return routed;

            if (request.RefreshTree || session.Root is null)
            {
                var refresh = RefreshSessionTree(session);
                if (!refresh.Success) return refresh;
            }

            var resolution = ResolveNodeWithRetry(session, request.ObjectKey, request.Locator, request.ResolutionPolicy);
            if (!resolution.Success || resolution.Node is null)
                return Fail(resolution.Message, sessionId, resolution.Details);

            var locator = LocatorGenerator.GenerateLocator(resolution.Node);
            return Ok("Element resolved.", sessionId, new ResolvedElementDto(
                resolution.Node.DisplayName,
                locator,
                _automation.GetActions(resolution.Node)));
        }
    }

    public DriverResult ExecuteAction(string sessionId, JavaActionRequest request)
    {
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session, out var result)) return result;
            var routed = RouteSessionWindow(session, request.ObjectKey, request.Window, request.AutoSwitchWindow);
            if (!routed.Success) return routed;

            if (request.RefreshTree || session.Root is null)
            {
                var refresh = RefreshSessionTree(session);
                if (!refresh.Success) return refresh;
            }

            var resolution = ResolveNodeWithRetry(session, request.ObjectKey, request.Locator, request.ResolutionPolicy);
            if (!resolution.Success || resolution.Node is null)
                return Fail(resolution.Message, sessionId, resolution.Details);

            var node = resolution.Node;
            if (!TryNormalizeAction(request.Action, out var action, out var actionError))
                return Fail(actionError, sessionId);

            var execution = _javaActions.Execute(
                action,
                node,
                request.Text ?? "",
                new ApiJavaActionHost(this, session.Window, request.PreferAccessibleAction));
            if (!execution.Success) return Fail($"Action '{request.Action}' failed for {node.DisplayName}: {execution.Message}", sessionId);

            return Ok($"Action '{action}' executed.", sessionId, new
            {
                action,
                element = new ResolvedElementDto(node.DisplayName, LocatorGenerator.GenerateLocator(node), _automation.GetActions(node)),
                message = execution.Message,
                text = execution.Text
            });
        }
    }

    private JavaWindowInfo? FindWindow(CreateSessionRequest request)
    {
        var windows = GetWindows();
        if (!string.IsNullOrWhiteSpace(request.Hwnd))
        {
            var normalized = request.Hwnd.Trim();
            return windows.FirstOrDefault(x =>
                string.Equals(x.HwndDisplay, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Hwnd.ToInt64().ToString("X"), normalized.TrimStart('0', 'x', 'X'), StringComparison.OrdinalIgnoreCase));
        }

        if (request.ProcessId is not null)
            return windows.FirstOrDefault(x => x.ProcessId == request.ProcessId.Value);

        if (!string.IsNullOrWhiteSpace(request.Title))
            return windows.FirstOrDefault(x => x.Title.Contains(request.Title, StringComparison.OrdinalIgnoreCase));

        return windows.FirstOrDefault();
    }

    private DriverResult RouteSessionWindow(JavaDriverSession session, string? objectKey, JavaWindowSelector? selector, bool autoSwitch)
    {
        if (selector is not null)
        {
            var selected = FindRelatedWindow(session, selector);
            if (selected is null) return Fail("Requested Java window/modal was not found for this session.", session.Id);
            if (selected.Hwnd != session.Window.Hwnd) SwitchSessionWindow(session, selected);
            return Ok("Session routed to requested window.", session.Id, ToSummary(session));
        }

        if (!autoSwitch || string.IsNullOrWhiteSpace(objectKey)) return Ok("Window routing not required.", session.Id);

        var entry = session.Repository.FirstOrDefault(x => string.Equals(x.ObjectKey, objectKey, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return Ok("Repository object not loaded; window routing skipped.", session.Id);

        var scope = !string.IsNullOrWhiteSpace(entry.WindowKey)
            ? session.Windows.FirstOrDefault(x => string.Equals(x.WindowKey, entry.WindowKey, StringComparison.OrdinalIgnoreCase))
            : null;
        if (scope is not null && WindowMatchesScope(scope, session.Window) && ScopeProcessMatches(scope, session.Window))
            return Ok("Session already uses recorded object window scope.", session.Id);
        if (scope is null && EntryMatchesWindow(entry, session.Window)) return Ok("Session already uses recorded object window.", session.Id);

        var recordedWindow = scope is not null
            ? FindRelatedWindow(session, scope)
            : FindRelatedWindow(session, new JavaWindowSelector(
            Hwnd: entry.WindowHwndDisplay,
            Title: entry.WindowTitle,
            ClassName: entry.WindowClassName,
            ProcessId: entry.WindowProcessId == 0 ? null : entry.WindowProcessId,
            VmId: entry.WindowVmId == 0 ? null : entry.WindowVmId,
            ExactTitle: true));

        if (recordedWindow is null)
            return Fail($"Could not find recorded window/modal '{scope?.FriendlyName ?? entry.WindowTitle}' for object '{entry.ObjectKey}'.", session.Id, new
            {
                entry.ObjectKey,
                entry.WindowKey,
                expectedWindow = scope,
                discoveredWindows = GetRelatedWindows(session).Select(JavaWindowDto.From).ToList()
            });

        SwitchSessionWindow(session, recordedWindow);
        return Ok("Session auto-switched to recorded object window.", session.Id, ToSummary(session));
    }

    private IReadOnlyList<JavaWindowInfo> GetRelatedWindows(JavaDriverSession session)
    {
        var windows = GetWindows();
        return windows
            .Where(window =>
                window.Hwnd == session.Window.Hwnd ||
                (session.Window.ProcessId != 0 && window.ProcessId == session.Window.ProcessId) ||
                (session.Window.VmId != 0 && window.VmId == session.Window.VmId))
            .OrderByDescending(window => window.Hwnd == session.Window.Hwnd)
            .ThenBy(window => window.Title)
            .ToList();
    }

    private JavaWindowInfo? FindRelatedWindow(JavaDriverSession session, JavaWindowSelector selector)
    {
        var related = GetRelatedWindows(session);
        return FindWindow(related, selector) ?? FindWindow(GetWindows(), selector);
    }

    private JavaWindowInfo? FindRelatedWindow(JavaDriverSession session, JavaWindowLocator scope)
    {
        var related = GetRelatedWindows(session);
        return FindWindow(related, scope) ?? FindWindow(GetWindows(), scope);
    }

    private static JavaWindowInfo? FindWindow(IEnumerable<JavaWindowInfo> windows, JavaWindowSelector selector)
    {
        var candidates = windows.ToList();
        if (!string.IsNullOrWhiteSpace(selector.Hwnd))
        {
            var normalized = selector.Hwnd.Trim();
            candidates = candidates
                .Where(window =>
                    string.Equals(window.HwndDisplay, normalized, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(window.Hwnd.ToInt64().ToString("X"), normalized.TrimStart('0', 'x', 'X'), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (selector.ProcessId is not null)
            candidates = candidates.Where(window => window.ProcessId == selector.ProcessId.Value).ToList();
        if (selector.VmId is not null)
            candidates = candidates.Where(window => window.VmId == selector.VmId.Value).ToList();
        if (!string.IsNullOrWhiteSpace(selector.ClassName))
            candidates = candidates.Where(window => string.Equals(window.ClassName, selector.ClassName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(selector.Title))
            candidates = candidates.Where(window => selector.ExactTitle
                ? string.Equals(window.Title, selector.Title, StringComparison.OrdinalIgnoreCase)
                : window.Title.Contains(selector.Title, StringComparison.OrdinalIgnoreCase)).ToList();

        return candidates.FirstOrDefault();
    }

    private static JavaWindowInfo? FindWindow(IEnumerable<JavaWindowInfo> windows, JavaWindowLocator scope)
    {
        return windows
            .Where(window => WindowMatchesScope(scope, window))
            .Where(window => ScopeProcessMatches(scope, window))
            .OrderByDescending(window => !string.IsNullOrWhiteSpace(scope.HwndDisplay)
                                         && string.Equals(scope.HwndDisplay, window.HwndDisplay, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(window => !string.IsNullOrWhiteSpace(scope.Title)
                                        && string.Equals(scope.Title, window.Title, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    private void SwitchSessionWindow(JavaDriverSession session, JavaWindowInfo window)
    {
        session.Window = window;
        session.Root = null;
        session.NodeCount = 0;
        session.LastRefreshedAtUtc = DateTime.UtcNow;
        _logger.Log($"API session active window changed. SessionId={session.Id}, Window='{window.Title}', Hwnd={window.HwndDisplay}.");
    }

    private static bool EntryMatchesWindow(JavaObjectRepositoryEntry entry, JavaWindowInfo window)
    {
        if (!string.IsNullOrWhiteSpace(entry.WindowHwndDisplay) &&
            string.Equals(entry.WindowHwndDisplay, window.HwndDisplay, StringComparison.OrdinalIgnoreCase)) return true;
        if (entry.WindowProcessId != 0 && entry.WindowProcessId != window.ProcessId) return false;
        if (entry.WindowVmId != 0 && entry.WindowVmId != window.VmId) return false;
        if (!string.IsNullOrWhiteSpace(entry.WindowClassName) &&
            !string.Equals(entry.WindowClassName, window.ClassName, StringComparison.OrdinalIgnoreCase)) return false;
        return string.IsNullOrWhiteSpace(entry.WindowTitle) ||
               string.Equals(entry.WindowTitle, window.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static bool WindowMatchesScope(JavaWindowLocator scope, JavaWindowInfo window)
    {
        if (!string.IsNullOrWhiteSpace(scope.HwndDisplay)
            && string.Equals(scope.HwndDisplay, window.HwndDisplay, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrWhiteSpace(scope.ClassName)
            && !string.Equals(scope.ClassName, window.ClassName, StringComparison.OrdinalIgnoreCase)) return false;

        return scope.TitleMatch switch
        {
            JavaWindowTitleMatch.Contains => string.IsNullOrWhiteSpace(scope.Title) || window.Title.Contains(scope.Title, StringComparison.OrdinalIgnoreCase),
            JavaWindowTitleMatch.Regex => MatchesRegex(window.Title, scope.Title),
            _ => string.IsNullOrWhiteSpace(scope.Title) || string.Equals(scope.Title, window.Title, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool ScopeProcessMatches(JavaWindowLocator scope, JavaWindowInfo window)
    {
        if (scope.ProcessId != 0 && scope.ProcessId != window.ProcessId) return false;
        if (scope.VmId != 0 && scope.VmId != window.VmId) return false;
        return true;
    }

    private static bool MatchesRegex(string value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return true;
        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private DriverResult RefreshSessionTree(JavaDriverSession session)
    {
        var crawler = new AccessibleTreeCrawler(_bridge, _logger);
        var root = crawler.BuildTree(session.Window);
        if (root is null)
            return Fail($"Could not crawl Java accessibility tree for '{session.Window.Title}'.", session.Id);

        session.Root = root;
        session.NodeCount = crawler.NodeCount;
        session.LastRefreshedAtUtc = DateTime.UtcNow;
        return Ok("Session tree refreshed.", session.Id, ToSummary(session));
    }

    private ResolveResult ResolveNode(JavaDriverSession session, string? objectKey, LocatorSuggestion? locator)
    {
        return ResolveNode(session, objectKey, locator, ResolutionPolicy.Default);
    }

    private ResolveResult ResolveNodeWithRetry(
        JavaDriverSession session,
        string? objectKey,
        LocatorSuggestion? locator,
        ResolutionPolicy? requestedPolicy)
    {
        var policy = (requestedPolicy ?? ResolutionPolicy.Default).Sanitize();
        var started = DateTime.UtcNow;
        var attempt = 0;
        ResolveResult? last = null;
        var refreshedAfterFailure = false;

        while (true)
        {
            attempt++;
            last = ResolveNode(session, objectKey, locator, policy);
            if (last.Success) return last with { Message = $"{last.Message} Attempts={attempt}." };

            var elapsedMs = (int)(DateTime.UtcNow - started).TotalMilliseconds;
            if (elapsedMs >= policy.TimeoutMs || policy.TimeoutMs == 0)
                return last with { Message = $"{last.Message} Attempts={attempt}, elapsedMs={elapsedMs}." };

            if (policy.RefreshTreeOnFailure && !refreshedAfterFailure)
            {
                var refresh = RefreshSessionTree(session);
                refreshedAfterFailure = true;
                if (!refresh.Success)
                    return last with { Message = $"{last.Message} Tree refresh after failure also failed: {refresh.Message}" };
            }

            Thread.Sleep(Math.Min(policy.PollIntervalMs, Math.Max(50, policy.TimeoutMs - elapsedMs)));
        }
    }

    private ResolveResult ResolveNode(
        JavaDriverSession session,
        string? objectKey,
        LocatorSuggestion? locator,
        ResolutionPolicy policy)
    {
        if (session.Root is null)
            return new(false, "Session tree is empty. Refresh the session first.", null);

        JavaObjectRepositoryEntry? entry = null;
        if (!string.IsNullOrWhiteSpace(objectKey))
        {
            entry = session.Repository.FirstOrDefault(x => string.Equals(x.ObjectKey, objectKey, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return new(false, $"Object key '{objectKey}' was not found in the loaded repository.", null);
        }
        else if (locator is not null)
        {
            entry = CreateRepositoryEntry(locator, session.Window);
        }
        else
        {
            return new(false, "Provide either objectKey or locator.", null);
        }

        var step = locator is null ? null : new JavaRecordedStep
        {
            ObjectKey = entry.ObjectKey,
            ObjectLocator = locator,
            ObjectLocatorJson = JsonExportService.Serialize(locator),
            ObjectRole = locator.Role,
            ObjectName = locator.Name,
            ObjectVirtualAccessibleName = locator.VirtualAccessibleName,
            ObjectDescription = locator.Description,
            ObjectPath = locator.Path,
            ObjectDepth = locator.ObjectDepth
        };

        var resolution = _resolver.ResolveDetailed(session.Root, entry, step, policy);
        return !resolution.Success || resolution.Node is null
            ? new(false, resolution.Message, null, resolution)
            : new(true, $"Resolved using {resolution.StrategyName}.", resolution.Node, resolution);
    }

    private JavaObjectRepositoryEntry CreateRepositoryEntry(LocatorSuggestion locator, JavaWindowInfo window) => new()
    {
        ObjectKey = "inline_locator",
        FriendlyName = string.IsNullOrWhiteSpace(locator.Name) ? locator.Role : locator.Name,
        CapturedAtUtc = DateTime.UtcNow,
        WindowKey = _repositoryService.CreateWindowKey(window),
        WindowHwndDisplay = window.HwndDisplay,
        WindowTitle = window.Title,
        WindowClassName = window.ClassName,
        WindowProcessId = window.ProcessId,
        WindowVmId = window.VmId,
        Engine = locator.Engine,
        Locator = locator,
        LocatorJson = JsonExportService.Serialize(locator),
        Role = locator.Role,
        RoleEnUs = locator.RoleEnUs,
        Name = locator.Name,
        VirtualAccessibleName = locator.VirtualAccessibleName,
        Description = locator.Description,
        States = locator.States,
        StatesEnUs = locator.StatesEnUs,
        Path = locator.Path,
        IndexPath = locator.IndexPath,
        XPath = locator.XPath,
        IndexXPath = locator.IndexXPath,
        SemanticXPath = locator.SemanticXPath,
        ParentRole = locator.ParentRole,
        ParentName = locator.ParentName,
        IndexInParent = locator.IndexInParent,
        ObjectDepth = locator.ObjectDepth,
        ChildrenCount = locator.ChildrenCount,
        X = locator.Bounds.X,
        Y = locator.Bounds.Y,
        Width = locator.Bounds.Width,
        Height = locator.Bounds.Height,
        HasManagedDescendantAncestor = locator.HasManagedDescendantAncestor,
        ActionNames = locator.ActionNames.ToList()
    };

    private bool PhysicalClick(AccessibleNode node, int count)
    {
        if (node.Width <= 0 || node.Height <= 0) return false;
        if (node.X == 0 && node.Y == 0) return false;

        var x = node.X + node.Width / 2;
        var y = node.Y + node.Height / 2;
        if (!SetCursorPos(x, y)) return false;

        for (var i = 0; i < count; i++)
        {
            MouseEvent(MouseLeftDown, 0, 0, 0, UIntPtr.Zero);
            MouseEvent(MouseLeftUp, 0, 0, 0, UIntPtr.Zero);
            if (i + 1 < count) Thread.Sleep(100);
        }

        _logger.Log($"API physical click executed on {node.DisplayName} at ({x}, {y}), Count={count}.");
        return true;
    }

    private int TypeUnicodeText(JavaWindowInfo window, AccessibleNode node, string text)
    {
        _automation.Focus(node);
        SetForegroundWindow(window.Hwnd);
        Thread.Sleep(100);
        var inputs = new List<NativeInput>(text.Length * 2);
        foreach (var character in text)
        {
            inputs.Add(NativeInput.Unicode(character, false));
            inputs.Add(NativeInput.Unicode(character, true));
        }

        var sent = inputs.Count == 0 ? 0 : (int)SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<NativeInput>()) / 2;
        _logger.Log($"API typed {sent} of {text.Length} Unicode character(s) into {node.DisplayName}.");
        return sent;
    }

    private sealed class ApiJavaActionHost(JavaDriverService owner, JavaWindowInfo window, bool allowSemanticFallback) : IJavaActionExecutionHost
    {
        public bool Focus(AccessibleNode node, out string message)
        {
            var success = owner._automation.Focus(node);
            message = success ? $"Focus requested successfully for {node.DisplayName}." : $"Focus request failed for {node.DisplayName}.";
            return success;
        }

        public bool InvokeDefaultAction(AccessibleNode node, out string message)
        {
            if (!allowSemanticFallback)
            {
                message = "Semantic fallback disabled for this request.";
                return false;
            }

            var success = owner._automation.InvokeDefaultAction(node, out var action);
            message = success ? $"Executed semantic action '{action}' on {node.DisplayName}." : $"No semantic action was available for {node.DisplayName}.";
            return success;
        }

        public bool SetText(AccessibleNode node, string text, out string message)
        {
            var success = owner._automation.SetText(node, text);
            message = success ? $"Text set successfully on {node.DisplayName} ({text.Length} characters)." : $"Set text failed for {node.DisplayName}.";
            return success;
        }

        public string GetText(AccessibleNode node, out string message)
        {
            var text = owner._automation.GetText(node);
            message = $"Read text from {node.DisplayName}.";
            return text;
        }

        public bool PhysicalClick(AccessibleNode node, int count, out string message)
        {
            var success = owner.PhysicalClick(node, count);
            message = success
                ? $"{(count == 2 ? "Double-clicked" : "Clicked")} {node.DisplayName} using physical input."
                : $"Physical click failed for {node.DisplayName}.";
            return success;
        }

        public int TypeUnicodeText(AccessibleNode node, string text, out string message)
        {
            var sent = owner.TypeUnicodeText(window, node, text);
            message = $"Typed {sent} of {text.Length} Unicode character(s) into {node.DisplayName}.";
            return sent;
        }

        public void BeforeAction(AccessibleNode node)
        {
            SetForegroundWindow(window.Hwnd);
            Thread.Sleep(70);
        }

        public void BetweenVirtualKeyClicks() => Thread.Sleep(80);
    }

    private static bool TryNormalizeAction(string action, out JavaRecordedActionKind actionKind, out string error)
    {
        actionKind = default;
        error = "";
        if (string.IsNullOrWhiteSpace(action))
        {
            error = "Action is required.";
            return false;
        }

        var normalized = action.Trim().Replace("-", "", StringComparison.OrdinalIgnoreCase).Replace("_", "", StringComparison.OrdinalIgnoreCase);
        var matched = true;
        actionKind = normalized.ToLowerInvariant() switch
        {
            "focus" => JavaRecordedActionKind.Focus,
            "click" => JavaRecordedActionKind.Click,
            "doubleclick" or "dblclick" => JavaRecordedActionKind.DoubleClick,
            "settext" or "set" => JavaRecordedActionKind.SetText,
            "typetext" or "type" => JavaRecordedActionKind.TypeText,
            "gettext" or "text" => JavaRecordedActionKind.GetText,
            _ => NoMatch()
        };

        if (matched) return true;
        error = $"Unsupported action '{action}'. Supported actions: focus, click, doubleClick, setText, typeText, getText.";
        return false;

        JavaRecordedActionKind NoMatch()
        {
            matched = false;
            return default;
        }
    }

    private bool TryGetSession(string sessionId, out JavaDriverSession session, out DriverResult result)
    {
        if (_sessions.TryGetValue(sessionId, out session!))
        {
            result = Ok("Session found.", sessionId);
            return true;
        }

        result = Fail($"Session '{sessionId}' was not found.", sessionId);
        return false;
    }

    private static JavaSessionSummary ToSummary(JavaDriverSession session) => new(
        session.Id,
        session.Window.HwndDisplay,
        session.Window.Title,
        session.Window.ClassName,
        session.Window.ProcessId,
        session.Window.VmId,
        session.NodeCount,
        session.Repository.Count,
        session.CreatedAtUtc,
        session.LastRefreshedAtUtc);

    private static object ToRepositorySummary(JavaObjectRepositoryEntry entry) => new
    {
        entry.ObjectKey,
        entry.FriendlyName,
        entry.WindowKey,
        window = new
        {
            key = entry.WindowKey,
            title = entry.WindowTitle,
            className = entry.WindowClassName,
            hwnd = entry.WindowHwndDisplay,
            processId = entry.WindowProcessId,
            vmId = entry.WindowVmId
        },
        entry.Role,
        entry.RoleEnUs,
        entry.Name,
        entry.VirtualAccessibleName,
        entry.Path,
        entry.IndexPath,
        entry.XPath,
        entry.SemanticXPath,
        bounds = new ElementBounds(entry.X, entry.Y, entry.Width, entry.Height),
        entry.Locator
    };

    private static DriverResult Ok(string message, string? sessionId = null, object? data = null) => new(true, message, sessionId, data);
    private static DriverResult Fail(string message, string? sessionId = null, object? data = null) => new(false, message, sessionId, data);

    public void Dispose() => _bridge.Dispose();

    private sealed record ResolveResult(bool Success, string Message, AccessibleNode? Node, ResolutionResult? Details = null);

    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint KeyEventUnicode = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public InputUnion Data;
        public static NativeInput Unicode(char value, bool keyUp) => new()
        {
            Type = 1,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    Scan = value,
                    Flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0)
                }
            }
        };
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "mouse_event")]
    private static extern void MouseEvent(uint flags, uint x, uint y, uint data, UIntPtr extraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
}

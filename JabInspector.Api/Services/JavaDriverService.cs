using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using JabInspector.Core.Diagnostics;
using JabInspector.Core.Models;
using JabInspector.Core.Services;
using JabInspector.Native;

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
    private bool _diagnosticsLogged;

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
            var windows = discovery.GetJavaWindows(attempts: 3, retryDelayMs: 300);
            if (windows.Count == 0) LogApiDiscoveryDiagnostics();
            return windows;
        }
    }

    public DriverResult CreateSession(CreateSessionRequest request)
    {
        lock (_sync)
        {
            var window = FindWindow(request);
            if (window is null)
                return Fail("No matching Java window was found. Provide hwnd, title, or processId. The API server could not see any Java Access Bridge windows; check /api/health and /api/java/windows from the same server process.");

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

    public DriverResult OpenApplication(LaunchApplicationRequest request)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(request.ApplicationPath))
                return Fail("ApplicationPath is required.");

            var fullPath = Path.GetFullPath(request.ApplicationPath.Trim());
            if (!File.Exists(fullPath))
                return Fail($"Application file was not found: {fullPath}");

            var workingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory
                : Path.GetFullPath(request.WorkingDirectory.Trim());

            if (!Directory.Exists(workingDirectory))
                return Fail($"Working directory was not found: {workingDirectory}");

            try
            {
                var launch = BuildLaunchStartInfo(fullPath, workingDirectory, request);
                using var process = Process.Start(launch.StartInfo);
                if (process is null)
                    return Fail("The application process could not be started.");

                var startedAtUtc = DateTime.UtcNow;
                JavaWindowInfo? matchedWindow = null;

                if (request.WaitForWindow is not null)
                {
                    var selector = BuildWaitSelector(request.WaitForWindow, process.Id, launch.CanScopeToLaunchedProcess);
                    matchedWindow = WaitForWindow(selector, request.WaitTimeoutMs, request.WaitPollIntervalMs);
                    if (matchedWindow is null)
                    {
                        return Fail(
                            $"Application launched, but no matching Java window appeared within {NormalizeTimeoutMs(request.WaitTimeoutMs)} ms.",
                            data: new JavaLaunchResultDto(
                                fullPath,
                                launch.StartInfo.FileName,
                                launch.DisplayArguments,
                                workingDirectory,
                                process.Id,
                                startedAtUtc,
                                true,
                                null));
                    }
                }

                _logger.Log($"API application launched. Path='{fullPath}', ProcessId={process.Id}, WaitedForWindow={request.WaitForWindow is not null}.");

                return Ok("Application launched.", data: new JavaLaunchResultDto(
                    fullPath,
                    launch.StartInfo.FileName,
                    launch.DisplayArguments,
                    workingDirectory,
                    process.Id,
                    startedAtUtc,
                    request.WaitForWindow is not null,
                    matchedWindow is null ? null : JavaWindowDto.From(matchedWindow)));
            }
            catch (Exception ex)
            {
                return Fail($"Could not launch application: {ex.Message}");
            }
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
            var paths = NormalizeRepositoryPaths(request.Path, request.Paths);
            if (paths.Count == 0)
                return Fail("At least one repository/project path is required.", sessionId);

            try
            {
                var projects = LoadRecordingProjects(paths);
                ReplaceSessionRepositories(session, projects);
                _logger.Log($"API repository loaded. SessionId={sessionId}, Paths={paths.Count}, Objects={session.Repository.Count}, Windows={session.Windows.Count}.");
                return Ok(paths.Count == 1 ? "Repository loaded." : "Repositories loaded.", sessionId, new
                {
                    repositoryPaths = paths,
                    repositoryCount = projects.Count,
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

    public DriverResult ValidateElement(string sessionId, JavaValidationRequest request)
    {
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session, out var result)) return result;
            return ValidateElementCore(session, request);
        }
    }

    public DriverResult ExecuteAction(string sessionId, JavaActionRequest request)
    {
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session, out var result)) return result;
            return ExecuteActionCore(session, request, isOneShot: false);
        }
    }

    public DriverResult ExecuteNavigation(string sessionId, JavaNavigationRequest request)
    {
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session, out var result)) return result;
            return ExecuteNavigationCore(session, request);
        }
    }

    public DriverResult ExecuteOneShotAction(JavaOneShotActionRequest request)
    {
        lock (_sync)
        {
            var repositoryPaths = NormalizeRepositoryPaths(request.RepositoryPath, request.RepositoryPaths);
            List<JavaRecordingProject> projects = [];
            if (repositoryPaths.Count > 0)
            {
                try
                {
                    projects = LoadRecordingProjects(repositoryPaths);
                }
                catch (Exception ex)
                {
                    return Fail($"Could not load repository: {ex.Message}");
                }
            }

            var window = FindInitialWindow(request, projects);
            if (window is null)
                return Fail("No matching Java window was found for one-shot action. Provide window title/hwnd/className/processId, or a repository object with window metadata.");

            var session = new JavaDriverSession
            {
                Id = Guid.NewGuid().ToString("N"),
                Window = window
            };

            ReplaceSessionRepositories(session, projects);

            var actionRequest = new JavaActionRequest(
                request.Action,
                request.ObjectKey,
                request.Locator,
                request.Text,
                request.Window,
                request.ResolutionPolicy,
                request.AutoSwitchWindow,
                request.RefreshTree,
                request.PreferAccessibleAction);

            if (request.KeepSession)
            {
                _sessions[session.Id] = session;
                _logger.Log($"API one-shot action created keep-alive session. SessionId={session.Id}, Window='{window.Title}', Hwnd={window.HwndDisplay}.");
            }

            var result = ExecuteActionCore(session, actionRequest, isOneShot: true);
            if (!request.KeepSession)
            {
                _logger.Log($"API one-shot action completed with ephemeral session. SessionId={session.Id}, Success={result.Success}.");
            }

            return result;
        }
    }

    public DriverResult ValidateOneShot(JavaValidationRequest request)
    {
        lock (_sync)
        {
            var repositoryPaths = NormalizeRepositoryPaths(request.RepositoryPath, request.RepositoryPaths);
            List<JavaRecordingProject> projects = [];
            if (repositoryPaths.Count > 0)
            {
                try
                {
                    projects = LoadRecordingProjects(repositoryPaths);
                }
                catch (Exception ex)
                {
                    return Fail($"Could not load repository: {ex.Message}");
                }
            }

            var window = FindInitialWindow(request, projects);
            if (window is null)
            {
                return Ok("Validation completed. No matching Java window was found.", null, CreateMissingValidation("No matching Java window was found."));
            }

            var session = new JavaDriverSession
            {
                Id = Guid.NewGuid().ToString("N"),
                Window = window
            };
            ReplaceSessionRepositories(session, projects);

            return ValidateElementCore(session, request);
        }
    }

    public DriverResult FindElements(string sessionId, JavaFindElementsRequest request)
    {
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session, out var result)) return result;
            return FindElementsCore(session, request);
        }
    }

    public DriverResult FindElementsOneShot(JavaFindElementsRequest request)
    {
        lock (_sync)
        {
            var sessionResult = CreateEphemeralSession(
                request.RepositoryPath,
                request.RepositoryPaths,
                request.Window,
                request.ObjectKey);
            if (!sessionResult.Success || sessionResult.Session is null)
                return Ok("Find elements completed. No matching Java window was found.", null, Array.Empty<JavaElementSnapshotDto>());

            return FindElementsCore(sessionResult.Session, request);
        }
    }

    public DriverResult FindChildElements(string sessionId, JavaFindChildElementsRequest request)
    {
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session, out var result)) return result;
            return FindChildElementsCore(session, request);
        }
    }

    public DriverResult FindChildElementsOneShot(JavaFindChildElementsRequest request)
    {
        lock (_sync)
        {
            var sessionResult = CreateEphemeralSession(
                request.RepositoryPath,
                request.RepositoryPaths,
                request.Window,
                request.ParentObjectKey);
            if (!sessionResult.Success || sessionResult.Session is null)
                return Ok("Find child elements completed. No matching Java window was found.", null, Array.Empty<JavaElementSnapshotDto>());

            return FindChildElementsCore(sessionResult.Session, request);
        }
    }

    private List<string> NormalizeRepositoryPaths(string? path, IReadOnlyList<string>? paths)
    {
        var normalized = new List<string>();
        if (!string.IsNullOrWhiteSpace(path)) normalized.Add(path.Trim());
        if (paths is not null)
        {
            normalized.AddRange(paths.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        }

        return normalized
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<JavaRecordingProject> LoadRecordingProjects(IReadOnlyList<string> paths)
    {
        var projects = new List<JavaRecordingProject>();
        foreach (var path in paths)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Repository/project file was not found: {path}", path);

            projects.Add(_repositoryService.LoadProject(path));
        }

        return projects;
    }

    private static void ReplaceSessionRepositories(JavaDriverSession session, IReadOnlyList<JavaRecordingProject> projects)
    {
        session.Repository.Clear();
        session.Windows.Clear();

        var objectsByKey = new Dictionary<string, JavaObjectRepositoryEntry>(StringComparer.OrdinalIgnoreCase);
        var windowsByKey = new Dictionary<string, JavaWindowLocator>(StringComparer.OrdinalIgnoreCase);
        var steps = new List<JavaRecordedStep>();

        foreach (var project in projects)
        {
            foreach (var window in project.Windows)
            {
                var key = string.IsNullOrWhiteSpace(window.WindowKey)
                    ? $"{window.HwndDisplay}|{window.Title}|{window.ClassName}|{window.ProcessId}|{window.VmId}"
                    : window.WindowKey;
                windowsByKey[key] = window;
            }

            foreach (var entry in project.Repository)
            {
                if (string.IsNullOrWhiteSpace(entry.ObjectKey)) continue;
                objectsByKey[entry.ObjectKey] = entry;
            }

            steps.AddRange(project.Steps);
        }

        session.Repository.AddRange(objectsByKey.Values);
        session.Windows.AddRange(windowsByKey.Values);
        session.Steps.Clear();
        session.Steps.AddRange(steps.OrderBy(step => step.Sequence));
    }

    private EphemeralSessionResult CreateEphemeralSession(
        string? repositoryPath,
        IReadOnlyList<string>? repositoryPaths,
        JavaWindowSelector? requestedWindow,
        string? objectKey)
    {
        var paths = NormalizeRepositoryPaths(repositoryPath, repositoryPaths);
        List<JavaRecordingProject> projects = [];
        if (paths.Count > 0)
        {
            try
            {
                projects = LoadRecordingProjects(paths);
            }
            catch (Exception ex)
            {
                return new(false, null, $"Could not load repository: {ex.Message}");
            }
        }

        var window = FindInitialWindow(requestedWindow, objectKey, projects);
        if (window is null) return new(false, null, "No matching Java window was found.");

        var session = new JavaDriverSession
        {
            Id = Guid.NewGuid().ToString("N"),
            Window = window
        };
        ReplaceSessionRepositories(session, projects);
        return new(true, session, "Ephemeral session created.");
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

    private LaunchPlan BuildLaunchStartInfo(string applicationPath, string workingDirectory, LaunchApplicationRequest request)
    {
        var extension = Path.GetExtension(applicationPath);
        var arguments = request.Arguments?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList() ?? [];
        var argumentsText = request.ArgumentsText?.Trim();
        var useShellExecute = request.UseShellExecute;
        var createNoWindow = request.CreateNoWindow;

        if (string.Equals(extension, ".jar", StringComparison.OrdinalIgnoreCase))
        {
            var javaExecutable = ResolveJavaExecutable(request.JavaExecutablePath);
            var startInfo = new ProcessStartInfo
            {
                FileName = javaExecutable,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = createNoWindow
            };

            startInfo.ArgumentList.Add("-jar");
            startInfo.ArgumentList.Add(applicationPath);
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            if (!string.IsNullOrWhiteSpace(argumentsText))
                startInfo.Arguments = $"-jar {QuoteForCommandLine(applicationPath)} {argumentsText}";

            return new LaunchPlan(
                startInfo,
                BuildDisplayArguments(startInfo, argumentsText),
                CanScopeToLaunchedProcess: true);
        }

        if (string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase))
        {
            var command = QuoteForCommandLine(applicationPath);
            if (arguments.Count > 0)
                command = $"{command} {string.Join(" ", arguments.Select(QuoteForCommandLine))}";
            else if (!string.IsNullOrWhiteSpace(argumentsText))
                command = $"{command} {argumentsText}";

            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                WorkingDirectory = workingDirectory,
                UseShellExecute = useShellExecute,
                CreateNoWindow = createNoWindow,
                Arguments = $"/c \"{command}\""
            };

            return new LaunchPlan(startInfo, startInfo.Arguments, CanScopeToLaunchedProcess: false);
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = applicationPath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = useShellExecute,
            CreateNoWindow = createNoWindow
        };

        if (arguments.Count > 0)
        {
            foreach (var argument in arguments)
                processStartInfo.ArgumentList.Add(argument);
        }
        else if (!string.IsNullOrWhiteSpace(argumentsText))
        {
            processStartInfo.Arguments = argumentsText;
        }

        return new LaunchPlan(
            processStartInfo,
            BuildDisplayArguments(processStartInfo, argumentsText),
            CanScopeToLaunchedProcess: true);
    }

    private string ResolveJavaExecutable(string? requestedJavaExecutablePath)
    {
        if (!string.IsNullOrWhiteSpace(requestedJavaExecutablePath))
        {
            var requested = Path.GetFullPath(requestedJavaExecutablePath.Trim());
            if (!File.Exists(requested))
                throw new FileNotFoundException($"Java executable was not found: {requested}", requested);
            return requested;
        }

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var javaw = Path.Combine(javaHome, "bin", "javaw.exe");
            if (File.Exists(javaw)) return javaw;

            var java = Path.Combine(javaHome, "bin", "java.exe");
            if (File.Exists(java)) return java;
        }

        return "javaw.exe";
    }

    private JavaWindowSelector BuildWaitSelector(JavaWindowSelector selector, int launchedProcessId, bool canScopeToLaunchedProcess)
    {
        if (!canScopeToLaunchedProcess) return selector;
        if (selector.ProcessId is not null || !string.IsNullOrWhiteSpace(selector.Hwnd)) return selector;
        return selector with { ProcessId = launchedProcessId };
    }

    private JavaWindowInfo? WaitForWindow(JavaWindowSelector selector, int? timeoutMs, int? pollIntervalMs)
    {
        var timeout = NormalizeTimeoutMs(timeoutMs);
        var poll = NormalizePollIntervalMs(pollIntervalMs);
        var started = Environment.TickCount64;

        while (Environment.TickCount64 - started <= timeout)
        {
            var windows = GetWindows();
            var matched = FindWindow(windows, selector);
            if (matched is not null) return matched;

            Thread.Sleep(poll);
        }

        return null;
    }

    private static int NormalizeTimeoutMs(int? timeoutMs) => timeoutMs is > 0 ? timeoutMs.Value : 30000;

    private static int NormalizePollIntervalMs(int? pollIntervalMs) => pollIntervalMs is > 0 ? pollIntervalMs.Value : 500;

    private static string BuildDisplayArguments(ProcessStartInfo startInfo, string? rawArgumentsText)
    {
        if (!string.IsNullOrWhiteSpace(rawArgumentsText))
            return rawArgumentsText;

        if (startInfo.ArgumentList.Count == 0)
            return startInfo.Arguments ?? "";

        return string.Join(" ", startInfo.ArgumentList.Select(QuoteForCommandLine));
    }

    private static string QuoteForCommandLine(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"')) return value;
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private void LogApiDiscoveryDiagnostics()
    {
        if (_diagnosticsLogged) return;
        _diagnosticsLogged = true;
        _logger.Log("API Java discovery diagnostics:");
        foreach (var line in NativeEnvironment.GetDiagnostics())
        {
            _logger.Log($"  {line}");
        }

        _logger.Log($"  User interactive: {Environment.UserInteractive}");
        _logger.Log($"  Process path: {Environment.ProcessPath ?? "(unknown)"}");
        _logger.Log($"  Current directory: {Environment.CurrentDirectory}");
    }

    private JavaWindowInfo? FindInitialWindow(JavaOneShotActionRequest request, IReadOnlyList<JavaRecordingProject> projects)
    {
        return FindInitialWindow(request.Window, request.ObjectKey, projects);
    }

    private JavaWindowInfo? FindInitialWindow(JavaValidationRequest request, IReadOnlyList<JavaRecordingProject> projects)
    {
        return FindInitialWindow(request.Window, request.ObjectKey, projects);
    }

    private JavaWindowInfo? FindInitialWindow(JavaWindowSelector? requestedWindow, string? objectKey, IReadOnlyList<JavaRecordingProject> projects)
    {
        var windows = GetWindows();
        if (requestedWindow is not null)
        {
            var selected = FindWindow(windows, requestedWindow);
            if (selected is not null) return selected;
        }

        if (projects.Count > 0 && !string.IsNullOrWhiteSpace(objectKey))
        {
            var entry = projects
                .SelectMany(project => project.Repository)
                .LastOrDefault(x => string.Equals(x.ObjectKey, objectKey, StringComparison.OrdinalIgnoreCase));
            if (entry is not null)
            {
                var scope = !string.IsNullOrWhiteSpace(entry.WindowKey)
                    ? projects.SelectMany(project => project.Windows).LastOrDefault(x => string.Equals(x.WindowKey, entry.WindowKey, StringComparison.OrdinalIgnoreCase))
                    : null;
                if (scope is not null)
                {
                    var scoped = FindWindow(windows, scope);
                    if (scoped is not null) return scoped;
                }

                var recorded = FindWindow(windows, new JavaWindowSelector(
                    entry.WindowHwndDisplay,
                    entry.WindowTitle,
                    entry.WindowClassName,
                    entry.WindowProcessId == 0 ? null : entry.WindowProcessId,
                    entry.WindowVmId == 0 ? null : entry.WindowVmId,
                    ExactTitle: true));
                if (recorded is not null) return recorded;
            }
        }

        foreach (var scope in projects.SelectMany(project => project.Windows))
        {
            var scoped = FindWindow(windows, scope);
            if (scoped is not null) return scoped;
        }

        return windows.FirstOrDefault();
    }

    private sealed record LaunchPlan(
        ProcessStartInfo StartInfo,
        string DisplayArguments,
        bool CanScopeToLaunchedProcess);

    private DriverResult ValidateElementCore(JavaDriverSession session, JavaValidationRequest request)
    {
        var routed = RouteSessionWindow(session, request.ObjectKey, request.Window, request.AutoSwitchWindow);
        if (!routed.Success)
        {
            return Ok("Validation completed. Requested window/modal was not found.", session.Id, CreateMissingValidation(routed.Message));
        }

        if (request.RefreshTree || session.Root is null)
        {
            var refresh = RefreshSessionTree(session);
            if (!refresh.Success) return refresh;
        }

        var resolution = ResolveNodeWithRetry(session, request.ObjectKey, request.Locator, request.ResolutionPolicy);
        if (!resolution.Success || resolution.Node is null)
        {
            return Ok("Validation completed. Element was not found.", session.Id, CreateMissingValidation(resolution.Message));
        }

        var node = resolution.Node;
        var locator = LocatorGenerator.GenerateLocator(node);
        var text = "";
        try
        {
            text = _automation.GetText(node) ?? "";
        }
        catch (Exception ex)
        {
            _logger.Debug($"Validation text read failed for {node.DisplayName}: {ex.Message}");
        }

        var bounds = new ElementBounds(node.X, node.Y, node.Width, node.Height);
        var hasPositiveBounds = node.Width > 0 && node.Height > 0;
        var validation = new JavaElementValidationDto(
            Exists: true,
            IsVisible: HasState(node, "visible") && hasPositiveBounds,
            IsShowing: HasState(node, "showing") && hasPositiveBounds,
            IsEnabled: HasState(node, "enabled"),
            IsFocusable: HasState(node, "focusable"),
            IsSelected: HasState(node, "selected"),
            HasText: !string.IsNullOrEmpty(text),
            TextMatches: string.IsNullOrEmpty(request.ExpectedText) || text.Contains(request.ExpectedText, StringComparison.OrdinalIgnoreCase),
            DisplayName: node.DisplayName,
            Role: node.Role,
            Name: node.Name,
            States: string.IsNullOrWhiteSpace(node.StatesEnUs) ? node.States : node.StatesEnUs,
            Text: text,
            Bounds: bounds,
            Locator: locator,
            Actions: _automation.GetActions(node),
            Message: $"Resolved '{node.DisplayName}' for validation.");

        return Ok("Validation completed.", session.Id, validation);
    }

    private DriverResult FindElementsCore(JavaDriverSession session, JavaFindElementsRequest request)
    {
        var routed = RouteSessionWindow(session, request.ObjectKey, request.Window, request.AutoSwitchWindow);
        if (!routed.Success) return Ok("Find elements completed. Requested window/modal was not found.", session.Id, Array.Empty<JavaElementSnapshotDto>());

        if (request.RefreshTree || session.Root is null)
        {
            var refresh = RefreshSessionTree(session);
            if (!refresh.Success) return refresh;
        }

        if (session.Root is null) return Ok("Find elements completed. Tree is empty.", session.Id, Array.Empty<JavaElementSnapshotDto>());

        var entry = ResolveFindEntry(session, request.ObjectKey, request.Locator);
        var policy = (request.ResolutionPolicy ?? ResolutionPolicy.Default).Sanitize();
        var minimumScore = Math.Clamp(request.MinimumScore ?? policy.MinimumScore, 0, 500);
        var maxResults = Math.Clamp(request.MaxResults ?? policy.MaxCandidates, 1, 500);

        var matches = EnumerateNodes(session.Root)
            .Select(node => new { Node = node, Score = ScoreFindCandidate(node, entry, request.Locator) })
            .Where(candidate => entry is null && request.Locator is null || candidate.Score >= minimumScore)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Node.ObjectDepth)
            .ThenBy(candidate => candidate.Node.IndexInParent)
            .Take(maxResults)
            .Select(candidate => CreateSnapshot(candidate.Node, candidate.Score))
            .ToList();

        return Ok($"Find elements completed. {matches.Count} match(es) returned.", session.Id, matches);
    }

    private DriverResult FindChildElementsCore(JavaDriverSession session, JavaFindChildElementsRequest request)
    {
        var routed = RouteSessionWindow(session, request.ParentObjectKey, request.Window, request.AutoSwitchWindow);
        if (!routed.Success) return Ok("Find child elements completed. Requested window/modal was not found.", session.Id, Array.Empty<JavaElementSnapshotDto>());

        if (request.RefreshTree || session.Root is null)
        {
            var refresh = RefreshSessionTree(session);
            if (!refresh.Success) return refresh;
        }

        if (session.Root is null) return Ok("Find child elements completed. Tree is empty.", session.Id, Array.Empty<JavaElementSnapshotDto>());

        AccessibleNode parent;
        if (string.IsNullOrWhiteSpace(request.ParentObjectKey) && request.ParentLocator is null)
        {
            parent = session.Root;
        }
        else
        {
            var parentEntry = ResolveFindEntry(session, request.ParentObjectKey, request.ParentLocator);
            if (parentEntry is null && request.ParentLocator is null)
                return Ok("Find child elements completed. Parent repository object was not found.", session.Id, Array.Empty<JavaElementSnapshotDto>());

            var step = parentEntry is null
                ? null
                : new JavaRecordedStep { ObjectKey = parentEntry.ObjectKey, ObjectLocator = parentEntry.Locator };
            var candidateEntry = parentEntry ?? CreateRepositoryEntry(request.ParentLocator!, session.Window);
            var resolution = ResolveNodeWithRetry(session, candidateEntry.ObjectKey, candidateEntry.Locator, request.ResolutionPolicy);
            if (!resolution.Success || resolution.Node is null)
                return Ok($"Find child elements completed. Parent was not found: {resolution.Message}", session.Id, Array.Empty<JavaElementSnapshotDto>());
            parent = resolution.Node;
        }

        var maxDepth = request.MaxDepth is null ? int.MaxValue : Math.Clamp(request.MaxDepth.Value, 0, 100);
        var maxResults = Math.Clamp(request.MaxResults ?? 500, 1, 5000);
        var baseDepth = parent.ObjectDepth < 0 ? 0 : parent.ObjectDepth;

        var children = EnumerateNodes(parent)
            .Where(node => request.IncludeSelf || !ReferenceEquals(node, parent))
            .Where(node => maxDepth == int.MaxValue || ((node.ObjectDepth < 0 ? baseDepth : node.ObjectDepth) - baseDepth) <= maxDepth)
            .Take(maxResults)
            .Select(node => CreateSnapshot(node, 0))
            .ToList();

        return Ok($"Find child elements completed. {children.Count} descendant element(s) returned.", session.Id, children);
    }

    private DriverResult ExecuteActionCore(JavaDriverSession session, JavaActionRequest request, bool isOneShot)
    {
        var routed = RouteSessionWindow(session, request.ObjectKey, request.Window, request.AutoSwitchWindow);
        if (!routed.Success) return routed;

        if (!TryNormalizeAction(request.Action, out var action, out var actionError))
            return Fail(actionError, session.Id);

        if (action == JavaRecordedActionKind.CloseWindow)
        {
            var host = new ApiJavaActionHost(this, session.Window, request.PreferAccessibleAction, recordedStep: null);
            var closed = host.CloseWindow(null, out var closeMessage);
            if (!closed) return Fail($"Action '{request.Action}' failed for window '{session.Window.Title}': {closeMessage}", session.Id);

            return Ok($"Action '{action}' executed.", session.Id, new
            {
                mode = isOneShot ? "one-shot" : "session",
                session = ToSummary(session),
                action,
                window = JavaWindowDto.From(session.Window),
                message = closeMessage
            });
        }

        if (request.RefreshTree || session.Root is null)
        {
            var refresh = RefreshSessionTree(session);
            if (!refresh.Success) return refresh;
        }

        var resolution = ResolveNodeWithRetry(session, request.ObjectKey, request.Locator, request.ResolutionPolicy, action);
        if (!resolution.Success || resolution.Node is null)
            return Fail(resolution.Message, session.Id, resolution.Details);

        var node = resolution.Node;
        var recordedStep = FindRecordedStep(session, request.ObjectKey, action);

        var execution = _javaActions.Execute(
            action,
            node,
            request.Text ?? "",
            new ApiJavaActionHost(this, session.Window, request.PreferAccessibleAction, recordedStep));
        if (!execution.Success) return Fail($"Action '{request.Action}' failed for {node.DisplayName}: {execution.Message}", session.Id);

        return Ok($"Action '{action}' executed.", session.Id, new
        {
            mode = isOneShot ? "one-shot" : "session",
            session = ToSummary(session),
            action,
            element = new ResolvedElementDto(node.DisplayName, LocatorGenerator.GenerateLocator(node), _automation.GetActions(node)),
            message = execution.Message,
            text = execution.Text
        });
    }

    private DriverResult ExecuteNavigationCore(JavaDriverSession session, JavaNavigationRequest request)
    {
        var command = NormalizeNavigationCommand(request.Command, out var error);
        if (command is null)
            return Fail(error, session.Id);

        var count = Math.Clamp(request.Count, 1, 100);
        SetForegroundWindow(session.Window.Hwnd);
        Thread.Sleep(80);

        var sent = SendNavigationKeys(command.Value, count);
        if (sent <= 0)
            return Fail($"Could not send navigation command '{command.Value}'.", session.Id);

        _logger.Log($"API navigation command executed. SessionId={session.Id}, Window='{session.Window.Title}', Command={command.Value}, Count={count}.");
        return Ok($"Navigation command '{command.Value}' sent.", session.Id, new
        {
            command = command.Value,
            count,
            sent
        });
    }

    private static JavaElementValidationDto CreateMissingValidation(string message) => new(
        Exists: false,
        IsVisible: false,
        IsShowing: false,
        IsEnabled: false,
        IsFocusable: false,
        IsSelected: false,
        HasText: false,
        TextMatches: false,
        DisplayName: "",
        Role: "",
        Name: "",
        States: "",
        Text: "",
        Bounds: null,
        Locator: null,
        Actions: [],
        Message: message);

    private static bool HasState(AccessibleNode node, string state)
    {
        return ContainsState(node.States, state) || ContainsState(node.StatesEnUs, state);
    }

    private static bool ContainsState(string states, string state)
    {
        if (string.IsNullOrWhiteSpace(states)) return false;
        return states.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(x => string.Equals(x, state, StringComparison.OrdinalIgnoreCase));
    }

    private JavaObjectRepositoryEntry? ResolveFindEntry(JavaDriverSession session, string? objectKey, LocatorSuggestion? locator)
    {
        if (!string.IsNullOrWhiteSpace(objectKey))
        {
            var entry = session.Repository.LastOrDefault(x => string.Equals(x.ObjectKey, objectKey, StringComparison.OrdinalIgnoreCase));
            if (entry is not null) return entry;
        }

        return locator is null ? null : CreateRepositoryEntry(locator, session.Window);
    }

    private JavaElementSnapshotDto CreateSnapshot(AccessibleNode node, int score)
    {
        return new JavaElementSnapshotDto(
            node.DisplayName,
            node.Role,
            node.RoleEnUs,
            node.Name,
            node.VirtualAccessibleName,
            node.Description,
            node.States,
            node.StatesEnUs,
            node.IndexInParent,
            node.ObjectDepth,
            node.ChildrenCount,
            node.Path,
            LocatorGenerator.BuildIndexPath(node),
            LocatorGenerator.BuildXPath(node),
            node.Parent?.Role ?? "",
            node.Parent?.Name ?? "",
            node.IsTableLikeContainer,
            node.IsTableLikeRow,
            node.IsTableLikeCell,
            node.TableLikeKind,
            node.TableLikeContainerPath,
            node.TableLikeColumnHeader,
            node.TableLikeRowIndex,
            node.TableLikeColumnIndex,
            node.TableLikeRowCount,
            node.TableLikeColumnCount,
            node.IsFormsLikeScope,
            node.IsFormsViewportLikeContainer,
            node.FormsScopePath,
            node.FormsScopeRole,
            node.FormsScopeName,
            node.FormsViewportPath,
            node.FormsViewportRole,
            node.FormsViewportName,
            node.TextPreview,
            node.TextLetter,
            node.TextSelectionStartIndex,
            node.TextSelectionEndIndex,
            node.CurrentValue,
            new ElementBounds(node.X, node.Y, node.Width, node.Height),
            LocatorGenerator.GenerateLocator(node),
            _automation.GetActions(node),
            score);
    }

    private static int ScoreFindCandidate(AccessibleNode node, JavaObjectRepositoryEntry? entry, LocatorSuggestion? locator)
    {
        if (entry is null && locator is null) return 0;
        var score = 0;
        AddScore(ref score, TextEquals(node.RoleEnUs, locator?.RoleEnUs ?? entry?.RoleEnUs), 22);
        AddScore(ref score, TextEquals(node.Role, locator?.Role ?? entry?.Role), 20);
        AddScore(ref score, TextEquals(node.Name, locator?.Name ?? entry?.Name), 28);
        AddScore(ref score, TextEquals(node.VirtualAccessibleName, locator?.VirtualAccessibleName ?? entry?.VirtualAccessibleName), 26);
        AddScore(ref score, TextEquals(node.Description, locator?.Description ?? entry?.Description), 12);
        AddScore(ref score, TextEquals(node.TextPreview, locator?.TextPreview ?? entry?.Locator?.TextPreview), 8);
        AddScore(ref score, TextEquals(node.TextLetter, locator?.TextLetter ?? entry?.TextLetter), 6);
        AddScore(ref score, TextEquals(node.TextSelected, locator?.TextSelected ?? entry?.Locator?.TextSelected), 8);
        AddScore(ref score, TextEquals(node.Path, locator?.Path ?? entry?.Path), 35);
        AddScore(ref score, TextEquals(LocatorGenerator.BuildIndexPath(node), locator?.IndexPath ?? entry?.IndexPath), 35);
        AddScore(ref score, TextEquals(LocatorGenerator.BuildXPath(node), locator?.XPath ?? entry?.XPath), 30);
        AddScore(ref score, TextEquals(LocatorGenerator.BuildIndexXPath(node), locator?.IndexXPath ?? entry?.IndexXPath), 30);
        AddScore(ref score, TextEquals(node.Parent?.Role, locator?.ParentRole ?? entry?.ParentRole), 10);
        AddScore(ref score, TextEquals(node.Parent?.Name, locator?.ParentName ?? entry?.ParentName), 10);
        AddScore(ref score, TextEquals(node.TableLikeKind, locator?.TableLikeKind ?? entry?.TableLikeKind), 16);
        AddScore(ref score, TextEquals(node.TableLikeContainerPath, locator?.TableLikeContainerPath ?? entry?.TableLikeContainerPath), 20);
        AddScore(ref score, TextEquals(node.TableLikeColumnHeader, locator?.TableLikeColumnHeader ?? entry?.TableLikeColumnHeader), 12);
        AddScore(ref score, TextEquals(node.FormsScopePath, locator?.FormsScopePath ?? entry?.FormsScopePath), 22);
        AddScore(ref score, TextEquals(node.FormsScopeName, locator?.FormsScopeName ?? entry?.FormsScopeName), 10);
        AddScore(ref score, TextEquals(node.FormsViewportPath, locator?.FormsViewportPath ?? entry?.FormsViewportPath), 20);
        AddScore(ref score, TextEquals(node.FormsViewportName, locator?.FormsViewportName ?? entry?.FormsViewportName), 8);
        AddScore(ref score, TextEquals(node.TextPreview, locator?.TextPreview ?? entry?.Locator?.TextPreview), 12);
        AddScore(ref score, TextEquals(node.CurrentValue, locator?.CurrentValue ?? entry?.Locator?.CurrentValue), 14);

        var expectedDepth = locator?.ObjectDepth ?? entry?.ObjectDepth ?? -1;
        if (expectedDepth >= 0 && node.ObjectDepth == expectedDepth) score += 8;
        var expectedIndex = locator?.IndexInParent ?? entry?.IndexInParent ?? -1;
        if (expectedIndex >= 0 && node.IndexInParent == expectedIndex) score += 8;
        var expectedRow = locator?.TableLikeRowIndex ?? entry?.TableLikeRowIndex ?? -1;
        if (expectedRow >= 0 && node.TableLikeRowIndex == expectedRow) score += 18;
        var expectedColumn = locator?.TableLikeColumnIndex ?? entry?.TableLikeColumnIndex ?? -1;
        if (expectedColumn >= 0 && node.TableLikeColumnIndex == expectedColumn) score += 18;

        if (locator?.Bounds is not null && BoundsClose(node, locator.Bounds)) score += 8;
        else if (entry is not null && BoundsClose(node, new ElementBounds(entry.X, entry.Y, entry.Width, entry.Height))) score += 8;

        return score;
    }

    private static void AddScore(ref int score, bool matched, int value)
    {
        if (matched) score += value;
    }

    private static bool TextEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool BoundsClose(AccessibleNode node, ElementBounds bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || node.Width <= 0 || node.Height <= 0) return false;
        return Math.Abs(node.X - bounds.X)
               + Math.Abs(node.Y - bounds.Y)
               + Math.Abs(node.Width - bounds.Width)
               + Math.Abs(node.Height - bounds.Height) <= 24;
    }

    private static IEnumerable<AccessibleNode> EnumerateNodes(AccessibleNode root)
    {
        var stack = new Stack<AccessibleNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    private DriverResult RouteSessionWindow(JavaDriverSession session, string? objectKey, JavaWindowSelector? selector, bool autoSwitch)
    {
        if (selector is not null)
        {
            var selected = FindRelatedWindow(session, selector, attempts: autoSwitch ? 6 : 1, retryDelayMs: autoSwitch ? 220 : 0);
            if (selected is null) return Fail("Requested Java window/modal was not found for this session.", session.Id);
            if (selected.Hwnd != session.Window.Hwnd) SwitchSessionWindow(session, selected);
            return Ok("Session routed to requested window.", session.Id, ToSummary(session));
        }

        if (!autoSwitch || string.IsNullOrWhiteSpace(objectKey)) return Ok("Window routing not required.", session.Id);

        var entry = session.Repository.FirstOrDefault(x => string.Equals(x.ObjectKey, objectKey, StringComparison.OrdinalIgnoreCase));
        var recordedStep = FindRecordedStep(session, objectKey, actionKind: null);
        if (entry is null && recordedStep is null) return Ok("Repository object not loaded; window routing skipped.", session.Id);

        var scope = entry is not null && !string.IsNullOrWhiteSpace(entry.WindowKey)
            ? session.Windows.FirstOrDefault(x => string.Equals(x.WindowKey, entry.WindowKey, StringComparison.OrdinalIgnoreCase))
            : null;
        var stepScope = FindRecordedWindowScope(session, recordedStep);

        if (stepScope is not null && WindowMatchesScope(stepScope, session.Window) && ScopeProcessMatches(stepScope, session.Window))
            return Ok("Session already uses recorded step window scope.", session.Id);
        if (scope is not null && WindowMatchesScope(scope, session.Window) && ScopeProcessMatches(scope, session.Window))
            return Ok("Session already uses recorded object window scope.", session.Id);
        if (recordedStep is not null && RecordedStepMatchesWindow(recordedStep, session.Window))
            return Ok("Session already uses recorded step window.", session.Id);
        if (entry is not null && scope is null && EntryMatchesWindow(entry, session.Window))
            return Ok("Session already uses recorded object window.", session.Id);

        var recordedWindow = stepScope is not null
            ? FindRelatedWindow(session, stepScope, attempts: 6, retryDelayMs: 220)
            : scope is not null
                ? FindRelatedWindow(session, scope, attempts: 6, retryDelayMs: 220)
                : FindRelatedWindow(session, BuildRecordedWindowSelector(entry, recordedStep), attempts: 6, retryDelayMs: 220);

        if (recordedWindow is null)
            return Fail($"Could not find recorded window/modal '{stepScope?.FriendlyName ?? scope?.FriendlyName ?? recordedStep?.WindowTitle ?? entry?.WindowTitle}' for object '{objectKey}'.", session.Id, new
            {
                objectKey,
                entry?.WindowKey,
                recordedStepWindowKey = recordedStep?.WindowKey,
                expectedWindow = scope,
                expectedRecordedWindow = stepScope,
                discoveredWindows = GetRelatedWindows(session).Select(JavaWindowDto.From).ToList()
            });

        SwitchSessionWindow(session, recordedWindow);
        return Ok("Session auto-switched to recorded window/modal.", session.Id, ToSummary(session));
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

    private JavaWindowInfo? FindRelatedWindow(JavaDriverSession session, JavaWindowSelector selector, int attempts = 1, int retryDelayMs = 0)
    {
        JavaWindowInfo? match = null;
        for (var attempt = 1; attempt <= Math.Max(1, attempts); attempt++)
        {
            var related = GetRelatedWindows(session);
            match = FindWindow(related, selector) ?? FindWindow(GetWindows(), selector);
            if (match is not null || attempt >= attempts) break;
            if (retryDelayMs > 0) Thread.Sleep(retryDelayMs);
        }

        return match;
    }

    private JavaWindowInfo? FindRelatedWindow(JavaDriverSession session, JavaWindowLocator scope, int attempts = 1, int retryDelayMs = 0)
    {
        JavaWindowInfo? match = null;
        for (var attempt = 1; attempt <= Math.Max(1, attempts); attempt++)
        {
            var related = GetRelatedWindows(session);
            match = FindWindow(related, scope) ?? FindWindow(GetWindows(), scope);
            if (match is not null || attempt >= attempts) break;
            if (retryDelayMs > 0) Thread.Sleep(retryDelayMs);
        }

        return match;
    }

    private static JavaWindowInfo? FindWindow(IEnumerable<JavaWindowInfo> windows, JavaWindowSelector selector)
    {
        var candidates = windows.ToList();
        if (candidates.Count == 0) return null;

        var ranked = candidates
            .Select(window => new
            {
                Window = window,
                Score = ScoreWindowSelector(window, selector)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Window.Title)
            .ToList();

        if (ranked.Count == 0) return null;

        var best = ranked[0];
        if (best.Score <= 0)
        {
            var fallback = candidates.FirstOrDefault(window =>
                !string.IsNullOrWhiteSpace(selector.Title)
                    ? (selector.ExactTitle
                        ? string.Equals(window.Title, selector.Title, StringComparison.OrdinalIgnoreCase)
                        : window.Title.Contains(selector.Title, StringComparison.OrdinalIgnoreCase))
                    : !string.IsNullOrWhiteSpace(selector.ClassName) && string.Equals(window.ClassName, selector.ClassName, StringComparison.OrdinalIgnoreCase));
            return fallback ?? candidates.FirstOrDefault();
        }

        return best.Window;
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
        if (!string.IsNullOrWhiteSpace(entry.WindowClassName) &&
            !string.Equals(entry.WindowClassName, window.ClassName, StringComparison.OrdinalIgnoreCase)) return false;
        return string.IsNullOrWhiteSpace(entry.WindowTitle) ||
               string.Equals(entry.WindowTitle, window.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RecordedStepMatchesWindow(JavaRecordedStep step, JavaWindowInfo window)
    {
        if (!string.IsNullOrWhiteSpace(step.WindowHwndDisplay) &&
            string.Equals(step.WindowHwndDisplay, window.HwndDisplay, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrWhiteSpace(step.WindowClassName) &&
            !string.Equals(step.WindowClassName, window.ClassName, StringComparison.OrdinalIgnoreCase)) return false;
        return string.IsNullOrWhiteSpace(step.WindowTitle) ||
               string.Equals(step.WindowTitle, window.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static JavaWindowSelector BuildRecordedWindowSelector(JavaObjectRepositoryEntry? entry, JavaRecordedStep? step)
    {
        return new JavaWindowSelector(
            Hwnd: FirstNonEmpty(step?.WindowHwndDisplay, entry?.WindowHwndDisplay),
            Title: FirstNonEmpty(step?.WindowTitle, entry?.WindowTitle),
            ClassName: FirstNonEmpty(step?.WindowClassName, entry?.WindowClassName),
            ProcessId: null,
            VmId: null,
            ExactTitle: true);
    }

    private JavaWindowLocator? FindRecordedWindowScope(JavaDriverSession session, JavaRecordedStep? step)
    {
        if (step is null) return null;
        if (!string.IsNullOrWhiteSpace(step.WindowKey))
        {
            var byKey = session.Windows.FirstOrDefault(x => string.Equals(x.WindowKey, step.WindowKey, StringComparison.OrdinalIgnoreCase));
            if (byKey is not null) return byKey;
        }

        return session.Windows.FirstOrDefault(x =>
            string.Equals(x.Title, step.WindowTitle, StringComparison.Ordinal)
            && string.Equals(x.ClassName, step.WindowClassName, StringComparison.Ordinal));
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
        return true;
    }

    private static int ScoreWindowSelector(JavaWindowInfo window, JavaWindowSelector selector)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(selector.Title))
        {
            if (selector.ExactTitle
                ? string.Equals(window.Title, selector.Title, StringComparison.OrdinalIgnoreCase)
                : window.Title.Contains(selector.Title, StringComparison.OrdinalIgnoreCase))
            {
                score += 80;
            }
        }

        if (!string.IsNullOrWhiteSpace(selector.ClassName) &&
            string.Equals(window.ClassName, selector.ClassName, StringComparison.OrdinalIgnoreCase))
        {
            score += 30;
        }

        if (selector.ProcessId is not null && window.ProcessId == selector.ProcessId.Value)
            score += 15;
        if (selector.VmId is not null && window.VmId == selector.VmId.Value)
            score += 15;

        if (!string.IsNullOrWhiteSpace(selector.Hwnd))
        {
            var normalized = selector.Hwnd.Trim();
            if (string.Equals(window.HwndDisplay, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(window.Hwnd.ToInt64().ToString("X"), normalized.TrimStart('0', 'x', 'X'), StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
        }

        return score;
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
        ResolutionPolicy? requestedPolicy,
        JavaRecordedActionKind? actionKind = null)
    {
        var policy = (requestedPolicy ?? ResolutionPolicy.Default).Sanitize();
        var started = DateTime.UtcNow;
        var attempt = 0;
        ResolveResult? last = null;
        var refreshedAfterFailure = false;

        while (true)
        {
            attempt++;
            last = ResolveNode(session, objectKey, locator, policy, actionKind);
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
        ResolutionPolicy policy,
        JavaRecordedActionKind? actionKind = null)
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

        var step = locator is null
            ? FindRecordedStep(session, objectKey, actionKind)
            : new JavaRecordedStep
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

    private static JavaRecordedStep? FindRecordedStep(JavaDriverSession session, string? objectKey, JavaRecordedActionKind? actionKind)
    {
        if (string.IsNullOrWhiteSpace(objectKey)) return null;
        var steps = session.Steps
            .Where(step => string.Equals(step.ObjectKey, objectKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (steps.Count == 0) return null;

        if (actionKind is not null)
        {
            var exact = steps.FirstOrDefault(step => step.ActionKind == actionKind.Value);
            if (exact is not null) return exact;
        }

        return steps.FirstOrDefault();
    }

    private JavaObjectRepositoryEntry CreateRepositoryEntry(LocatorSuggestion locator, JavaWindowInfo window)
    {
        var bounds = locator.Bounds ?? new ElementBounds(0, 0, 0, 0);
        return new JavaObjectRepositoryEntry
        {
            ObjectKey = "inline_locator",
            FriendlyName = FirstNonEmpty(locator.Name, locator.VirtualAccessibleName, locator.Description, locator.Role, "inline_locator"),
            CapturedAtUtc = DateTime.UtcNow,
            WindowKey = _repositoryService.CreateWindowKey(window),
            WindowHwndDisplay = window.HwndDisplay,
            WindowTitle = window.Title,
            WindowClassName = window.ClassName,
            WindowProcessId = window.ProcessId,
            WindowVmId = window.VmId,
            Engine = FirstNonEmpty(locator.Engine, "java-access-bridge"),
            Locator = locator,
            LocatorJson = JsonExportService.Serialize(locator),
            Role = locator.Role ?? "",
            RoleEnUs = locator.RoleEnUs ?? "",
            Name = locator.Name ?? "",
            VirtualAccessibleName = locator.VirtualAccessibleName ?? "",
            Description = locator.Description ?? "",
            States = locator.States ?? "",
            StatesEnUs = locator.StatesEnUs ?? "",
            Path = locator.Path ?? "",
            IndexPath = locator.IndexPath ?? "",
            XPath = locator.XPath ?? "",
            IndexXPath = locator.IndexXPath ?? "",
            SemanticXPath = locator.SemanticXPath ?? "",
            ParentRole = locator.ParentRole ?? "",
            ParentName = locator.ParentName ?? "",
            IsTableLikeContainer = locator.IsTableLikeContainer,
            IsTableLikeRow = locator.IsTableLikeRow,
            IsTableLikeCell = locator.IsTableLikeCell,
            TableLikeKind = locator.TableLikeKind ?? "",
            TableLikeContainerPath = locator.TableLikeContainerPath ?? "",
            TableLikeColumnHeader = locator.TableLikeColumnHeader ?? "",
            TableLikeRowIndex = locator.TableLikeRowIndex,
            TableLikeColumnIndex = locator.TableLikeColumnIndex,
            TableLikeRowCount = locator.TableLikeRowCount,
            TableLikeColumnCount = locator.TableLikeColumnCount,
            IsFormsLikeScope = locator.IsFormsLikeScope,
            IsFormsViewportLikeContainer = locator.IsFormsViewportLikeContainer,
            FormsScopePath = locator.FormsScopePath ?? "",
            FormsScopeRole = locator.FormsScopeRole ?? "",
            FormsScopeName = locator.FormsScopeName ?? "",
            FormsViewportPath = locator.FormsViewportPath ?? "",
            FormsViewportRole = locator.FormsViewportRole ?? "",
            FormsViewportName = locator.FormsViewportName ?? "",
            IndexInParent = locator.IndexInParent,
            ObjectDepth = locator.ObjectDepth,
            ChildrenCount = locator.ChildrenCount,
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            HasManagedDescendantAncestor = locator.HasManagedDescendantAncestor,
            ActionNames = locator.ActionNames?.ToList() ?? []
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }

    private bool PhysicalClick(JavaWindowInfo window, AccessibleNode node, int count, JavaRecordedStep? step)
    {
        SetForegroundWindow(window.Hwnd);
        Thread.Sleep(70);

        if (TryGetRecordedPlaybackPoint(window, step, out var recordedX, out var recordedY))
            return PhysicalClickAt(recordedX, recordedY, count, node.DisplayName, "recorded window-relative point");

        var visualNode = node;
        while (visualNode.Parent is not null && !HasUsableBounds(visualNode))
        {
            visualNode = visualNode.Parent;
        }

        if (!HasUsableBounds(visualNode)) return false;

        var x = visualNode.X + visualNode.Width / 2;
        var y = visualNode.Y + visualNode.Height / 2;
        return PhysicalClickAt(x, y, count, node.DisplayName, ReferenceEquals(visualNode, node) ? "element center" : $"ancestor center ({visualNode.DisplayName})");
    }

    private bool PhysicalClickAt(int x, int y, int count, string displayName, string source)
    {
        if (!SetCursorPos(x, y)) return false;

        for (var i = 0; i < count; i++)
        {
            MouseEvent(MouseLeftDown, 0, 0, 0, UIntPtr.Zero);
            MouseEvent(MouseLeftUp, 0, 0, 0, UIntPtr.Zero);
            if (i + 1 < count) Thread.Sleep(100);
        }

        _logger.Log($"API physical click executed on {displayName} at ({x}, {y}), Count={count}, Source={source}.");
        return true;
    }

    private static bool HasUsableBounds(AccessibleNode node)
    {
        return node.Width > 0 && node.Height > 0 && !(node.X == 0 && node.Y == 0);
    }

    private static bool TryGetRecordedPlaybackPoint(JavaWindowInfo window, JavaRecordedStep? step, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (step?.WindowOffsetX is null || step.WindowOffsetY is null) return false;
        if (!User32Native.GetWindowRect(window.Hwnd, out var rect)) return false;

        x = rect.Left + step.WindowOffsetX.Value;
        y = rect.Top + step.WindowOffsetY.Value;
        return x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;
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

    private sealed class ApiJavaActionHost(JavaDriverService owner, JavaWindowInfo window, bool allowSemanticFallback, JavaRecordedStep? recordedStep) : IJavaActionExecutionHost
    {
        public bool Focus(AccessibleNode node, out string message)
        {
            var success = owner._automation.Focus(node);
            message = success ? $"Focus requested successfully for {node.DisplayName}." : $"Focus request failed for {node.DisplayName}.";
            return success;
        }

        public bool CloseWindow(AccessibleNode? node, out string message)
        {
            var success = PostMessage(window.Hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
            message = success ? $"Close requested for window '{window.Title}'." : $"Close request failed for window '{window.Title}'.";
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
            var success = owner.PhysicalClick(window, node, count, recordedStep);
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
            "closewindow" or "close" or "windowclose" => JavaRecordedActionKind.CloseWindow,
            "settext" or "set" => JavaRecordedActionKind.SetText,
            "typetext" or "type" => JavaRecordedActionKind.TypeText,
            "gettext" or "text" => JavaRecordedActionKind.GetText,
            "assertvisible" or "isvisible" or "visible" => JavaRecordedActionKind.AssertVisible,
            _ => NoMatch()
        };

        if (matched) return true;
        error = $"Unsupported action '{action}'. Supported actions: focus, click, doubleClick, closeWindow, setText, typeText, getText, assertVisible.";
        return false;

        JavaRecordedActionKind NoMatch()
        {
            matched = false;
            return default;
        }
    }

    private static JavaNavigationCommand? NormalizeNavigationCommand(string command, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(command))
        {
            error = "Navigation command is required.";
            return null;
        }

        return command.Trim().Replace("-", "", StringComparison.OrdinalIgnoreCase).Replace("_", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant() switch
        {
            "pagedown" or "pageforward" => JavaNavigationCommand.PageDown,
            "pageup" or "pageback" => JavaNavigationCommand.PageUp,
            "down" => JavaNavigationCommand.Down,
            "up" => JavaNavigationCommand.Up,
            "home" => JavaNavigationCommand.Home,
            "end" => JavaNavigationCommand.End,
            _ => null
        };
    }

    private int SendNavigationKeys(JavaNavigationCommand command, int count)
    {
        var key = command switch
        {
            JavaNavigationCommand.PageDown => 0x22,
            JavaNavigationCommand.PageUp => 0x21,
            JavaNavigationCommand.Down => 0x28,
            JavaNavigationCommand.Up => 0x26,
            JavaNavigationCommand.Home => 0x24,
            JavaNavigationCommand.End => 0x23,
            _ => 0
        };

        if (key == 0) return 0;

        var inputs = new List<NativeInput>(count * 2);
        for (var i = 0; i < count; i++)
        {
            inputs.Add(NativeInput.Key((ushort)key, false));
            inputs.Add(NativeInput.Key((ushort)key, true));
        }

        return inputs.Count == 0 ? 0 : (int)SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<NativeInput>()) / 2;
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
        entry.FormsScopePath,
        entry.FormsScopeRole,
        entry.FormsScopeName,
        entry.FormsViewportPath,
        entry.FormsViewportRole,
        entry.FormsViewportName,
        entry.TextPreview,
        entry.TextLetter,
        entry.TextSelectionStartIndex,
        entry.TextSelectionEndIndex,
        bounds = new ElementBounds(entry.X, entry.Y, entry.Width, entry.Height),
        entry.Locator
    };

    private static DriverResult Ok(string message, string? sessionId = null, object? data = null) => new(true, message, sessionId, data);
    private static DriverResult Fail(string message, string? sessionId = null, object? data = null) => new(false, message, sessionId, data);

    public void Dispose() => _bridge.Dispose();

    private sealed record ResolveResult(bool Success, string Message, AccessibleNode? Node, ResolutionResult? Details = null);
    private sealed record EphemeralSessionResult(bool Success, JavaDriverSession? Session, string Message);

    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint WmClose = 0x0010;
    private const uint KeyEventUnicode = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;
    private enum JavaNavigationCommand
    {
        PageDown,
        PageUp,
        Down,
        Up,
        Home,
        End
    }

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

        public static NativeInput Key(ushort virtualKey, bool keyUp) => new()
        {
            Type = 1,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = keyUp ? KeyEventKeyUp : 0
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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
}

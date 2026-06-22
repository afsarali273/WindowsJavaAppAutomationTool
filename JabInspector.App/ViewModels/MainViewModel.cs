using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using JabInspector.App.Helpers;
using JabInspector.Core.Diagnostics;
using JabInspector.Core.Models;
using JabInspector.Core.Services;
using WinInspector.Core.Models;
using WinInspector.Core.Services;

namespace JabInspector.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly InspectorLogger _logger = new();
    private readonly AccessBridgeService _bridge;
    private readonly AutomationService _automation;
    private readonly WindowsWindowDiscoveryService _windowsDiscovery = new();
    private readonly WindowsAutomationRouter _windowsRouter = new();
    private readonly WindowsAutomationActionService _windowsActions = new();

    private JavaWindowViewModel? _selectedJavaWindow;
    private WindowsWindowViewModel? _selectedWindowsWindow;
    private AccessibleNode? _selectedNode;
    private WindowsAutomationNode? _selectedWindowsNode;
    private AccessibleNode? _root;
    private WindowsAutomationNode? _windowsRoot;
    private InspectorMode _selectedMode = InspectorMode.Java;
    private string _locatorPreview = "Select an element to generate a resilient locator.";
    private string _status = "Ready to inspect";
    private bool _busy;
    private readonly List<long> _hoverContexts = [];
    private readonly List<AccessibleNode> _dynamicHoverNodes = [];
    private readonly Dictionary<long, AccessibleNode> _nodesByContext = [];
    private string _automationOutput = "Automation results will appear here.";
    private string _supportedActions = "Select an accessibility node.";
    private string _settingsSummary = "Review Java Access Bridge requirements and common setup actions.";
    private string _settingsActionResult = "No settings action has been run yet.";
    private string _jabswitchPath = "(not found)";
    private string _bridgeDllPath = "(not found)";
    private string _javaHomePath = "(not set)";
    private string _accessibilityRegistrationPath = "(not found)";

    public ObservableCollection<JavaWindowViewModel> JavaWindows { get; } = [];
    public ObservableCollection<WindowsWindowViewModel> WindowsDesktopWindows { get; } = [];
    public ObservableCollection<AccessibleNode> Tree { get; } = [];
    public ObservableCollection<WindowsAutomationNode> WindowsTree { get; } = [];
    public ObservableCollection<string> Logs { get; } = [];
    public ObservableCollection<RequirementCheckViewModel> RequirementChecks { get; } = [];
    public IReadOnlyList<InspectorMode> AvailableModes { get; } = Enum.GetValues<InspectorMode>();

    public RelayCommand RefreshWindowsCommand { get; }
    public RelayCommand AttachCommand { get; }
    public RelayCommand CopyLocatorCommand { get; }
    public RelayCommand RunDiagnosticsCommand { get; }
    public RelayCommand RefreshRequirementsCommand { get; }
    public RelayCommand EnableJavaAccessBridgeCommand { get; }
    public RelayCommand DisableJavaAccessBridgeCommand { get; }
    public RelayCommand OpenEaseOfAccessCommand { get; }

    public MainViewModel()
    {
        _bridge = new AccessBridgeService(_logger);
        _automation = new AutomationService(_bridge, _logger);
        _logger.MessageLogged += message => App.Current.Dispatcher.Invoke(() =>
        {
            Logs.Add(message);
            while (Logs.Count > 500) Logs.RemoveAt(0);
        });

        RefreshWindowsCommand = new RelayCommand(RefreshWindows, () => !IsBusy);
        AttachCommand = new RelayCommand(Attach, () => !IsBusy && (IsJavaMode ? SelectedJavaWindow is not null : SelectedWindowsWindow is not null));
        CopyLocatorCommand = new RelayCommand(CopyLocator, () => HasSelection);
        RunDiagnosticsCommand = new RelayCommand(RunDiagnostics);
        RefreshRequirementsCommand = new RelayCommand(RefreshRequirements);
        EnableJavaAccessBridgeCommand = new RelayCommand(EnableJavaAccessBridge, () => HasJabSwitch);
        DisableJavaAccessBridgeCommand = new RelayCommand(DisableJavaAccessBridge, () => HasJabSwitch);
        OpenEaseOfAccessCommand = new RelayCommand(OpenEaseOfAccess);

        RunDiagnostics();
        RefreshRequirements();
    }

    public JavaWindowViewModel? SelectedJavaWindow
    {
        get => _selectedJavaWindow;
        set
        {
            if (!Set(ref _selectedJavaWindow, value)) return;
            OnPropertyChanged(nameof(SelectedWindowItem));
            AttachCommand.RaiseCanExecuteChanged();
        }
    }

    public WindowsWindowViewModel? SelectedWindowsWindow
    {
        get => _selectedWindowsWindow;
        set
        {
            if (!Set(ref _selectedWindowsWindow, value)) return;
            OnPropertyChanged(nameof(SelectedWindowItem));
            RefreshPropertySurface();
            AttachCommand.RaiseCanExecuteChanged();
        }
    }

    public object? SelectedWindowItem
    {
        get => IsJavaMode ? SelectedJavaWindow : SelectedWindowsWindow;
        set
        {
            if (value is JavaWindowViewModel javaWindow) SelectedJavaWindow = javaWindow;
            else if (value is WindowsWindowViewModel windowsWindow) SelectedWindowsWindow = windowsWindow;
            else if (value is null)
            {
                SelectedJavaWindow = null;
                SelectedWindowsWindow = null;
            }
        }
    }

    public AccessibleNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (!Set(ref _selectedNode, value)) return;
            if (IsJavaMode)
            {
                LocatorPreview = value is null ? "Select an element to generate a resilient locator." : JsonExportService.Serialize(LocatorGenerator.GenerateLocator(value));
                SupportedActions = value is null ? "Select an accessibility node." : "Open this tab to discover semantic actions.";
            }
            CopyLocatorCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasSelection));
            RefreshPropertySurface();
        }
    }

    public WindowsAutomationNode? SelectedWindowsNode
    {
        get => _selectedWindowsNode;
        set
        {
            if (!Set(ref _selectedWindowsNode, value)) return;
            if (IsWindowsMode)
            {
                LocatorPreview = value is null ? "Select an element to generate a resilient locator." : BuildWindowsLocatorPreview(value);
                SupportedActions = value is null ? "Select a Windows automation node." : $"Resolved through {value.BackendKind}. Focus, click, type, set text, and get text are available where the selected backend exposes them.";
            }
            CopyLocatorCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasSelection));
            RefreshPropertySurface();
        }
    }

    public InspectorMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (!Set(ref _selectedMode, value)) return;
            ClearSelectionsForModeChange();
            OnPropertyChanged(nameof(IsJavaMode));
            OnPropertyChanged(nameof(IsWindowsMode));
            OnPropertyChanged(nameof(CurrentWindowItems));
            OnPropertyChanged(nameof(CurrentTreeItems));
            OnPropertyChanged(nameof(SelectedWindowItem));
            OnPropertyChanged(nameof(WindowPaneTitle));
            OnPropertyChanged(nameof(WindowPaneHint));
            OnPropertyChanged(nameof(RefreshButtonLabel));
            OnPropertyChanged(nameof(AttachButtonLabel));
            OnPropertyChanged(nameof(ShellTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
            OnPropertyChanged(nameof(WindowItemCount));
            OnPropertyChanged(nameof(JavaSelectionVisibility));
            OnPropertyChanged(nameof(WindowsSelectionVisibility));
            AttachCommand.RaiseCanExecuteChanged();
            CopyLocatorCommand.RaiseCanExecuteChanged();
            RefreshPropertySurface();
            Status = value == InspectorMode.Java ? "Java mode selected" : "Windows mode selected";
        }
    }

    public AccessibleNode? Root => _root;
    public WindowsAutomationNode? WindowsRoot => _windowsRoot;
    public JavaWindowInfo? CurrentWindow => SelectedJavaWindow?.Model;
    public bool HasSelection => IsJavaMode ? SelectedNode is not null : SelectedWindowsNode is not null;
    public bool IsJavaMode => SelectedMode == InspectorMode.Java;
    public bool IsWindowsMode => SelectedMode == InspectorMode.Windows;
    public object CurrentWindowItems => IsJavaMode ? JavaWindows : WindowsDesktopWindows;
    public object CurrentTreeItems => IsJavaMode ? Tree : WindowsTree;
    public int WindowItemCount => IsJavaMode ? JavaWindows.Count : WindowsDesktopWindows.Count;

    public string LocatorPreview { get => _locatorPreview; private set => Set(ref _locatorPreview, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string AutomationOutput { get => _automationOutput; private set => Set(ref _automationOutput, value); }
    public string SupportedActions { get => _supportedActions; private set => Set(ref _supportedActions, value); }
    public string ShellTitle => IsJavaMode ? "Java Access Bridge Inspector" : "Desktop Windows Inspector";
    public string HeaderSubtitle => IsJavaMode ? "Inspect hierarchy, properties, and automation targets" : "Shared shell for Windows UIA, Win32, and future FlaUI inspection";
    public string WindowPaneTitle => IsJavaMode ? "JAVA WINDOWS" : "DESKTOP WINDOWS";
    public string WindowPaneHint => IsJavaMode ? "Tip: start the target Swing or AWT app, then refresh." : "Tip: select a native window, attach, and inspect it with UIA and Win32 fallback.";
    public string RefreshButtonLabel => IsJavaMode ? "Refresh windows" : "Refresh desktop";
    public string AttachButtonLabel => IsJavaMode ? "Attach and inspect" : "Inspect selection";

    public string SelectedDisplayName => IsJavaMode
        ? SelectedNode?.DisplayName ?? "(no selection)"
        : SelectedWindowsNode?.DisplayName ?? SelectedWindowsWindow?.Model.DisplayName ?? "(no selection)";
    public string PropertyNameValue => IsJavaMode ? SelectedNode?.Name ?? "" : SelectedWindowsNode?.Name ?? "";
    public string PropertyDescriptionValue => IsJavaMode ? SelectedNode?.Description ?? "" : SelectedWindowsWindow?.Model.DisplayName ?? "";
    public string PropertyRoleValue => IsJavaMode ? SelectedNode?.Role ?? "" : SelectedWindowsNode?.Role ?? "";
    public string PropertyRoleSecondaryValue => IsJavaMode ? SelectedNode?.RoleEnUs ?? "" : SelectedWindowsNode?.BackendKind.ToString() ?? "";
    public string PropertyStatesValue => IsJavaMode ? SelectedNode?.States ?? "" : SelectedWindowsNode?.ClassName ?? "";
    public string PropertyStatesSecondaryValue => IsJavaMode ? SelectedNode?.StatesEnUs ?? "" : SelectedWindowsNode?.AutomationId ?? "";
    public string PropertyBoundsValue => IsJavaMode
        ? FormatJavaBounds(SelectedNode)
        : FormatWindowsBounds(SelectedWindowsNode);
    public string PropertyIndexValue => IsJavaMode ? $"{SelectedNode?.IndexInParent ?? -1}" : $"{SelectedWindowsNode?.IndexInParent ?? -1}";
    public string PropertyChildrenValue => IsJavaMode ? $"{SelectedNode?.ChildrenCount ?? 0}" : $"{SelectedWindowsNode?.Children.Count ?? 0}";
    public string PropertyRawIdsValue => IsJavaMode
        ? FormatJavaIds(SelectedNode)
        : FormatWindowsIds(SelectedWindowsNode);
    public Visibility JavaSelectionVisibility => IsJavaMode ? Visibility.Visible : Visibility.Collapsed;
    public Visibility WindowsSelectionVisibility => IsWindowsMode ? Visibility.Visible : Visibility.Collapsed;

    public string SettingsSummary { get => _settingsSummary; private set => Set(ref _settingsSummary, value); }
    public string SettingsActionResult { get => _settingsActionResult; private set => Set(ref _settingsActionResult, value); }
    public string JabSwitchPath { get => _jabswitchPath; private set => Set(ref _jabswitchPath, value); }
    public string BridgeDllPath { get => _bridgeDllPath; private set => Set(ref _bridgeDllPath, value); }
    public string JavaHomePath { get => _javaHomePath; private set => Set(ref _javaHomePath, value); }
    public string AccessibilityRegistrationPath { get => _accessibilityRegistrationPath; private set => Set(ref _accessibilityRegistrationPath, value); }
    public bool HasJabSwitch => !string.Equals(JabSwitchPath, "(not found)", StringComparison.OrdinalIgnoreCase);

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            Set(ref _busy, value);
            RefreshWindowsCommand.RaiseCanExecuteChanged();
            AttachCommand.RaiseCanExecuteChanged();
        }
    }

    private async void RefreshWindows()
    {
        if (IsJavaMode)
        {
            IsBusy = true;
            Status = "Scanning desktop windows...";
            try
            {
                if (!_bridge.Initialize()) { Status = "Access Bridge initialization failed"; return; }
                await Task.Delay(500);
                var service = new JavaWindowDiscoveryService(_bridge, _logger);
                var windows = await Task.Run(service.GetJavaWindows);
                JavaWindows.Clear();
                foreach (var window in windows) JavaWindows.Add(new(window));
                SelectedJavaWindow = JavaWindows.FirstOrDefault();
                Status = windows.Count == 0 ? "No Java windows found" : $"{windows.Count} Java window(s) available";
            }
            finally
            {
                OnPropertyChanged(nameof(WindowItemCount));
                OnPropertyChanged(nameof(CurrentWindowItems));
                IsBusy = false;
            }
            return;
        }

        IsBusy = true;
        Status = "Scanning desktop windows...";
        try
        {
            var windows = await Task.Run(_windowsDiscovery.GetTopLevelWindows);
            WindowsDesktopWindows.Clear();
            foreach (var window in windows) WindowsDesktopWindows.Add(new(window));
            SelectedWindowsWindow = WindowsDesktopWindows.FirstOrDefault();
            Status = windows.Count == 0 ? "No desktop windows found" : $"{windows.Count} desktop window(s) available";
            _logger.Log($"Windows mode discovered {windows.Count} top-level window(s).");
        }
        finally
        {
            OnPropertyChanged(nameof(WindowItemCount));
            OnPropertyChanged(nameof(CurrentWindowItems));
            IsBusy = false;
        }
    }

    private async void Attach()
    {
        if (IsJavaMode)
        {
            if (SelectedJavaWindow is null) return;
            IsBusy = true;
            Status = $"Reading {SelectedJavaWindow.Model.Title}...";
            Tree.Clear();
            SelectedNode = null;
            try
            {
                var crawler = new AccessibleTreeCrawler(_bridge, _logger);
                _root = await Task.Run(() => crawler.BuildTree(SelectedJavaWindow.Model));
                OnPropertyChanged(nameof(Root));
                if (_root is not null)
                {
                    Tree.Add(_root);
                    _nodesByContext.Clear();
                    IndexNodes(_root);
                    SelectedNode = _root;
                    Status = $"Attached · {crawler.NodeCount:N0} accessible nodes";
                }
                else Status = "Attach failed";
            }
            finally { IsBusy = false; }
            return;
        }

        if (SelectedWindowsWindow is null) return;
        IsBusy = true;
        Status = $"Inspecting {SelectedWindowsWindow.Model.DisplayName}...";
        WindowsTree.Clear();
        SelectedWindowsNode = null;
        try
        {
            var result = await Task.Run(() => _windowsRouter.Inspect(SelectedWindowsWindow.Model));
            if (result.Succeeded && result.Root is not null)
            {
                _windowsRoot = result.Root;
                WindowsTree.Add(result.Root);
                OnPropertyChanged(nameof(WindowsRoot));
                OnPropertyChanged(nameof(CurrentTreeItems));
                SelectedWindowsNode = result.Root;
                Status = $"Attached via {result.BackendKind}";
                _logger.Log($"Windows attach succeeded using {result.BackendKind} for {SelectedWindowsWindow.Model.DisplayName}.");
            }
            else
            {
                Status = "Windows attach failed";
                _logger.Log($"Windows attach failed: {result.FailureReason}");
            }
        }
        finally { IsBusy = false; }
    }

    private void CopyLocator()
    {
        if (!HasSelection) return;
        ClipboardHelper.SetText(LocatorPreview);
        _logger.Log("Locator JSON copied to clipboard.");
        Status = "Locator copied";
    }

    private void RunDiagnostics()
    {
        _logger.Log("--- Startup diagnostics ---");
        foreach (var line in StartupDiagnostics.Generate()) _logger.Log(line);
    }

    public void RefreshRequirements()
    {
        var report = InspectorRequirementsService.Generate();
        SettingsSummary = report.Summary;
        JabSwitchPath = report.JabSwitchPath;
        BridgeDllPath = report.BridgeDllPath;
        JavaHomePath = report.JavaHome;
        AccessibilityRegistrationPath = report.AccessibilityRegistrationPath;
        RequirementChecks.Clear();
        foreach (var check in report.Checks) RequirementChecks.Add(new RequirementCheckViewModel(check));
        EnableJavaAccessBridgeCommand.RaiseCanExecuteChanged();
        DisableJavaAccessBridgeCommand.RaiseCanExecuteChanged();
    }

    private void EnableJavaAccessBridge() => RunJabSwitch("/enable", "Java Access Bridge enable requested.");
    private void DisableJavaAccessBridge() => RunJabSwitch("/disable", "Java Access Bridge disable requested.");

    private void RunJabSwitch(string argument, string successMessage)
    {
        if (!HasJabSwitch) { SettingsActionResult = "jabswitch.exe is not available on this machine."; return; }
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = JabSwitchPath,
                Arguments = argument,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(5000);
            SettingsActionResult = successMessage;
            _logger.Log($"{successMessage} ({argument})");
            RefreshRequirements();
        }
        catch (Exception ex)
        {
            SettingsActionResult = $"jabswitch failed: {ex.Message}";
            _logger.Log(SettingsActionResult);
        }
    }

    private void OpenEaseOfAccess()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:easeofaccess",
                UseShellExecute = true
            });
            SettingsActionResult = "Windows Accessibility settings opened.";
        }
        catch (Exception ex)
        {
            SettingsActionResult = $"Could not open Windows Accessibility settings: {ex.Message}";
        }
    }

    public InspectorSnapshot? CreateSnapshot() =>
        IsJavaMode && _root is not null && SelectedJavaWindow is not null
            ? new InspectorSnapshot(DateTime.Now, SelectedJavaWindow.Model.Title, SelectedJavaWindow.Model.HwndDisplay, SelectedJavaWindow.Model.VmId, _root)
            : null;

    public bool RefreshBounds(AccessibleNode node)
    {
        if (!_bridge.TryGetAccessibleContextInfo(node.VmId, node.Context, out var info)) return false;
        node.X = info.X; node.Y = info.Y; node.Width = info.Width; node.Height = info.Height;
        if (ReferenceEquals(node, SelectedNode)) RefreshPropertySurface();
        return true;
    }

    public AccessibleNode? InspectAt(int jabX, int jabY)
    {
        if (_root is null || !_bridge.TryGetAccessibleContextAt(_root.VmId, _root.Context, jabX, jabY, out var context)) return null;
        if (!_bridge.TryGetAccessibleContextInfo(_root.VmId, context, out var info)) { _bridge.ReleaseObject(_root.VmId, context); return null; }
        if (_bridge.IsSameObject(_root.VmId, context, _root.Context))
        {
            ApplyInfo(_root, info);
            _bridge.ReleaseObject(_root.VmId, context);
            return _root;
        }
        if (_nodesByContext.TryGetValue(context, out var existing))
        {
            ApplyInfo(existing, info);
            _bridge.ReleaseObject(_root.VmId, context);
            return existing;
        }
        ReleaseHoverContexts();
        _hoverContexts.Add(context);
        var node = CreateNode(_root.VmId, context, info);
        var child = node;
        var currentContext = context;
        for (var depth = 0; depth < 25 && _bridge.TryGetParentContext(_root.VmId, currentContext, out var parentContext); depth++)
        {
            if (_bridge.IsSameObject(_root.VmId, parentContext, _root.Context))
            { child.Parent = _root; _bridge.ReleaseObject(_root.VmId, parentContext); break; }
            if (_nodesByContext.TryGetValue(parentContext, out var indexedParent))
            { child.Parent = indexedParent; _bridge.ReleaseObject(_root.VmId, parentContext); break; }
            if (!_bridge.TryGetAccessibleContextInfo(_root.VmId, parentContext, out var parentInfo))
            { _bridge.ReleaseObject(_root.VmId, parentContext); break; }
            _hoverContexts.Add(parentContext);
            var parent = CreateNode(_root.VmId, parentContext, parentInfo);
            child.Parent = parent; child = parent; currentContext = parentContext;
        }
        var resolved = ResolveTreeNode(node, out var usesDynamicNodes);
        if (resolved is not null)
        {
            ApplyInfo(resolved, info);
            if (!usesDynamicNodes) ReleaseHoverContexts();
            return resolved;
        }
        return node;
    }

    public void RefreshSupportedActions()
    {
        SupportedActions = IsJavaMode
            ? SelectedNode is null ? "Select an accessibility node." : FormatActions(_automation.GetActions(SelectedNode))
            : SelectedWindowsNode is null ? "Select a Windows automation node." : $"Resolved through {SelectedWindowsNode.BackendKind}. Focus, click, type, set text, and get text are available where the selected backend exposes them.";
    }

    public bool InvokeDefaultAction()
    {
        if (!IsJavaMode)
        {
            if (SelectedWindowsWindow?.Model is null || SelectedWindowsNode is null)
            {
                AutomationOutput = "Select a Windows element first.";
                return false;
            }
            var windowsSuccess = _windowsActions.TryInvoke(SelectedWindowsWindow.Model, SelectedWindowsNode, out var message);
            AutomationOutput = windowsSuccess ? message : $"Windows semantic action unavailable: {message}";
            return windowsSuccess;
        }
        if (SelectedNode is null) return false;
        var javaSuccess = _automation.InvokeDefaultAction(SelectedNode, out var action);
        AutomationOutput = javaSuccess ? $"Executed semantic action: {action}" : "No supported semantic click action was exposed; using physical click fallback.";
        return javaSuccess;
    }

    public bool FocusSelected()
    {
        if (!IsJavaMode)
        {
            if (SelectedWindowsWindow?.Model is null || SelectedWindowsNode is null)
            {
                AutomationOutput = "Select a Windows element first.";
                return false;
            }
            var windowsSuccess = _windowsActions.TryFocus(SelectedWindowsWindow.Model, SelectedWindowsNode, out var message);
            AutomationOutput = message;
            return windowsSuccess;
        }
        if (SelectedNode is null) return false;
        var javaSuccess = _automation.Focus(SelectedNode);
        AutomationOutput = javaSuccess ? "Focus requested successfully." : "Focus request failed.";
        return javaSuccess;
    }

    public bool SetSelectedText(string text)
    {
        if (!IsJavaMode)
        {
            if (SelectedWindowsWindow?.Model is null || SelectedWindowsNode is null)
            {
                AutomationOutput = "Select a Windows element first.";
                return false;
            }
            var windowsSuccess = _windowsActions.TrySetText(SelectedWindowsWindow.Model, SelectedWindowsNode, text, out var message);
            AutomationOutput = message;
            return windowsSuccess;
        }
        if (SelectedNode is null) return false;
        var javaSuccess = _automation.SetText(SelectedNode, text);
        AutomationOutput = javaSuccess ? $"Text set successfully ({text.Length} characters)." : "Set text failed. Select an editable text component.";
        return javaSuccess;
    }

    public string GetSelectedText()
    {
        if (!IsJavaMode)
        {
            if (SelectedWindowsWindow?.Model is null || SelectedWindowsNode is null)
            {
                AutomationOutput = "Select a Windows element first.";
                return AutomationOutput;
            }
            AutomationOutput = _windowsActions.GetText(SelectedWindowsWindow.Model, SelectedWindowsNode);
            return AutomationOutput;
        }
        if (SelectedNode is null) return "Select an element first.";
        AutomationOutput = _automation.GetText(SelectedNode);
        return AutomationOutput;
    }

    public void ReportAutomation(string result) => AutomationOutput = result;
    public void Log(string message) => _logger.Log(message);

    public void Dispose()
    {
        ReleaseHoverContexts();
        _bridge.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void ClearSelectionsForModeChange()
    {
        Tree.Clear();
        WindowsTree.Clear();
        _root = null;
        _windowsRoot = null;
        SelectedNode = null;
        SelectedWindowsNode = null;
        LocatorPreview = "Select an element to generate a resilient locator.";
        SupportedActions = IsJavaMode ? "Select an accessibility node." : "Select a Windows automation node.";
    }

    private void RefreshPropertySurface()
    {
        OnPropertyChanged(nameof(SelectedDisplayName));
        OnPropertyChanged(nameof(PropertyNameValue));
        OnPropertyChanged(nameof(PropertyDescriptionValue));
        OnPropertyChanged(nameof(PropertyRoleValue));
        OnPropertyChanged(nameof(PropertyRoleSecondaryValue));
        OnPropertyChanged(nameof(PropertyStatesValue));
        OnPropertyChanged(nameof(PropertyStatesSecondaryValue));
        OnPropertyChanged(nameof(PropertyBoundsValue));
        OnPropertyChanged(nameof(PropertyIndexValue));
        OnPropertyChanged(nameof(PropertyChildrenValue));
        OnPropertyChanged(nameof(PropertyRawIdsValue));
    }

    private static string BuildWindowsLocatorPreview(WindowsAutomationNode node)
    {
        var payload = new
        {
            provider = node.BackendKind.ToString().ToLowerInvariant(),
            path = BuildWindowsPath(node),
            role = node.Role,
            name = node.Name,
            className = node.ClassName,
            automationId = node.AutomationId,
            nativeHandle = $"0x{node.NativeHandle.ToInt64():X}"
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string BuildWindowsPath(WindowsAutomationNode node)
    {
        var segments = new Stack<string>();
        for (var cursor = node; cursor is not null; cursor = cursor.Parent)
        {
            var role = string.IsNullOrWhiteSpace(cursor.Role) ? "node" : cursor.Role;
            segments.Push($"{role}[{Math.Max(cursor.IndexInParent, 0)}]");
        }
        return string.Join("/", segments);
    }

    private static string FormatJavaBounds(AccessibleNode? node) =>
        node is null ? "" : $"{node.X}, {node.Y}  ·  {node.Width} x {node.Height}";

    private static string FormatWindowsBounds(WindowsAutomationNode? node) =>
        node is null ? "" : $"{node.Bounds.X}, {node.Bounds.Y}  ·  {node.Bounds.Width} x {node.Bounds.Height}";

    private static string FormatJavaIds(AccessibleNode? node) =>
        node is null ? "" : $"VM {node.VmId} · CTX {node.Context}";

    private static string FormatWindowsIds(WindowsAutomationNode? node) =>
        node is null ? "" : $"HWND 0x{node.NativeHandle.ToInt64():X}";

    private static string FormatActions(IReadOnlyList<string> actions) =>
        actions.Count == 0 ? "No semantic actions exposed" : string.Join("  ·  ", actions);

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void ReleaseHoverContexts()
    {
        for (var i = _dynamicHoverNodes.Count - 1; i >= 0; i--)
        {
            var node = _dynamicHoverNodes[i];
            node.Parent?.Children.Remove(node);
            _nodesByContext.Remove(node.Context);
        }
        _dynamicHoverNodes.Clear();
        if (_root is not null) foreach (var context in _hoverContexts) _bridge.ReleaseObject(_root.VmId, context);
        _hoverContexts.Clear();
    }

    private void IndexNodes(AccessibleNode node)
    {
        _nodesByContext[node.Context] = node;
        foreach (var child in node.Children) IndexNodes(child);
    }

    private AccessibleNode? ResolveTreeNode(AccessibleNode hoverNode, out bool usesDynamicNodes)
    {
        usesDynamicNodes = false;
        if (_root is null) return null;
        var unresolved = new Stack<AccessibleNode>();
        AccessibleNode? cursor = hoverNode;
        AccessibleNode? resolvedBase = null;
        while (cursor is not null)
        {
            if (_nodesByContext.TryGetValue(cursor.Context, out resolvedBase)) break;
            unresolved.Push(cursor);
            cursor = cursor.Parent;
        }
        resolvedBase ??= _root;
        if (unresolved.Count > 0 && NodesCorrespond(unresolved.Peek(), resolvedBase)) unresolved.Pop();

        while (unresolved.Count > 0)
        {
            var step = unresolved.Pop();
            AccessibleNode? child = null;
            if (step.IndexInParent >= 0 && step.IndexInParent < resolvedBase.Children.Count)
            {
                var indexed = resolvedBase.Children[step.IndexInParent];
                if (NodesCorrespond(step, indexed)) child = indexed;
            }
            child ??= resolvedBase.Children.FirstOrDefault(x => NodesCorrespond(step, x));
            child ??= resolvedBase.Children.FirstOrDefault(x => string.Equals(x.RoleEnUs, step.RoleEnUs, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Role, step.Role, StringComparison.OrdinalIgnoreCase));
            if (child is null)
            {
                step.Parent = resolvedBase;
                resolvedBase.Children.Add(step);
                _nodesByContext[step.Context] = step;
                _dynamicHoverNodes.Add(step);
                child = step;
                usesDynamicNodes = true;
            }
            resolvedBase = child;
        }
        return resolvedBase;
    }

    private static bool NodesCorrespond(AccessibleNode left, AccessibleNode right)
    {
        var roleMatches = string.Equals(left.RoleEnUs, right.RoleEnUs, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(left.Role, right.Role, StringComparison.OrdinalIgnoreCase);
        if (!roleMatches) return false;
        return string.IsNullOrWhiteSpace(left.Name) || string.IsNullOrWhiteSpace(right.Name) ||
               string.Equals(left.Name, right.Name, StringComparison.Ordinal);
    }

    private static AccessibleNode CreateNode(int vmId, long context, JabInspector.Native.AccessibleContextInfo info)
    {
        var node = new AccessibleNode { VmId = vmId, Context = context };
        ApplyInfo(node, info);
        return node;
    }

    private static void ApplyInfo(AccessibleNode node, JabInspector.Native.AccessibleContextInfo x)
    {
        node.Name = x.Name ?? "";
        node.Description = x.Description ?? "";
        node.Role = string.IsNullOrWhiteSpace(x.Role) ? "unknown" : x.Role;
        node.RoleEnUs = x.RoleEnUs ?? "";
        node.States = x.States ?? "";
        node.StatesEnUs = x.StatesEnUs ?? "";
        node.IndexInParent = x.IndexInParent;
        node.ChildrenCount = x.ChildrenCount;
        node.X = x.X;
        node.Y = x.Y;
        node.Width = x.Width;
        node.Height = x.Height;
        node.AccessibleComponent = x.AccessibleComponent;
        node.AccessibleAction = x.AccessibleAction;
        node.AccessibleSelection = x.AccessibleSelection;
        node.AccessibleText = x.AccessibleText;
        node.AccessibleInterfaces = x.AccessibleInterfaces;
    }
}

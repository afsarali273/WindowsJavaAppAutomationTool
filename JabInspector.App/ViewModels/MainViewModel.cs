using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    private readonly JavaElementInspectionService _javaInspection;
    private readonly JavaObjectRepositoryService _javaRepository = new();
    private readonly JavaNodeResolverService _javaResolver = new();
    private readonly WindowsWindowDiscoveryService _windowsDiscovery = new();
    private readonly WindowsPrivilegeService _windowsPrivilege = new();
    private readonly WindowsAutomationRouter _windowsRouter = new();
    private readonly WindowsAutomationActionService _windowsActions = new();
    private readonly WindowsElementProjectionService _windowsProjection = new();
    private readonly Win32Scanner _win32Scanner = new();
    private readonly MsaaScanner _msaaScanner = new();
    private readonly object _pendingLogLock = new();
    private readonly Queue<string> _pendingLogs = new();
    private bool _logDrainScheduled;
    private string _windowsProjectedSourcePreview = "Select a Windows automation node to inspect projected source data.";
    private string _windowsRawSourcePreview = "Select a Windows automation node to inspect raw backend data.";
    private string _windowsPointProbePreview = "Select a Windows automation node to inspect point-probe evidence.";

    private JavaWindowViewModel? _selectedJavaWindow;
    private WindowsWindowViewModel? _selectedWindowsWindow;
    private AccessibleNode? _selectedNode;
    private WindowsAutomationNode? _selectedWindowsNode;
    private JavaObjectRepositoryEntry? _selectedRepositoryEntry;
    private JavaRecordedStep? _selectedRecordedStep;
    private LocatorCandidate? _selectedWindowsLocatorCandidate;
    private AccessibleNode? _root;
    private WindowsAutomationNode? _windowsRoot;
    private DesktopElement? _selectedWindowsElement;
    private WindowsInspectionView _selectedWindowsInspectionView = WindowsInspectionView.Routed;
    private InspectorMode _selectedMode = InspectorMode.Java;
    private string _locatorPreview = "Select an element to generate a resilient locator.";
    private string _status = "Ready to inspect";
    private bool _busy;
    private readonly List<long> _hoverContexts = [];
    private readonly List<AccessibleNode> _dynamicHoverNodes = [];
    private readonly Dictionary<long, AccessibleNode> _nodesByContext = [];
    private bool _suppressAutoAttachOnSelection;
    private string _automationOutput = "Automation results will appear here.";
    private string _supportedActions = "Select an accessibility node.";
    private string _settingsSummary = "Review Java Access Bridge requirements and common setup actions.";
    private string _settingsActionResult = "No settings action has been run yet.";
    private string _jabswitchPath = "(not found)";
    private string _bridgeDllPath = "(not found)";
    private string _javaHomePath = "(not set)";
    private string _accessibilityRegistrationPath = "(not found)";
    private string _recordingSessionName = "No active recording session";
    private string _recordingApplicationAlias = "";
    private string _recordingProjectPath = "";
    private string _recordingStatus = "Create a Java recording session to capture a locator repository and playback steps.";
    private string _recordingRepositoryPreview = "Select a recorded object to inspect its repository properties.";
    private string _recordingStepPreview = "Select a recorded step to inspect its playback metadata.";
    private string _playbackOutput = "Playback output will appear here.";
    private string _windowsPrivilegeSummary = "Inspector privilege diagnostics will appear when Windows mode is active.";
    private string _selectedWindowsPrivilegeWarning = "";
    private bool _isRecordingActive;
    private bool _isRecordingPaused;

    public ObservableCollection<JavaWindowViewModel> JavaWindows { get; } = [];
    public ObservableCollection<WindowsWindowViewModel> WindowsDesktopWindows { get; } = [];
    public ObservableCollection<AccessibleNode> Tree { get; } = [];
    public ObservableCollection<WindowsAutomationNode> WindowsTree { get; } = [];
    public ObservableCollection<JavaObjectRepositoryEntry> RepositoryEntries { get; } = [];
    public ObservableCollection<JavaRecordedStep> RecordedSteps { get; } = [];
    public ObservableCollection<JavaWindowLocator> RecordingWindowScopes { get; } = [];
    public ObservableCollection<string> Logs { get; } = [];
    public ObservableCollection<RequirementCheckViewModel> RequirementChecks { get; } = [];
    public IReadOnlyList<InspectorMode> AvailableModes { get; } = Enum.GetValues<InspectorMode>();
    public IReadOnlyList<WindowsInspectionView> AvailableWindowsInspectionViews { get; } = Enum.GetValues<WindowsInspectionView>();

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
        _javaInspection = new JavaElementInspectionService(_bridge, _logger);
        _logger.MessageLogged += EnqueueLogMessage;

        RefreshWindowsCommand = new RelayCommand(RefreshWindows, () => !IsBusy);
        AttachCommand = new RelayCommand(Attach, () => !IsBusy && (IsJavaMode ? SelectedJavaWindow is not null : SelectedWindowsWindow is not null));
        CopyLocatorCommand = new RelayCommand(CopyLocator, () => HasSelection);
        RunDiagnosticsCommand = new RelayCommand(RunDiagnostics);
        RefreshRequirementsCommand = new RelayCommand(RefreshRequirements);
        EnableJavaAccessBridgeCommand = new RelayCommand(EnableJavaAccessBridge, () => HasJabSwitch);
        DisableJavaAccessBridgeCommand = new RelayCommand(DisableJavaAccessBridge, () => HasJabSwitch);
        OpenEaseOfAccessCommand = new RelayCommand(OpenEaseOfAccess);

        _logger.Log($"Log file path: {_logger.LogFilePath}");
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
            if (!_suppressAutoAttachOnSelection && IsJavaMode && value is not null) _ = SafeAutoAttachSelectedWindowAsync();
        }
    }

    public WindowsWindowViewModel? SelectedWindowsWindow
    {
        get => _selectedWindowsWindow;
        set
        {
            if (!Set(ref _selectedWindowsWindow, value)) return;
            OnPropertyChanged(nameof(SelectedWindowItem));
            UpdateWindowsPrivilegeState();
            RefreshPropertySurface();
            AttachCommand.RaiseCanExecuteChanged();
            if (IsWindowsMode && value is not null) _ = SafeAutoAttachSelectedWindowAsync();
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
                if (value is not null) RefreshBounds(value);
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
                _selectedWindowsElement = value is null || SelectedWindowsWindow?.Model is null
                    ? null
                    : _windowsProjection.Project(SelectedWindowsWindow.Model, value);
                UpdateWindowsSourcePreviews(value, _selectedWindowsElement);
                SelectedWindowsLocatorCandidate = _selectedWindowsElement?.Locators
                    .OrderBy(locator => locator.Priority)
                    .ThenByDescending(locator => locator.Score)
                    .FirstOrDefault();
                LocatorPreview = value is null ? "Select an element to generate a resilient locator." : BuildWindowsLocatorPreview(value, _selectedWindowsElement);
                SupportedActions = value is null ? "Select a Windows automation node." : $"Resolved through {value.BackendKind}. Focus, click, type, set text, and get text are available where the selected backend exposes them.";
            }
            CopyLocatorCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasSelection));
            RefreshPropertySurface();
        }
    }

    public JavaObjectRepositoryEntry? SelectedRepositoryEntry
    {
        get => _selectedRepositoryEntry;
        set
        {
            if (!Set(ref _selectedRepositoryEntry, value)) return;
            RecordingRepositoryPreview = value is null ? "Select a recorded object to inspect its repository properties." : _javaRepository.BuildPropertiesPreview(value);
        }
    }

    public LocatorCandidate? SelectedWindowsLocatorCandidate
    {
        get => _selectedWindowsLocatorCandidate;
        set
        {
            if (!Set(ref _selectedWindowsLocatorCandidate, value)) return;
            OnPropertyChanged(nameof(SelectedWindowsLocatorCandidateDetails));
        }
    }

    public JavaRecordedStep? SelectedRecordedStep
    {
        get => _selectedRecordedStep;
        set
        {
            if (!Set(ref _selectedRecordedStep, value)) return;
            RecordingStepPreview = value is null ? "Select a recorded step to inspect its playback metadata." : _javaRepository.BuildStepPreview(value);
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
            if (value == InspectorMode.Windows)
            {
                UpdateWindowsPrivilegeState();
            }
        }
    }

    public AccessibleNode? Root => _root;
    public WindowsAutomationNode? WindowsRoot => _windowsRoot;
    public JavaWindowInfo? CurrentWindow => SelectedJavaWindow?.Model;
    public bool HasSelection => IsJavaMode ? SelectedNode is not null : SelectedWindowsNode is not null;
    public bool IsJavaMode => SelectedMode == InspectorMode.Java;
    public bool IsWindowsMode => SelectedMode == InspectorMode.Windows;
    public WindowsInspectionView SelectedWindowsInspectionView
    {
        get => _selectedWindowsInspectionView;
        set
        {
            if (!Set(ref _selectedWindowsInspectionView, value)) return;
            OnPropertyChanged(nameof(WindowsInspectionHint));
            if (IsWindowsMode && SelectedWindowsWindow is not null)
            {
                _ = SafeAutoAttachSelectedWindowAsync();
            }
        }
    }
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
    public string WindowsInspectionHint => SelectedWindowsInspectionView switch
    {
        WindowsInspectionView.Routed => "Auto-selects the strongest available Windows backend.",
        WindowsInspectionView.UiaRaw => "UIA Raw View exposes the deepest automation tree the OS provides.",
        WindowsInspectionView.UiaControl => "UIA Control View filters down to interactive controls.",
        WindowsInspectionView.UiaContent => "UIA Content View focuses on user-facing content elements.",
        WindowsInspectionView.Msaa => "MSAA is useful for VB6 and older accessibility trees.",
        WindowsInspectionView.Win32 => "Win32 shows the raw HWND tree and classic control metadata.",
        _ => ""
    };
    public string RefreshButtonLabel => IsJavaMode ? "Refresh windows" : "Refresh desktop";
    public string AttachButtonLabel => IsJavaMode ? "Attach and inspect" : "Inspect selection";

    public string SelectedDisplayName => IsJavaMode
        ? SelectedNode?.DisplayName ?? "(no selection)"
        : SelectedWindowsNode?.DisplayName ?? SelectedWindowsWindow?.Model.DisplayName ?? "(no selection)";
    public string PropertyNameValue => IsJavaMode ? SelectedNode?.Name ?? "" : SelectedWindowsNode?.Name ?? "";
    public string PropertyDescriptionValue => IsJavaMode ? SelectedNode?.Description ?? "" : BuildWindowsDescription(SelectedWindowsNode);
    public string PropertyRoleValue => IsJavaMode ? SelectedNode?.Role ?? "" : SelectedWindowsNode?.Role ?? "";
    public string PropertyRoleSecondaryValue => IsJavaMode ? SelectedNode?.RoleEnUs ?? "" : BuildWindowsRoleSecondaryValue(SelectedWindowsNode, _selectedWindowsElement);
    public string PropertyStatesValue => IsJavaMode ? SelectedNode?.States ?? "" : BuildWindowsStatesValue(SelectedWindowsNode);
    public string PropertyStatesSecondaryValue => IsJavaMode ? SelectedNode?.StatesEnUs ?? "" : BuildWindowsStatesSecondaryValue(SelectedWindowsNode);
    public string PropertyBoundsValue => IsJavaMode
        ? FormatJavaBounds(SelectedNode)
        : BuildWindowsBoundsValue(SelectedWindowsNode);
    public string PropertyIndexValue => IsJavaMode ? $"{SelectedNode?.IndexInParent ?? -1}" : $"{SelectedWindowsNode?.IndexInParent ?? -1}";
    public string PropertyChildrenValue => IsJavaMode ? $"{SelectedNode?.ChildrenCount ?? 0}" : $"{SelectedWindowsNode?.Children.Count ?? 0}";
    public string PropertyRawIdsValue => IsJavaMode
        ? FormatJavaIds(SelectedNode)
        : BuildWindowsRawIdsValue(SelectedWindowsNode);
    public string PropertyLocatorPathValue => IsJavaMode && SelectedNode is not null ? LocatorGenerator.BuildPath(SelectedNode) : BuildWindowsPath(SelectedWindowsNode);
    public string PropertyIndexPathValue => IsJavaMode && SelectedNode is not null ? LocatorGenerator.BuildIndexPath(SelectedNode) : BuildWindowsIndexPath(SelectedWindowsNode);
    public string PropertyXPathValue => IsJavaMode && SelectedNode is not null ? LocatorGenerator.BuildXPath(SelectedNode) : BuildWindowsClassPath(SelectedWindowsNode);
    public string PropertyTextPreviewValue => IsJavaMode ? FormatTextPreview(SelectedNode) : BuildWindowsTextPreview(SelectedWindowsNode);
    public string PropertyTextDetailsValue => IsJavaMode ? FormatTextDetails(SelectedNode) : BuildWindowsTextDetails(SelectedWindowsNode);
    public string PropertyValueDetailsValue => IsJavaMode ? FormatValueDetails(SelectedNode) : BuildWindowsValueDetails(_selectedWindowsElement);
    public string WindowsEvidenceSummary => IsJavaMode ? "" : BuildWindowsEvidenceSummary(SelectedWindowsNode, _selectedWindowsElement);
    public string WindowsLocatorCandidatesSummary => IsJavaMode ? "" : BuildWindowsLocatorCandidatesSummary(_selectedWindowsElement);
    public string WindowsMetadataSummary => IsJavaMode ? "" : BuildWindowsMetadataSummary(_selectedWindowsElement);
    public IReadOnlyList<LocatorCandidate> WindowsLocatorCandidates => _selectedWindowsElement?.Locators
        .OrderBy(locator => locator.Priority)
        .ThenByDescending(locator => locator.Score)
        .ToList() ?? [];
    public string SelectedWindowsLocatorCandidateDetails => BuildWindowsLocatorCandidateDetails(SelectedWindowsLocatorCandidate);
    public string WindowsProjectedSourcePreview => IsJavaMode ? "" : _windowsProjectedSourcePreview;
    public string WindowsRawSourcePreview => IsJavaMode ? "" : _windowsRawSourcePreview;
    public string WindowsPointProbePreview => IsJavaMode ? "" : _windowsPointProbePreview;
    public string WindowsPrivilegeSummary { get => _windowsPrivilegeSummary; private set => Set(ref _windowsPrivilegeSummary, value); }
    public string SelectedWindowsPrivilegeWarning { get => _selectedWindowsPrivilegeWarning; private set => Set(ref _selectedWindowsPrivilegeWarning, value); }
    public bool IsInspectorElevated => _windowsPrivilege.IsCurrentProcessElevated();
    public Visibility WindowsPrivilegeWarningVisibility => !string.IsNullOrWhiteSpace(SelectedWindowsPrivilegeWarning) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility JavaSelectionVisibility => IsJavaMode ? Visibility.Visible : Visibility.Collapsed;
    public Visibility WindowsSelectionVisibility => IsWindowsMode ? Visibility.Visible : Visibility.Collapsed;

    public string SettingsSummary { get => _settingsSummary; private set => Set(ref _settingsSummary, value); }
    public string SettingsActionResult { get => _settingsActionResult; private set => Set(ref _settingsActionResult, value); }
    public string JabSwitchPath { get => _jabswitchPath; private set => Set(ref _jabswitchPath, value); }
    public string BridgeDllPath { get => _bridgeDllPath; private set => Set(ref _bridgeDllPath, value); }
    public string JavaHomePath { get => _javaHomePath; private set => Set(ref _javaHomePath, value); }
    public string AccessibilityRegistrationPath { get => _accessibilityRegistrationPath; private set => Set(ref _accessibilityRegistrationPath, value); }
    public bool HasJabSwitch => !string.Equals(JabSwitchPath, "(not found)", StringComparison.OrdinalIgnoreCase);
    public bool IsRecordingActive { get => _isRecordingActive; private set => Set(ref _isRecordingActive, value); }
    public bool IsRecordingPaused { get => _isRecordingPaused; private set => Set(ref _isRecordingPaused, value); }
    public string RecordingSessionName { get => _recordingSessionName; private set => Set(ref _recordingSessionName, value); }
    public string RecordingApplicationAlias { get => _recordingApplicationAlias; private set => Set(ref _recordingApplicationAlias, value); }
    public string RecordingProjectPath { get => _recordingProjectPath; private set => Set(ref _recordingProjectPath, value); }
    public string RecordingStatus { get => _recordingStatus; private set => Set(ref _recordingStatus, value); }
    public string RecordingRepositoryPreview { get => _recordingRepositoryPreview; private set => Set(ref _recordingRepositoryPreview, value); }
    public string RecordingStepPreview { get => _recordingStepPreview; private set => Set(ref _recordingStepPreview, value); }
    public string PlaybackOutput { get => _playbackOutput; private set => Set(ref _playbackOutput, value); }
    public bool CanUseJavaRecording => IsJavaMode && CurrentWindow is not null && Root is not null;
    public int RecordingStepCount => RecordedSteps.Count;
    public int RecordingObjectCount => RepositoryEntries.Count;
    public string RepositoryStorageDirectory => EnsureRepositoryStorageDirectory();
    public string CurrentRepositorySummary => string.IsNullOrWhiteSpace(RecordingProjectPath)
        ? $"In-memory repository. Default save path: {Path.Combine(RepositoryStorageDirectory, GetDefaultRecordingProjectFileName())}"
        : RecordingProjectPath;
    public string RecordingPauseButtonText => IsRecordingPaused ? "Resume" : "Pause";
    public string RecordingBadgeText => IsRecordingActive
        ? IsRecordingPaused ? $"PAUSED  {RecordingStepCount} STEP(S)" : $"REC  {RecordingStepCount} STEP(S)"
        : "RECORDER IDLE";

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
                var windows = await Task.Run(() => service.GetJavaWindows());
                JavaWindows.Clear();
                foreach (var window in windows) JavaWindows.Add(new(window));
                SelectedJavaWindow = JavaWindows.FirstOrDefault();
                Status = windows.Count == 0 ? "No Java windows found" : $"{windows.Count} Java window(s) available";
            }
            catch (Exception ex)
            {
                Status = $"Java window refresh failed: {ex.Message}";
                _logger.Log($"Java window refresh failed: {ex}");
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
            UpdateWindowsPrivilegeState();
            Status = windows.Count == 0 ? "No desktop windows found" : $"{windows.Count} desktop window(s) available";
            _logger.Log($"Windows mode discovered {windows.Count} top-level window(s).");
        }
        catch (Exception ex)
        {
            Status = $"Desktop window refresh failed: {ex.Message}";
            _logger.Log($"Desktop window refresh failed: {ex}");
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
        try
        {
            await AutoAttachSelectedWindowAsync();
        }
        catch (Exception ex)
        {
            Status = $"Attach failed: {ex.Message}";
            _logger.Log($"Attach failed: {ex}");
            IsBusy = false;
        }
    }

    private async Task SafeAutoAttachSelectedWindowAsync()
    {
        try
        {
            await AutoAttachSelectedWindowAsync();
        }
        catch (Exception ex)
        {
            Status = $"Attach failed: {ex.Message}";
            _logger.Log($"Attach failed: {ex}");
            IsBusy = false;
        }
    }

    private async Task AutoAttachSelectedWindowAsync()
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
            var selectedView = SelectedWindowsInspectionView;
            var result = await Task.Run(() => _windowsRouter.Inspect(SelectedWindowsWindow.Model, selectedView));
            if (result.Succeeded && result.Root is not null)
            {
                _windowsRoot = result.Root;
                WindowsTree.Add(result.Root);
                OnPropertyChanged(nameof(WindowsRoot));
                OnPropertyChanged(nameof(CurrentTreeItems));
                SelectedWindowsNode = result.Root;
                Status = selectedView == WindowsInspectionView.Routed
                    ? $"Attached via {result.BackendKind}"
                    : $"Attached via {selectedView}";
                _logger.Log($"Windows attach succeeded using {result.BackendKind} for {SelectedWindowsWindow.Model.DisplayName}. RequestedView={selectedView}.");
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
        ApplyInfo(node, info);
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
            { child.Parent = _root; child.HasManagedDescendantAncestor = _root.HasManagedDescendantAncestor || _root.ManagesDescendants; _bridge.ReleaseObject(_root.VmId, parentContext); break; }
            if (_nodesByContext.TryGetValue(parentContext, out var indexedParent))
            { child.Parent = indexedParent; child.HasManagedDescendantAncestor = indexedParent.HasManagedDescendantAncestor || indexedParent.ManagesDescendants; _bridge.ReleaseObject(_root.VmId, parentContext); break; }
            if (!_bridge.TryGetAccessibleContextInfo(_root.VmId, parentContext, out var parentInfo))
            { _bridge.ReleaseObject(_root.VmId, parentContext); break; }
            _hoverContexts.Add(parentContext);
            var parent = CreateNode(_root.VmId, parentContext, parentInfo);
            child.Parent = parent;
            child.HasManagedDescendantAncestor = parent.HasManagedDescendantAncestor || parent.ManagesDescendants;
            child = parent; currentContext = parentContext;
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

    public JavaInspectionResult? InspectJavaAtScreenPoint(
        JabInspector.Core.Models.NativePoint screenPoint,
        Func<int, int, AccessibleNode?> inspectAtJabPoint,
        Func<AccessibleNode, ElementBounds> getPhysicalBounds,
        Func<ElementBounds, JabInspector.Core.Models.NativePoint, bool> containsPoint,
        string logPrefix = "[INSPECT]")
    {
        return _root is not null && CurrentWindow is not null
            ? _javaInspection.InspectAtScreenPoint(
                _root,
                CurrentWindow.Hwnd,
                screenPoint,
                inspectAtJabPoint,
                getPhysicalBounds,
                containsPoint,
                node => RefreshBounds(node),
                logPrefix)
            : null;
    }

    public JavaInspectionResult? ResolveJavaNodeBounds(
        AccessibleNode node,
        Func<AccessibleNode, ElementBounds> getPhysicalBounds,
        string logPrefix = "[BOUNDS]") =>
        _javaInspection.ResolveVisibleBounds(
            node,
            getPhysicalBounds,
            candidate => RefreshBounds(candidate),
            logPrefix);

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
    public void ReportPlayback(string result) => PlaybackOutput = result;
    public void Log(string message) => _logger.Log(message);

    public bool StartJavaRecordingSession(string sessionName, string applicationAlias, bool appendExisting = false)
    {
        _logger.Debug($"Recording session start requested. JavaMode={IsJavaMode}, HasWindow={CurrentWindow is not null}, HasRoot={Root is not null}, Session='{sessionName}', Alias='{applicationAlias}', AppendExisting={appendExisting}.");
        if (!IsJavaMode || CurrentWindow is null || Root is null)
        {
            RecordingStatus = "Attach to a Java window before starting a recording session.";
            _logger.Log($"Recording session start rejected. Status='{RecordingStatus}'");
            return false;
        }

        var hasExistingSession = !string.IsNullOrWhiteSpace(RecordingSessionName)
                                 && !string.Equals(RecordingSessionName, "No active recording session", StringComparison.OrdinalIgnoreCase);
        var normalizedSessionName = string.IsNullOrWhiteSpace(sessionName)
            ? appendExisting && hasExistingSession ? RecordingSessionName : $"JavaSession_{DateTime.Now:yyyyMMdd_HHmmss}"
            : sessionName.Trim();
        var normalizedAlias = string.IsNullOrWhiteSpace(applicationAlias)
            ? appendExisting && !string.IsNullOrWhiteSpace(RecordingApplicationAlias) ? RecordingApplicationAlias : CurrentWindow.Title
            : applicationAlias.Trim();
        if (!appendExisting)
        {
            RepositoryEntries.Clear();
            RecordedSteps.Clear();
            RecordingWindowScopes.Clear();
            SelectedRepositoryEntry = null;
            SelectedRecordedStep = null;
        }

        RememberRecordingWindowScope(CurrentWindow);
        RecordingSessionName = normalizedSessionName;
        RecordingApplicationAlias = normalizedAlias;
        if (!appendExisting || string.IsNullOrWhiteSpace(RecordingProjectPath))
        {
            RecordingProjectPath = Path.Combine(
                EnsureRepositoryStorageDirectory(),
                GetDefaultRecordingProjectFileName());
        }

        IsRecordingActive = true;
        IsRecordingPaused = false;
        RecordingStatus = appendExisting
            ? $"Resuming recorder for {normalizedAlias}. Next step will be {RecordedSteps.Count + 1}."
            : $"Preparing recorder for {normalizedAlias}...";
        PlaybackOutput = "Playback output will appear here.";
        RefreshRecordingSurface();
        var preScanSucceeded = RefreshCurrentJavaTree("recording pre-scan");
        var modeText = appendExisting ? "resumed" : "active";
        RecordingStatus = preScanSucceeded
            ? $"Recording session '{normalizedSessionName}' is {modeText} for {normalizedAlias}. Pre-scanned {CountNodes(Root):N0} node(s). Next step: {RecordedSteps.Count + 1}."
            : $"Recording session '{normalizedSessionName}' is {modeText} for {normalizedAlias}. Pre-scan could not refresh the tree. Next step: {RecordedSteps.Count + 1}.";
        RefreshRecordingSurface();
        _logger.Log($"Recording session {(appendExisting ? "resumed/appended" : "started")}. Session='{RecordingSessionName}', Alias='{RecordingApplicationAlias}', ProjectPath='{RecordingProjectPath}', RootNode='{Root?.DisplayName ?? "(none)"}', ExistingSteps={RecordedSteps.Count}, ExistingObjects={RepositoryEntries.Count}, PreScanSucceeded={preScanSucceeded}.");
        return true;
    }

    public void StopJavaRecordingSession()
    {
        IsRecordingActive = false;
        IsRecordingPaused = false;
        RecordingStatus = RepositoryEntries.Count == 0 && RecordedSteps.Count == 0
            ? "Recording session stopped. No objects or steps were captured."
            : $"Recording session stopped with {RecordingObjectCount} object(s) and {RecordingStepCount} step(s).";
        RefreshRecordingSurface();
        _logger.Log($"Recording session stopped. Steps={RecordingStepCount}, Objects={RecordingObjectCount}, ProjectPath='{RecordingProjectPath}'.");
    }

    public void PauseJavaRecordingSession()
    {
        if (!IsRecordingActive)
        {
            RecordingStatus = "No active recording session to pause.";
            _logger.Log("Pause recording ignored because no recording session is active.");
            return;
        }

        if (IsRecordingPaused) return;
        IsRecordingPaused = true;
        RecordingStatus = $"Recording paused. {RecordingStepCount} step(s) captured so far.";
        RefreshRecordingSurface();
        _logger.Log($"Recording paused. Steps={RecordingStepCount}, Objects={RecordingObjectCount}.");
    }

    public void ResumeJavaRecordingSession()
    {
        if (!IsRecordingActive)
        {
            RecordingStatus = "No active recording session to resume.";
            _logger.Log("Resume recording ignored because no recording session is active.");
            return;
        }

        if (!IsRecordingPaused) return;
        IsRecordingPaused = false;
        RecordingStatus = $"Recording resumed for {RecordingApplicationAlias}.";
        RefreshRecordingSurface();
        _logger.Log($"Recording resumed. Steps={RecordingStepCount}, Objects={RecordingObjectCount}.");
    }

    public void ToggleJavaRecordingPause()
    {
        if (IsRecordingPaused) ResumeJavaRecordingSession();
        else PauseJavaRecordingSession();
    }

    public JavaObjectRepositoryEntry? AddSelectedNodeToRepository(string? friendlyName = null)
    {
        _logger.Debug($"Repository capture requested. JavaMode={IsJavaMode}, HasWindow={CurrentWindow is not null}, HasSelectedNode={SelectedNode is not null}, FriendlyName='{friendlyName ?? ""}'.");
        if (!IsJavaMode || CurrentWindow is null || SelectedNode is null)
        {
            RecordingStatus = "Select a Java accessibility node before adding it to the repository.";
            _logger.Log($"Repository capture rejected. Status='{RecordingStatus}'.");
            return null;
        }
        return CaptureNodeToRepository(CurrentWindow, SelectedNode, friendlyName, updateStatus: true);
    }

    public JavaObjectRepositoryEntry? RefreshSelectedRepositoryEntryFromSelectedNode()
    {
        if (SelectedRepositoryEntry is null)
        {
            RecordingStatus = "Select a repository object before refreshing it from the inspected element.";
            return null;
        }

        if (!IsJavaMode || CurrentWindow is null || SelectedNode is null)
        {
            RecordingStatus = "Select a Java accessibility node before refreshing the repository object.";
            return null;
        }

        RefreshBounds(SelectedNode);
        var existing = SelectedRepositoryEntry;
        var refreshed = _javaRepository.CreateEntry(CurrentWindow, SelectedNode, existing.ObjectKey, existing.FriendlyName);
        var index = RepositoryEntries.IndexOf(existing);
        if (index >= 0) RepositoryEntries[index] = refreshed;
        else RepositoryEntries.Add(refreshed);
        RememberRecordingWindowScope(CurrentWindow);
        SelectedRepositoryEntry = refreshed;
        RecordingStatus = $"Refreshed repository object {refreshed.ObjectKey} from selected inspector element.";
        RefreshRecordingSurface();
        _logger.Log($"Repository object refreshed from inspector selection. ObjectKey='{refreshed.ObjectKey}', Path='{refreshed.Path}', Role='{refreshed.Role}', Name='{refreshed.Name}'.");
        return refreshed;
    }

    public JavaObjectRepositoryEntry? AddSelectedNodeToRepositoryFile(string path, bool createNew)
    {
        _logger.Debug($"Repository file capture requested. Path='{path}', CreateNew={createNew}, JavaMode={IsJavaMode}, HasWindow={CurrentWindow is not null}, HasSelectedNode={SelectedNode is not null}.");
        if (!IsJavaMode || CurrentWindow is null || SelectedNode is null)
        {
            RecordingStatus = "Select a Java accessibility node before adding it to a repository file.";
            return null;
        }

        RefreshBounds(SelectedNode);

        JavaRecordingProject project;
        if (!createNew && File.Exists(path))
        {
            project = _javaRepository.LoadProject(path);
        }
        else
        {
            var sessionName = Path.GetFileNameWithoutExtension(path);
            if (sessionName.EndsWith(".jrecording", StringComparison.OrdinalIgnoreCase))
                sessionName = Path.GetFileNameWithoutExtension(sessionName);
            project = _javaRepository.CreateProject(
                string.IsNullOrWhiteSpace(sessionName) ? "JavaRepository" : sessionName,
                string.IsNullOrWhiteSpace(CurrentWindow.Title) ? "JavaApplication" : CurrentWindow.Title,
                CurrentWindow);
        }

        var existing = project.Repository.FirstOrDefault(x =>
            string.Equals(x.Path, SelectedNode.Path, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Name, SelectedNode.Name, StringComparison.Ordinal));

        JavaObjectRepositoryEntry entry;
        if (existing is not null)
        {
            entry = _javaRepository.CreateEntry(CurrentWindow, SelectedNode, existing.ObjectKey, existing.FriendlyName);
            var index = project.Repository.IndexOf(existing);
            project.Repository[index] = entry;
        }
        else
        {
            var preferredName = $"{SelectedNode.Role}_{SelectedNode.Name}_{SelectedNode.IndexInParent}";
            var key = _javaRepository.CreateUniqueObjectKey(preferredName, project.Repository);
            entry = _javaRepository.CreateEntry(CurrentWindow, SelectedNode, key, SelectedNode.DisplayName);
            project.Repository.Add(entry);
        }

        var currentWindowScope = _javaRepository.CreateWindowLocator(CurrentWindow, Root);
        var existingWindow = project.Windows.FirstOrDefault(x => string.Equals(x.WindowKey, currentWindowScope.WindowKey, StringComparison.OrdinalIgnoreCase));
        if (existingWindow is null) project.Windows.Add(currentWindowScope);
        _javaRepository.SaveProject(path, project);
        RecordingStatus = $"Saved repository object {entry.ObjectKey} to {path}.";
        _logger.Log($"Repository object saved to file. ObjectKey='{entry.ObjectKey}', Path='{path}', CreateNew={createNew}.");
        return entry;
    }

    public JavaObjectRepositoryEntry? AddRepositoryEntryToRepositoryFile(JavaObjectRepositoryEntry sourceEntry, string path, bool createNew)
    {
        _logger.Debug($"Repository entry export requested. ObjectKey='{sourceEntry.ObjectKey}', Path='{path}', CreateNew={createNew}.");

        JavaRecordingProject project;
        if (!createNew && File.Exists(path))
        {
            project = _javaRepository.LoadProject(path);
        }
        else
        {
            var sessionName = Path.GetFileNameWithoutExtension(path);
            if (sessionName.EndsWith(".jrecording", StringComparison.OrdinalIgnoreCase))
                sessionName = Path.GetFileNameWithoutExtension(sessionName);
            project = new JavaRecordingProject
            {
                SchemaVersion = 2,
                SessionName = string.IsNullOrWhiteSpace(sessionName) ? "JavaRepository" : sessionName,
                ApplicationAlias = string.IsNullOrWhiteSpace(sourceEntry.WindowTitle) ? "JavaApplication" : sourceEntry.WindowTitle,
                CreatedAtUtc = DateTime.UtcNow,
                WindowTitle = sourceEntry.WindowTitle,
                WindowClassName = sourceEntry.WindowClassName
            };
        }

        var existing = project.Repository.FirstOrDefault(x =>
            string.Equals(x.ObjectKey, sourceEntry.ObjectKey, StringComparison.OrdinalIgnoreCase)
            || (string.Equals(x.Path, sourceEntry.Path, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Name, sourceEntry.Name, StringComparison.Ordinal)
                && string.Equals(x.WindowTitle, sourceEntry.WindowTitle, StringComparison.Ordinal)));

        var objectKey = existing?.ObjectKey
                        ?? (!project.Repository.Any(x => string.Equals(x.ObjectKey, sourceEntry.ObjectKey, StringComparison.OrdinalIgnoreCase))
                            ? sourceEntry.ObjectKey
                            : _javaRepository.CreateUniqueObjectKey(sourceEntry.ObjectKey, project.Repository));
        var entry = _javaRepository.CloneEntry(sourceEntry, objectKey, sourceEntry.FriendlyName);

        if (existing is not null)
        {
            var index = project.Repository.IndexOf(existing);
            project.Repository[index] = entry;
        }
        else
        {
            project.Repository.Add(entry);
        }

        var windowScope = _javaRepository.CreateWindowLocator(entry);
        var existingWindow = project.Windows.FirstOrDefault(x => string.Equals(x.WindowKey, windowScope.WindowKey, StringComparison.OrdinalIgnoreCase));
        if (existingWindow is null) project.Windows.Add(windowScope);
        _javaRepository.SaveProject(path, project);
        RecordingStatus = $"Saved repository object {entry.ObjectKey} to {path}.";
        _logger.Log($"Repository entry exported to file. ObjectKey='{entry.ObjectKey}', Path='{path}', CreateNew={createNew}.");
        return entry;
    }

    public JavaRecordedStep? RecordJavaAction(JavaRecordedActionKind actionKind, string? inputText = null, int? recordedScreenX = null, int? recordedScreenY = null, int? windowOffsetX = null, int? windowOffsetY = null)
    {
        _logger.Debug($"Record step requested. RecordingActive={IsRecordingActive}, Paused={IsRecordingPaused}, JavaMode={IsJavaMode}, HasSelectedNode={SelectedNode is not null}, Action={actionKind}, InputLength={(inputText ?? "").Length}.");
        if (!IsRecordingActive || IsRecordingPaused || !IsJavaMode || SelectedNode is null)
        {
            _logger.Debug("Record step skipped because recording is inactive, paused, mode is not Java, or no node is selected.");
            return null;
        }

        var entry = AddSelectedNodeToRepository();
        if (entry is null)
        {
            _logger.Log("Record step aborted because repository capture returned null.");
            return null;
        }

        var step = _javaRepository.CreateRecordedStep(
            entry,
            actionKind,
            RecordedSteps.Count + 1,
            inputText,
            CurrentWindow,
            recordedScreenX,
            recordedScreenY,
            windowOffsetX,
            windowOffsetY);
        RecordedSteps.Add(step);
        RememberRecordingWindowScope(CurrentWindow);
        SelectedRecordedStep = step;
        RecordingStatus = $"Recorded step {step.Sequence}: {actionKind} on {entry.ObjectKey}.";
        RefreshRecordingSurface();
        _logger.Log($"Recorded step created. Sequence={step.Sequence}, Action={step.ActionKind}, ObjectKey='{step.ObjectKey}', InputLength={step.InputText.Length}, TotalSteps={RecordedSteps.Count}.");
        return step;
    }

    public JavaRecordedStep? RecordJavaActionForNode(JavaRecordedActionKind actionKind, JavaWindowInfo window, AccessibleNode node, string? inputText = null, int? recordedScreenX = null, int? recordedScreenY = null, int? windowOffsetX = null, int? windowOffsetY = null)
    {
        _logger.Debug($"Record step for explicit node requested. RecordingActive={IsRecordingActive}, Paused={IsRecordingPaused}, JavaMode={IsJavaMode}, Window='{window.Title}', Node='{node.DisplayName}', Action={actionKind}, InputLength={(inputText ?? "").Length}.");
        if (!IsRecordingActive || IsRecordingPaused || !IsJavaMode)
        {
            _logger.Debug("Record explicit-node step skipped because recording is inactive, paused, or mode is not Java.");
            return null;
        }

        var entry = CaptureNodeToRepository(window, node, friendlyName: null, updateStatus: false);
        if (entry is null)
        {
            _logger.Log("Record explicit-node step aborted because repository capture returned null.");
            return null;
        }

        var step = _javaRepository.CreateRecordedStep(
            entry,
            actionKind,
            RecordedSteps.Count + 1,
            inputText,
            window,
            recordedScreenX,
            recordedScreenY,
            windowOffsetX,
            windowOffsetY);
        RecordedSteps.Add(step);
        RememberRecordingWindowScope(window);
        SelectedRepositoryEntry = entry;
        SelectedRecordedStep = step;
        RecordingStatus = $"Recorded step {step.Sequence}: {actionKind} on {entry.ObjectKey}.";
        RefreshRecordingSurface();
        _logger.Log($"Recorded explicit-node step created. Sequence={step.Sequence}, Action={step.ActionKind}, ObjectKey='{step.ObjectKey}', Window='{window.Title}', InputLength={step.InputText.Length}, TotalSteps={RecordedSteps.Count}.");
        return step;
    }

    public JavaRecordedStep? RecordJavaWindowAction(JavaRecordedActionKind actionKind, JavaWindowInfo window, string? inputText = null, int? recordedScreenX = null, int? recordedScreenY = null, int? windowOffsetX = null, int? windowOffsetY = null)
    {
        _logger.Debug($"Record window action requested. RecordingActive={IsRecordingActive}, Paused={IsRecordingPaused}, JavaMode={IsJavaMode}, Window='{window.Title}', Action={actionKind}, InputLength={(inputText ?? "").Length}.");
        if (!IsRecordingActive || IsRecordingPaused || !IsJavaMode)
        {
            _logger.Debug("Record window action skipped because recording is inactive, paused, or mode is not Java.");
            return null;
        }

        var step = _javaRepository.CreateWindowActionStep(
            window,
            actionKind,
            RecordedSteps.Count + 1,
            inputText,
            recordedScreenX,
            recordedScreenY,
            windowOffsetX,
            windowOffsetY);
        RecordedSteps.Add(step);
        RememberRecordingWindowScope(window);
        SelectedRecordedStep = step;
        RecordingStatus = $"Recorded step {step.Sequence}: {actionKind} on window {window.Title}.";
        RefreshRecordingSurface();
        _logger.Log($"Recorded window action step created. Sequence={step.Sequence}, Action={step.ActionKind}, Window='{window.Title}', Hwnd={window.HwndDisplay}, TotalSteps={RecordedSteps.Count}.");
        return step;
    }

    public bool PromoteLastRecordedClickToDoubleClick()
    {
        _logger.Debug($"Promote last recorded click requested. RecordingActive={IsRecordingActive}, HasSelectedNode={SelectedNode is not null}, StepCount={RecordedSteps.Count}.");
        if (!IsRecordingActive || SelectedNode is null || RecordedSteps.Count == 0)
        {
            _logger.Log("Promote last recorded click skipped because recording is inactive, no selected node exists, or there are no steps.");
            return false;
        }

        var entry = AddSelectedNodeToRepository();
        if (entry is null)
        {
            _logger.Log("Promote last recorded click aborted because repository entry resolution returned null.");
            return false;
        }

        var last = RecordedSteps[^1];
        if (last.ActionKind != JavaRecordedActionKind.Click || !string.Equals(last.ObjectKey, entry.ObjectKey, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Log($"Promote last recorded click skipped because last step does not match. LastAction={last.ActionKind}, LastObjectKey='{last.ObjectKey}', CurrentObjectKey='{entry.ObjectKey}'.");
            return false;
        }

        var upgraded = _javaRepository.PromoteClickToDoubleClick(last);

        RecordedSteps[^1] = upgraded;
        SelectedRecordedStep = upgraded;
        RecordingStatus = $"Upgraded step {upgraded.Sequence} to DoubleClick on {entry.ObjectKey}.";
        RefreshRecordingSurface();
        _logger.Log($"Promoted last recorded click to double-click. Sequence={upgraded.Sequence}, ObjectKey='{upgraded.ObjectKey}'.");
        return true;
    }

    private JavaObjectRepositoryEntry? CaptureNodeToRepository(JavaWindowInfo window, AccessibleNode node, string? friendlyName, bool updateStatus)
    {
        RefreshBounds(node);

        var existing = RepositoryEntries.FirstOrDefault(x => string.Equals(x.Path, node.Path, StringComparison.OrdinalIgnoreCase) &&
                                                             string.Equals(x.Name, node.Name, StringComparison.Ordinal) &&
                                                             string.Equals(x.WindowTitle, window.Title, StringComparison.Ordinal) &&
                                                             string.Equals(x.WindowClassName, window.ClassName, StringComparison.Ordinal));
        if (existing is not null)
        {
            var refreshed = _javaRepository.CreateEntry(window, node, existing.ObjectKey, existing.FriendlyName);
            var index = RepositoryEntries.IndexOf(existing);
            RepositoryEntries[index] = refreshed;
            RememberRecordingWindowScope(window);
            SelectedRepositoryEntry = refreshed;
            if (updateStatus)
            {
                RecordingStatus = $"Repository refreshed {refreshed.ObjectKey}.";
                RefreshRecordingSurface();
            }
            _logger.Log($"Repository capture refreshed existing object. ObjectKey='{refreshed.ObjectKey}', Path='{refreshed.Path}', Name='{refreshed.Name}', Window='{window.Title}', LocatorJsonLength={refreshed.LocatorJson.Length}.");
            return refreshed;
        }

        var preferredName = friendlyName ?? $"{node.Role}_{node.Name}_{node.IndexInParent}";
        var key = _javaRepository.CreateUniqueObjectKey(preferredName, RepositoryEntries);
        var entry = _javaRepository.CreateEntry(window, node, key, friendlyName ?? node.DisplayName);
        RepositoryEntries.Add(entry);
        RememberRecordingWindowScope(window);
        SelectedRepositoryEntry = entry;
        if (updateStatus)
        {
            RecordingStatus = $"Captured repository object {entry.ObjectKey}.";
            RefreshRecordingSurface();
        }
        _logger.Log($"Repository object captured. ObjectKey='{entry.ObjectKey}', FriendlyName='{entry.FriendlyName}', Path='{entry.Path}', Role='{entry.Role}', Name='{entry.Name}', Window='{window.Title}', ParentRole='{entry.ParentRole}', ParentName='{entry.ParentName}', LocatorJsonLength={entry.LocatorJson.Length}.");
        return entry;
    }

    public bool DeleteSelectedRecordedStep()
    {
        if (SelectedRecordedStep is null)
        {
            RecordingStatus = "Select a recorded step before deleting.";
            return false;
        }

        var deleted = SelectedRecordedStep;
        var targetSequence = deleted.Sequence;
        var remaining = RecordedSteps
            .Where(step => !ReferenceEquals(step, deleted))
            .OrderBy(step => step.Sequence)
            .ToList();

        RecordedSteps.Clear();
        for (var i = 0; i < remaining.Count; i++)
        {
            remaining[i].Sequence = i + 1;
            RecordedSteps.Add(remaining[i]);
        }

        SelectedRecordedStep = RecordedSteps.FirstOrDefault(step => step.Sequence >= Math.Min(targetSequence, RecordedSteps.Count))
                               ?? RecordedSteps.LastOrDefault();
        RecordingStatus = $"Deleted recorded step {targetSequence}. {RecordingStepCount} step(s) remain.";
        RefreshRecordingSurface();
        _logger.Log($"Deleted recorded step. PreviousSequence={targetSequence}, RemainingSteps={RecordingStepCount}.");
        return true;
    }

    public bool MoveRecordedStep(JavaRecordedStep step, int targetIndex)
    {
        if (step is null)
        {
            RecordingStatus = "Select a recorded step before moving it.";
            return false;
        }

        var currentIndex = RecordedSteps.IndexOf(step);
        if (currentIndex < 0)
        {
            RecordingStatus = "The selected recorded step was not found.";
            return false;
        }

        var boundedTargetIndex = Math.Max(0, Math.Min(targetIndex, RecordedSteps.Count - 1));
        if (currentIndex == boundedTargetIndex) return false;

        var ordered = RecordedSteps.ToList();
        ordered.RemoveAt(currentIndex);
        ordered.Insert(boundedTargetIndex, step);

        RecordedSteps.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Sequence = i + 1;
            RecordedSteps.Add(ordered[i]);
        }

        SelectedRecordedStep = step;
        RecordingStatus = $"Moved step to position {step.Sequence}.";
        RefreshRecordingSurface();
        _logger.Log($"Recorded step moved. ObjectKey='{step.ObjectKey}', NewSequence={step.Sequence}, TargetIndex={boundedTargetIndex}, TotalSteps={RecordedSteps.Count}.");
        return true;
    }

    public int CountRecordedStepsUsingSelectedRepositoryEntry()
    {
        if (SelectedRepositoryEntry is null) return 0;
        return RecordedSteps.Count(step => string.Equals(step.ObjectKey, SelectedRepositoryEntry.ObjectKey, StringComparison.OrdinalIgnoreCase));
    }

    public bool DeleteSelectedRepositoryEntry(bool deleteReferencingSteps = false)
    {
        if (SelectedRepositoryEntry is null)
        {
            RecordingStatus = "Select a repository object before deleting.";
            return false;
        }

        var deleted = SelectedRepositoryEntry;
        var referencingSteps = RecordedSteps
            .Where(step => string.Equals(step.ObjectKey, deleted.ObjectKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(step => step.Sequence)
            .ToList();

        if (referencingSteps.Count > 0 && !deleteReferencingSteps)
        {
            RecordingStatus = $"Repository object '{deleted.ObjectKey}' is used by {referencingSteps.Count} recorded step(s). Confirm deletion from Recorder Studio to remove those steps too.";
            _logger.Log($"Repository delete blocked pending confirmation. ObjectKey='{deleted.ObjectKey}', ReferencingSteps={referencingSteps.Count}.");
            return false;
        }

        RepositoryEntries.Remove(deleted);
        if (referencingSteps.Count > 0)
        {
            var remaining = RecordedSteps
                .Where(step => !string.Equals(step.ObjectKey, deleted.ObjectKey, StringComparison.OrdinalIgnoreCase))
                .OrderBy(step => step.Sequence)
                .ToList();
            RecordedSteps.Clear();
            for (var i = 0; i < remaining.Count; i++)
            {
                remaining[i].Sequence = i + 1;
                RecordedSteps.Add(remaining[i]);
            }
        }

        SelectedRepositoryEntry = RepositoryEntries.FirstOrDefault();
        SelectedRecordedStep = RecordedSteps.FirstOrDefault();
        RecordingStatus = referencingSteps.Count == 0
            ? $"Deleted repository object {deleted.ObjectKey}."
            : $"Deleted repository object {deleted.ObjectKey} and {referencingSteps.Count} dependent step(s).";
        RefreshRecordingSurface();
        _logger.Log($"Repository object deleted. ObjectKey='{deleted.ObjectKey}', DeletedReferencingSteps={referencingSteps.Count}, RemainingObjects={RecordingObjectCount}, RemainingSteps={RecordingStepCount}.");
        return true;
    }

    public bool RenameSelectedRepositoryEntry(string objectKey, string friendlyName)
    {
        if (SelectedRepositoryEntry is null)
        {
            RecordingStatus = "Select a repository object before renaming.";
            return false;
        }

        var oldKey = SelectedRepositoryEntry.ObjectKey;
        var requestedKey = string.IsNullOrWhiteSpace(objectKey)
            ? oldKey
            : objectKey.Trim();
        if (!string.Equals(oldKey, requestedKey, StringComparison.OrdinalIgnoreCase)
            && RepositoryEntries.Any(x => !ReferenceEquals(x, SelectedRepositoryEntry)
                                          && string.Equals(x.ObjectKey, requestedKey, StringComparison.OrdinalIgnoreCase)))
        {
            RecordingStatus = $"Repository object key '{requestedKey}' already exists.";
            return false;
        }

        SelectedRepositoryEntry.ObjectKey = requestedKey;
        SelectedRepositoryEntry.FriendlyName = string.IsNullOrWhiteSpace(friendlyName)
            ? SelectedRepositoryEntry.FriendlyName
            : friendlyName.Trim();

        if (!string.Equals(oldKey, requestedKey, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var step in RecordedSteps.Where(step => string.Equals(step.ObjectKey, oldKey, StringComparison.OrdinalIgnoreCase)))
            {
                step.ObjectKey = requestedKey;
                step.StepName = $"{step.ActionKind} {requestedKey}";
            }
        }

        RefreshRepositoryEntry(SelectedRepositoryEntry);
        RecordingStatus = $"Repository object renamed to {SelectedRepositoryEntry.ObjectKey}.";
        RefreshRecordingSurface();
        _logger.Log($"Repository object renamed. OldKey='{oldKey}', NewKey='{SelectedRepositoryEntry.ObjectKey}', FriendlyName='{SelectedRepositoryEntry.FriendlyName}'.");
        return true;
    }

    public bool ApplySelectedRepositoryPropertyEdits()
    {
        if (SelectedRepositoryEntry is null)
        {
            RecordingStatus = "Select a repository object before applying property edits.";
            return false;
        }

        _javaRepository.ApplyPropertyEdits(SelectedRepositoryEntry);
        RefreshRepositoryEntry(SelectedRepositoryEntry);
        RecordingStatus = $"Applied property edits to {SelectedRepositoryEntry.ObjectKey}.";
        RefreshRecordingSurface();
        _logger.Log($"Repository property edits applied. ObjectKey='{SelectedRepositoryEntry.ObjectKey}', Properties={SelectedRepositoryEntry.Properties.Count}, LocatorJsonLength={SelectedRepositoryEntry.LocatorJson.Length}.");
        return true;
    }

    public bool AddPropertyToSelectedRepositoryEntry(string name = "custom.property", string value = "")
    {
        if (SelectedRepositoryEntry is null)
        {
            RecordingStatus = "Select a repository object before adding a property.";
            return false;
        }

        var baseName = string.IsNullOrWhiteSpace(name) ? "custom.property" : name.Trim();
        var candidate = baseName;
        for (var i = 2; SelectedRepositoryEntry.Properties.Any(x => string.Equals(x.Name, candidate, StringComparison.OrdinalIgnoreCase)); i++)
        {
            candidate = $"{baseName}.{i}";
        }

        SelectedRepositoryEntry.Properties.Add(new JavaRepositoryProperty { Name = candidate, Value = value, IsPrimary = false });
        RefreshRepositoryEntry(SelectedRepositoryEntry);
        RecordingStatus = $"Added property '{candidate}' to {SelectedRepositoryEntry.ObjectKey}.";
        return true;
    }

    public bool DeletePropertyFromSelectedRepositoryEntry(JavaRepositoryProperty? property)
    {
        if (SelectedRepositoryEntry is null || property is null)
        {
            RecordingStatus = "Select a repository property before deleting.";
            return false;
        }

        SelectedRepositoryEntry.Properties.Remove(property);
        _javaRepository.ApplyPropertyEdits(SelectedRepositoryEntry);
        RefreshRepositoryEntry(SelectedRepositoryEntry);
        RecordingStatus = $"Deleted property '{property.Name}' from {SelectedRepositoryEntry.ObjectKey}.";
        return true;
    }

    public void RefreshSelectedRepositoryPreview()
    {
        if (SelectedRepositoryEntry is null) RecordingRepositoryPreview = "Select a recorded object to inspect its repository properties.";
        else RecordingRepositoryPreview = _javaRepository.BuildPropertiesPreview(SelectedRepositoryEntry);
    }

    public AccessibleNode? ResolveSelectedRepositoryEntry(out string message, ResolutionPolicy? policy = null)
    {
        message = "";
        if (!IsJavaMode)
        {
            message = "Attach to a Java window before resolving repository objects.";
            return null;
        }

        if (SelectedRepositoryEntry is null)
        {
            message = "Select a repository object first.";
            return null;
        }

        if (!TryAutoAttachJavaWindowForRepositoryEntry(SelectedRepositoryEntry, out message))
        {
            return null;
        }

        if (Root is null)
        {
            message = "Attach to a Java window before resolving repository objects.";
            return null;
        }

        var effectivePolicy = (policy ?? ResolutionPolicy.Default).Sanitize();
        var resolution = _javaResolver.ResolveDetailed(Root, SelectedRepositoryEntry, null, effectivePolicy);
        if (!resolution.Success || resolution.Node is null)
        {
            message = resolution.Message;
            return null;
        }

        message = $"Resolved {SelectedRepositoryEntry.ObjectKey} using {resolution.StrategyName}.";
        return resolution.Node;
    }

    public bool TryAutoAttachJavaWindowForRepositoryEntry(JavaObjectRepositoryEntry entry, out string message)
    {
        message = "";
        if (CurrentWindow is not null
            && (EntryMatchesWindow(entry, CurrentWindow)
                || (TryFindRepositoryWindowScope(entry, out var currentScope)
                    && currentScope is not null
                    && WindowMatchesScope(currentScope, CurrentWindow)
                    && ScopeProcessMatches(currentScope, CurrentWindow)))
            && Root is not null)
        {
            return true;
        }

        JavaWindowInfo? match = null;
        IReadOnlyList<JavaWindowInfo> lastWindows = [];
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            var service = new JavaWindowDiscoveryService(_bridge, _logger);
            var windows = service.GetJavaWindows();
            lastWindows = windows;
            match = ResolveRepositoryWindowMatch(entry, windows);
            if (match is not null) break;
            if (attempt < 6) Thread.Sleep(220);
        }

        if (match is null)
        {
            var discovered = lastWindows.Count == 0
                ? "(none)"
                : string.Join(" | ", lastWindows.Select(x => $"{x.Title} [{x.HwndDisplay}] pid={x.ProcessId} vm={x.VmId} class={x.ClassName}"));
            message = $"Could not find modal/window '{entry.WindowTitle}' for repository object '{entry.ObjectKey}'. Discovered Java windows: {discovered}";
            _logger.Log(message);
            return false;
        }

        var attached = TryAutoAttachJavaWindow(match.Hwnd, $"repository object {entry.ObjectKey}");
        message = attached
            ? $"Attached modal/window '{match.Title}' for repository object '{entry.ObjectKey}'."
            : $"Failed to auto-attach modal/window '{match.Title}' for repository object '{entry.ObjectKey}'.";
        if (!attached) _logger.Log(message);
        return attached;
    }

    private void RefreshRepositoryEntry(JavaObjectRepositoryEntry entry)
    {
        var index = RepositoryEntries.IndexOf(entry);
        if (index >= 0)
        {
            RepositoryEntries.RemoveAt(index);
            RepositoryEntries.Insert(index, entry);
            SelectedRepositoryEntry = entry;
        }
        else
        {
            SelectedRepositoryEntry = entry;
        }

        RefreshSelectedRepositoryPreview();
    }

    public string GetDefaultRecordingProjectFileName()
    {
        if (!string.IsNullOrWhiteSpace(RecordingProjectPath))
        {
            var currentFileName = Path.GetFileName(RecordingProjectPath);
            if (!string.IsNullOrWhiteSpace(currentFileName)) return currentFileName;
        }

        var logicalName = !string.IsNullOrWhiteSpace(RecordingSessionName)
                          && !string.Equals(RecordingSessionName, "No active recording session", StringComparison.OrdinalIgnoreCase)
            ? RecordingSessionName
            : !string.IsNullOrWhiteSpace(RecordingApplicationAlias)
                ? $"{RecordingApplicationAlias}_{DateTime.Now:yyyyMMdd_HHmmss}"
                : $"JavaRecording_{DateTime.Now:yyyyMMdd_HHmmss}";
        return $"{SanitizeFileName(logicalName)}.jrecording.json";
    }

    public bool SaveRecordingProject(string? path = null)
    {
        _logger.Debug($"Save recording project requested. HasWindow={CurrentWindow is not null}, RepositoryCount={RepositoryEntries.Count}, StepCount={RecordedSteps.Count}, RequestedPath='{path ?? ""}'.");
        if (!IsJavaMode)
        {
            RecordingStatus = "Java recording projects can only be saved in Java mode.";
            _logger.Log($"Save recording project rejected. Status='{RecordingStatus}'.");
            return false;
        }
        if (RepositoryEntries.Count == 0 && RecordedSteps.Count == 0)
        {
            RecordingStatus = "There is nothing to save yet. Capture at least one repository object or step.";
            _logger.Log($"Save recording project rejected. Status='{RecordingStatus}'.");
            return false;
        }

        var savePath = string.IsNullOrWhiteSpace(path) ? RecordingProjectPath : path!;
        var project = CurrentWindow is not null
            ? _javaRepository.CreateProject(
                string.IsNullOrWhiteSpace(RecordingSessionName) ? "JavaRecording" : RecordingSessionName,
                string.IsNullOrWhiteSpace(RecordingApplicationAlias) ? CurrentWindow.Title : RecordingApplicationAlias,
                CurrentWindow)
            : new JavaRecordingProject
            {
                SchemaVersion = 2,
                SessionName = string.IsNullOrWhiteSpace(RecordingSessionName) ? "JavaRepository" : RecordingSessionName,
                ApplicationAlias = string.IsNullOrWhiteSpace(RecordingApplicationAlias) ? "JavaApplication" : RecordingApplicationAlias,
                CreatedAtUtc = DateTime.UtcNow,
                WindowTitle = RecordingWindowScopes.FirstOrDefault()?.Title ?? RepositoryEntries.FirstOrDefault()?.WindowTitle ?? "",
                WindowClassName = RecordingWindowScopes.FirstOrDefault()?.ClassName ?? RepositoryEntries.FirstOrDefault()?.WindowClassName ?? ""
            };
        project.Repository = RepositoryEntries.ToList();
        project.Steps = RecordedSteps.ToList();
        project.Windows = RecordingWindowScopes.ToList();
        _javaRepository.SaveProject(savePath, project);
        RecordingProjectPath = savePath;
        RecordingStatus = $"Recording project saved to {savePath}.";
        RefreshRecordingSurface();
        _logger.Log($"Recording project saved. Path='{savePath}', Objects={project.Repository.Count}, Steps={project.Steps.Count}.");
        return true;
    }

    public bool LoadRecordingProject(string path)
    {
        _logger.Debug($"Load recording project requested. Path='{path}'.");
        var project = _javaRepository.LoadProject(path);
        RepositoryEntries.Clear();
        foreach (var entry in project.Repository) RepositoryEntries.Add(entry);
        RecordedSteps.Clear();
        foreach (var step in project.Steps.OrderBy(x => x.Sequence)) RecordedSteps.Add(step);
        RecordingWindowScopes.Clear();
        foreach (var window in project.Windows) RecordingWindowScopes.Add(window);
        RecordingSessionName = project.SessionName;
        RecordingApplicationAlias = project.ApplicationAlias;
        RecordingProjectPath = path;
        IsRecordingActive = false;
        IsRecordingPaused = false;
        SelectedRepositoryEntry = RepositoryEntries.FirstOrDefault();
        SelectedRecordedStep = RecordedSteps.FirstOrDefault();
        RecordingStatus = $"Loaded recording project '{project.SessionName}' with {RepositoryEntries.Count} object(s) and {RecordedSteps.Count} step(s).";
        PlaybackOutput = "Playback output will appear here.";
        RefreshRecordingSurface();
        _logger.Log($"Recording project loaded. Session='{project.SessionName}', Alias='{project.ApplicationAlias}', Objects={RepositoryEntries.Count}, Steps={RecordedSteps.Count}.");
        return true;
    }

    public AccessibleNode? ResolveRecordedStep(JavaRecordedStep step, out string message, ResolutionPolicy? policy = null)
    {
        _logger.Debug($"Resolve recorded step requested. Sequence={step.Sequence}, Action={step.ActionKind}, ObjectKey='{step.ObjectKey}', HasRoot={Root is not null}.");
        message = "";
        if (!IsJavaMode || Root is null)
        {
            message = "Attach to a Java window before playback.";
            _logger.Log($"Resolve recorded step failed. Reason='{message}'.");
            return null;
        }

        var entry = RepositoryEntries.FirstOrDefault(x => string.Equals(x.ObjectKey, step.ObjectKey, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            message = $"Repository object '{step.ObjectKey}' was not found.";
            _logger.Log($"Resolve recorded step failed. Reason='{message}'.");
            return null;
        }

        var effectivePolicy = (policy ?? ResolutionPolicy.Default).Sanitize();
        var started = DateTime.UtcNow;
        var attempt = 0;
        var refreshedAfterFailure = false;
        ResolutionResult resolution;

        while (true)
        {
            attempt++;
            resolution = _javaResolver.ResolveDetailed(Root, entry, step, effectivePolicy);
            if (resolution.Success && resolution.Node is not null) break;

            var elapsedMs = (int)(DateTime.UtcNow - started).TotalMilliseconds;
            if (effectivePolicy.TimeoutMs == 0 || elapsedMs >= effectivePolicy.TimeoutMs) break;

            if (effectivePolicy.RefreshTreeOnFailure && !refreshedAfterFailure)
            {
                refreshedAfterFailure = true;
                RefreshCurrentJavaTree($"playback resolve retry for step {step.Sequence}");
            }

            Thread.Sleep(Math.Min(effectivePolicy.PollIntervalMs, Math.Max(50, effectivePolicy.TimeoutMs - elapsedMs)));
        }

        if (!resolution.Success || resolution.Node is null)
        {
            var closest = resolution.Candidates.Count == 0
                ? "No close candidates."
                : string.Join(" | ", resolution.Candidates.Take(3).Select(candidate =>
                    $"{candidate.DisplayName} score={candidate.Score} mismatches={string.Join("; ", candidate.Mismatches.Take(3))}"));
            message = $"{resolution.Message} Attempts={attempt}. Closest candidates: {closest}";
            _logger.Log($"Resolve recorded step failed. Status={resolution.Status}, Reason='{message}'.");
            return null;
        }

        message = $"Resolved {step.ObjectKey} to {resolution.Node.DisplayName} using {resolution.StrategyName} after {attempt} attempt(s).";
        _logger.Debug($"Resolve recorded step succeeded. Sequence={step.Sequence}, ObjectKey='{step.ObjectKey}', Strategy='{resolution.StrategyName}', ResolvedNode='{resolution.Node.DisplayName}', Path='{resolution.Node.Path}'.");
        return resolution.Node;
    }

    public bool RefreshCurrentJavaTree(string reason)
    {
        _logger.Debug($"Refresh current Java tree requested. Reason='{reason}', HasWindow={CurrentWindow is not null}.");
        if (!IsJavaMode || CurrentWindow is null)
        {
            _logger.Log("Refresh current Java tree skipped because Java mode is inactive or no current window is attached.");
            return false;
        }

        var crawler = new AccessibleTreeCrawler(_bridge, _logger);
        var root = crawler.BuildTree(CurrentWindow);
        if (root is null)
        {
            _logger.Log($"Refresh current Java tree failed. Reason='{reason}'.");
            return false;
        }

        Tree.Clear();
        _root = root;
        Tree.Add(root);
        _nodesByContext.Clear();
        IndexNodes(root);
        SelectedNode = root;
        OnPropertyChanged(nameof(Root));
        OnPropertyChanged(nameof(CurrentTreeItems));
        OnPropertyChanged(nameof(CanUseJavaRecording));
        _logger.Log($"Refresh current Java tree succeeded. Reason='{reason}', NodesIndexed={crawler.NodeCount}, Root='{root.DisplayName}'.");
        return true;
    }

    public bool TryAutoAttachJavaWindow(IntPtr hwnd, string reason)
    {
        _logger.Debug($"Auto-attach requested. Reason='{reason}', TargetHwnd=0x{hwnd.ToInt64():X}, CurrentHwnd='{CurrentWindow?.HwndDisplay ?? ""}'.");
        if (hwnd == IntPtr.Zero) return false;
        if (CurrentWindow?.Hwnd == hwnd && Root is not null)
        {
            _logger.Debug("Auto-attach skipped because target Java window is already attached.");
            return true;
        }
        if (!_bridge.Initialize())
        {
            _logger.Log("Auto-attach failed because Access Bridge initialization is not available.");
            return false;
        }
        if (!_bridge.IsJavaWindow(hwnd))
        {
            _logger.Debug($"Auto-attach skipped because hwnd 0x{hwnd.ToInt64():X} is not a Java window.");
            return false;
        }

        var service = new JavaWindowDiscoveryService(_bridge, _logger);
        var windows = service.GetJavaWindows();
        var match = windows.FirstOrDefault(x => x.Hwnd == hwnd)
                    ?? windows.FirstOrDefault(x => x.ProcessId == CurrentWindow?.ProcessId && string.Equals(x.Title, CurrentWindow?.Title, StringComparison.Ordinal));
        if (match is null)
        {
            _logger.Log($"Auto-attach failed because the target Java modal/window 0x{hwnd.ToInt64():X} was not discovered.");
            return false;
        }

        SelectedJavaWindow = JavaWindows.FirstOrDefault(x => x.Model.Hwnd == match.Hwnd) ?? new JavaWindowViewModel(match);
        var root = BuildStableJavaTree(match, reason, out var finalNodeCount);
        if (root is null)
        {
            _logger.Log($"Auto-attach failed because tree build returned null for modal '{match.Title}'.");
            return false;
        }

        JavaWindows.Clear();
        foreach (var window in windows) JavaWindows.Add(new(window));
        SelectedJavaWindow = JavaWindows.FirstOrDefault(x => x.Model.Hwnd == match.Hwnd) ?? new JavaWindowViewModel(match);
        Tree.Clear();
        _root = root;
        Tree.Add(root);
        _nodesByContext.Clear();
        IndexNodes(root);
        SelectedNode = root;
        OnPropertyChanged(nameof(Root));
        OnPropertyChanged(nameof(CurrentWindowItems));
        OnPropertyChanged(nameof(WindowItemCount));
        OnPropertyChanged(nameof(CanUseJavaRecording));
        Status = $"Auto-attached {match.Title}";
        _logger.Log($"Auto-attach succeeded. Reason='{reason}', AttachedWindow='{match.Title}', Hwnd={match.HwndDisplay}, VmId={match.VmId}, NodesIndexed={finalNodeCount}.");
        return true;
    }

    public async Task<bool> TryAutoAttachJavaWindowAsync(IntPtr hwnd, string reason)
    {
        _logger.Debug($"Async auto-attach requested. Reason='{reason}', TargetHwnd=0x{hwnd.ToInt64():X}, CurrentHwnd='{CurrentWindow?.HwndDisplay ?? ""}'.");
        if (hwnd == IntPtr.Zero) return false;
        if (CurrentWindow?.Hwnd == hwnd && Root is not null)
        {
            _logger.Debug("Async auto-attach skipped because target Java window is already attached.");
            return true;
        }

        var currentProcessId = CurrentWindow?.ProcessId;
        var currentTitle = CurrentWindow?.Title;
        var result = await Task.Run(() =>
        {
            if (!_bridge.Initialize())
                return new AutoAttachResult(false, "Access Bridge initialization is not available.", null, [], null, 0);

            if (!_bridge.IsJavaWindow(hwnd))
                return new AutoAttachResult(false, $"hwnd 0x{hwnd.ToInt64():X} is not a Java window.", null, [], null, 0);

            var service = new JavaWindowDiscoveryService(_bridge, _logger);
            var windows = service.GetJavaWindows();
            var match = windows.FirstOrDefault(x => x.Hwnd == hwnd)
                        ?? windows.FirstOrDefault(x => x.ProcessId == currentProcessId && string.Equals(x.Title, currentTitle, StringComparison.Ordinal));

            if (match is null)
                return new AutoAttachResult(false, $"target Java modal/window 0x{hwnd.ToInt64():X} was not discovered.", null, windows, null, 0);

            var root = BuildStableJavaTree(match, reason, out var finalNodeCount);
            return root is null
                ? new AutoAttachResult(false, $"tree build returned null for modal '{match.Title}'.", match, windows, null, 0)
                : new AutoAttachResult(true, "", match, windows, root, finalNodeCount);
        });

        if (!result.Succeeded || result.Match is null || result.Root is null)
        {
            _logger.Log($"Async auto-attach failed. Reason='{reason}', Details='{result.Message}'.");
            return false;
        }

        JavaWindows.Clear();
        foreach (var window in result.Windows) JavaWindows.Add(new(window));
        if (JavaWindows.All(x => x.Model.Hwnd != result.Match.Hwnd)) JavaWindows.Add(new(result.Match));

        _suppressAutoAttachOnSelection = true;
        try
        {
            SelectedJavaWindow = JavaWindows.FirstOrDefault(x => x.Model.Hwnd == result.Match.Hwnd) ?? new JavaWindowViewModel(result.Match);
        }
        finally
        {
            _suppressAutoAttachOnSelection = false;
        }

        Tree.Clear();
        _root = result.Root;
        Tree.Add(result.Root);
        _nodesByContext.Clear();
        IndexNodes(result.Root);
        SelectedNode = result.Root;
        OnPropertyChanged(nameof(Root));
        OnPropertyChanged(nameof(CurrentWindowItems));
        OnPropertyChanged(nameof(WindowItemCount));
        OnPropertyChanged(nameof(CanUseJavaRecording));
        Status = $"Auto-attached {result.Match.Title}";
        _logger.Log($"Async auto-attach succeeded. Reason='{reason}', AttachedWindow='{result.Match.Title}', Hwnd={result.Match.HwndDisplay}, VmId={result.Match.VmId}, NodesIndexed={result.NodeCount}.");
        return true;
    }

    public bool TryAutoAttachJavaWindowForRecordedStep(JavaRecordedStep step, out string message)
    {
        message = "";
        var stepScope = FindRecordedWindowScope(step);
        if (CurrentWindow is not null
            && ((stepScope is not null && WindowMatchesScope(stepScope, CurrentWindow) && ScopeProcessMatches(stepScope, CurrentWindow))
                || (string.Equals(CurrentWindow.Title, step.WindowTitle, StringComparison.Ordinal)
                    && string.Equals(CurrentWindow.ClassName, step.WindowClassName, StringComparison.Ordinal)
                    && CurrentWindow.ProcessId == step.WindowProcessId))
            && Root is not null)
        {
            return true;
        }

        JavaWindowInfo? match = null;
        IReadOnlyList<JavaWindowInfo> lastWindows = [];
        for (var attempt = 1; attempt <= 8; attempt++)
        {
            var service = new JavaWindowDiscoveryService(_bridge, _logger);
            var windows = service.GetJavaWindows();
            lastWindows = windows;
            match = windows.FirstOrDefault(x =>
                        WindowMatchesScope(stepScope, x)
                        && ScopeProcessMatches(stepScope, x))
                    ?? windows.FirstOrDefault(x =>
                        string.Equals(x.Title, step.WindowTitle, StringComparison.Ordinal)
                        && string.Equals(x.ClassName, step.WindowClassName, StringComparison.Ordinal)
                        && x.ProcessId == step.WindowProcessId)
                    ?? windows.FirstOrDefault(x =>
                        string.Equals(x.Title, step.WindowTitle, StringComparison.Ordinal)
                        && x.VmId == step.WindowVmId)
                    ?? windows.FirstOrDefault(x =>
                        x.ProcessId == step.WindowProcessId
                        && string.Equals(x.ClassName, step.WindowClassName, StringComparison.Ordinal)
                        && x.Title.Contains(step.WindowTitle, StringComparison.OrdinalIgnoreCase))
                    ?? windows.FirstOrDefault(x =>
                        x.ProcessId == step.WindowProcessId
                        && string.Equals(x.Title, step.WindowTitle, StringComparison.OrdinalIgnoreCase))
                    ?? windows.FirstOrDefault(x =>
                        x.VmId == step.WindowVmId
                        && string.Equals(x.Title, step.WindowTitle, StringComparison.OrdinalIgnoreCase))
                    ?? windows.FirstOrDefault(x => string.Equals(x.HwndDisplay, step.WindowHwndDisplay, StringComparison.OrdinalIgnoreCase));

            _logger.Debug($"Recorded-step auto-attach attempt {attempt} for step {step.Sequence} scanned {windows.Count} Java window(s). MatchFound={match is not null}.");
            if (match is not null) break;
            if (attempt < 8) Thread.Sleep(250);
        }

        if (match is null)
        {
            var discovered = lastWindows.Count == 0
                ? "(none)"
                : string.Join(" | ", lastWindows.Select(x => $"{x.Title} [{x.HwndDisplay}] pid={x.ProcessId} vm={x.VmId} class={x.ClassName}"));
            message = $"Could not find modal/window '{step.WindowTitle}' for recorded step {step.Sequence}. Discovered Java windows: {discovered}";
            _logger.Log(message);
            return false;
        }

        var attached = TryAutoAttachJavaWindow(match.Hwnd, $"playback step {step.Sequence}");
        message = attached
            ? $"Attached modal/window '{match.Title}' for step {step.Sequence}."
            : $"Failed to auto-attach modal/window '{match.Title}' for step {step.Sequence}.";
        _logger.Log(message);
        return attached;
    }

    public bool WaitForRecordedStepWindow(JavaRecordedStep step, int timeoutMs, int pollIntervalMs, out string message)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var attempt = 0;
        while (DateTime.UtcNow <= deadline)
        {
            attempt++;
            if (TryAutoAttachJavaWindowForRecordedStep(step, out message))
            {
                _logger.Log($"WaitForRecordedStepWindow succeeded on attempt {attempt} for step {step.Sequence}.");
                return true;
            }

            if (pollIntervalMs > 0) Thread.Sleep(pollIntervalMs);
        }

        message = $"Timed out waiting for recorded modal/window '{step.WindowTitle}' for step {step.Sequence}.";
        _logger.Log(message);
        return false;
    }

    public bool DoesStepRequireWindowTransition(JavaRecordedStep currentStep, JavaRecordedStep? nextStep)
    {
        if (nextStep is null) return false;
        if (string.IsNullOrWhiteSpace(nextStep.WindowTitle)) return false;

        return !string.Equals(currentStep.WindowTitle, nextStep.WindowTitle, StringComparison.Ordinal)
               || !string.Equals(currentStep.WindowClassName, nextStep.WindowClassName, StringComparison.Ordinal)
               || currentStep.WindowProcessId != nextStep.WindowProcessId
               || currentStep.WindowVmId != nextStep.WindowVmId
               || !string.Equals(currentStep.WindowKey, nextStep.WindowKey, StringComparison.OrdinalIgnoreCase)
               || !string.Equals(currentStep.WindowHwndDisplay, nextStep.WindowHwndDisplay, StringComparison.OrdinalIgnoreCase);
    }

    private void RememberRecordingWindowScope(JavaWindowInfo? window)
    {
        if (window is null) return;
        var scope = _javaRepository.CreateWindowLocator(window, Root);
        var existing = RecordingWindowScopes.FirstOrDefault(x => string.Equals(x.WindowKey, scope.WindowKey, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            RecordingWindowScopes.Add(scope);
            return;
        }

        if (string.IsNullOrWhiteSpace(existing.HwndDisplay)) existing.HwndDisplay = scope.HwndDisplay;
        if (existing.ProcessId == 0) existing.ProcessId = scope.ProcessId;
        if (existing.VmId == 0) existing.VmId = scope.VmId;
        if (string.IsNullOrWhiteSpace(existing.RootRole)) existing.RootRole = scope.RootRole;
        if (string.IsNullOrWhiteSpace(existing.RootRoleEnUs)) existing.RootRoleEnUs = scope.RootRoleEnUs;
        if (string.IsNullOrWhiteSpace(existing.RootName)) existing.RootName = scope.RootName;
        if (string.IsNullOrWhiteSpace(existing.RootVirtualAccessibleName)) existing.RootVirtualAccessibleName = scope.RootVirtualAccessibleName;
        if (string.IsNullOrWhiteSpace(existing.RootDescription)) existing.RootDescription = scope.RootDescription;
        if (string.IsNullOrWhiteSpace(existing.RootPath)) existing.RootPath = scope.RootPath;
    }

    private JavaWindowLocator? FindRecordedWindowScope(JavaRecordedStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.WindowKey))
        {
            var byKey = RecordingWindowScopes.FirstOrDefault(x => string.Equals(x.WindowKey, step.WindowKey, StringComparison.OrdinalIgnoreCase));
            if (byKey is not null) return byKey;
        }

        return RecordingWindowScopes.FirstOrDefault(x =>
            string.Equals(x.Title, step.WindowTitle, StringComparison.Ordinal)
            && string.Equals(x.ClassName, step.WindowClassName, StringComparison.Ordinal)
            && (step.WindowProcessId == 0 || x.ProcessId == 0 || x.ProcessId == step.WindowProcessId));
    }

    private bool TryFindRepositoryWindowScope(JavaObjectRepositoryEntry entry, out JavaWindowLocator? scope)
    {
        scope = null;
        if (!string.IsNullOrWhiteSpace(entry.WindowKey))
        {
            scope = RecordingWindowScopes.FirstOrDefault(x => string.Equals(x.WindowKey, entry.WindowKey, StringComparison.OrdinalIgnoreCase));
            if (scope is not null) return true;
        }

        scope = RecordingWindowScopes.FirstOrDefault(x =>
            string.Equals(x.Title, entry.WindowTitle, StringComparison.Ordinal)
            && string.Equals(x.ClassName, entry.WindowClassName, StringComparison.Ordinal)
            && (entry.WindowProcessId == 0 || x.ProcessId == 0 || x.ProcessId == entry.WindowProcessId));
        return scope is not null;
    }

    private JavaWindowInfo? ResolveRepositoryWindowMatch(JavaObjectRepositoryEntry entry, IReadOnlyList<JavaWindowInfo> windows)
    {
        if (TryFindRepositoryWindowScope(entry, out var scope) && scope is not null)
        {
            var scoped = windows.FirstOrDefault(x => WindowMatchesScope(scope, x) && ScopeProcessMatches(scope, x));
            if (scoped is not null) return scoped;
        }

        return windows.FirstOrDefault(x => EntryMatchesWindow(entry, x))
               ?? windows.FirstOrDefault(x =>
                   x.ProcessId == entry.WindowProcessId
                   && string.Equals(x.ClassName, entry.WindowClassName, StringComparison.Ordinal)
                   && x.Title.Contains(entry.WindowTitle, StringComparison.OrdinalIgnoreCase))
               ?? windows.FirstOrDefault(x =>
                   x.ProcessId == entry.WindowProcessId
                   && string.Equals(x.Title, entry.WindowTitle, StringComparison.OrdinalIgnoreCase))
               ?? windows.FirstOrDefault(x =>
                   entry.WindowVmId != 0
                   && x.VmId == entry.WindowVmId
                   && string.Equals(x.Title, entry.WindowTitle, StringComparison.OrdinalIgnoreCase))
               ?? windows.FirstOrDefault(x =>
                   !string.IsNullOrWhiteSpace(entry.WindowHwndDisplay)
                   && string.Equals(x.HwndDisplay, entry.WindowHwndDisplay, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EntryMatchesWindow(JavaObjectRepositoryEntry entry, JavaWindowInfo window)
    {
        if (!string.IsNullOrWhiteSpace(entry.WindowHwndDisplay)
            && string.Equals(entry.WindowHwndDisplay, window.HwndDisplay, StringComparison.OrdinalIgnoreCase)) return true;
        if (entry.WindowProcessId != 0 && entry.WindowProcessId != window.ProcessId) return false;
        if (entry.WindowVmId != 0 && entry.WindowVmId != window.VmId) return false;
        if (!string.IsNullOrWhiteSpace(entry.WindowClassName)
            && !string.Equals(entry.WindowClassName, window.ClassName, StringComparison.OrdinalIgnoreCase)) return false;
        return string.IsNullOrWhiteSpace(entry.WindowTitle)
               || string.Equals(entry.WindowTitle, window.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static bool WindowMatchesScope(JavaWindowLocator? scope, JavaWindowInfo window)
    {
        if (scope is null) return false;
        if (!string.IsNullOrWhiteSpace(scope.HwndDisplay)
            && string.Equals(scope.HwndDisplay, window.HwndDisplay, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrWhiteSpace(scope.ClassName)
            && !string.Equals(scope.ClassName, window.ClassName, StringComparison.OrdinalIgnoreCase)) return false;

        return scope.TitleMatch switch
        {
            JavaWindowTitleMatch.Contains => string.IsNullOrWhiteSpace(scope.Title) || window.Title.Contains(scope.Title, StringComparison.OrdinalIgnoreCase),
            JavaWindowTitleMatch.Regex => MatchesRegex(window.Title, scope.Title),
            _ => string.IsNullOrWhiteSpace(scope.Title) || string.Equals(window.Title, scope.Title, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool ScopeProcessMatches(JavaWindowLocator? scope, JavaWindowInfo window)
    {
        if (scope is null) return false;
        if (scope.ProcessId != 0 && scope.ProcessId != window.ProcessId) return false;
        if (scope.VmId != 0 && scope.VmId != window.VmId) return false;
        return true;
    }

    private static bool MatchesRegex(string value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return true;
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(value, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private AccessibleNode? BuildStableJavaTree(JavaWindowInfo window, string reason, out int finalNodeCount)
    {
        finalNodeCount = 0;
        AccessibleNode? bestRoot = null;
        var lastCount = -1;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var crawler = new AccessibleTreeCrawler(_bridge, _logger);
            var root = crawler.BuildTree(window);
            if (root is null) continue;

            bestRoot = root;
            finalNodeCount = crawler.NodeCount;
            _logger.Debug($"Stable tree attempt {attempt} for '{window.Title}' during '{reason}' produced {crawler.NodeCount} node(s).");

            if (attempt > 1 && crawler.NodeCount == lastCount)
            {
                _logger.Debug($"Stable tree achieved on attempt {attempt} for '{window.Title}'.");
                break;
            }

            lastCount = crawler.NodeCount;
            if (attempt < 3) Thread.Sleep(140);
        }

        return bestRoot;
    }

    private sealed record AutoAttachResult(
        bool Succeeded,
        string Message,
        JavaWindowInfo? Match,
        IReadOnlyList<JavaWindowInfo> Windows,
        AccessibleNode? Root,
        int NodeCount);

    public void Dispose()
    {
        ReleaseHoverContexts();
        _bridge.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void EnqueueLogMessage(string message)
    {
        lock (_pendingLogLock)
        {
            _pendingLogs.Enqueue(message);
            if (_logDrainScheduled) return;
            _logDrainScheduled = true;
        }

        App.Current.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(DrainPendingLogs));
    }

    private void DrainPendingLogs()
    {
        List<string> batch;
        lock (_pendingLogLock)
        {
            batch = _pendingLogs.ToList();
            _pendingLogs.Clear();
            _logDrainScheduled = false;
        }

        foreach (var message in batch) Logs.Add(message);
        while (Logs.Count > 500) Logs.RemoveAt(0);
    }

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
        RefreshRecordingSurface();
    }

    private void RefreshRecordingSurface()
    {
        OnPropertyChanged(nameof(RecordingStepCount));
        OnPropertyChanged(nameof(RecordingObjectCount));
        OnPropertyChanged(nameof(RecordingBadgeText));
        OnPropertyChanged(nameof(RecordingPauseButtonText));
        OnPropertyChanged(nameof(IsRecordingPaused));
        OnPropertyChanged(nameof(CurrentRepositorySummary));
        OnPropertyChanged(nameof(RepositoryStorageDirectory));
    }

    private static string EnsureRepositoryStorageDirectory()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "JabInspector",
            "ObjectRepositories");
        Directory.CreateDirectory(path);
        return path;
    }

    private static int CountNodes(AccessibleNode? root)
    {
        if (root is null) return 0;
        var count = 0;
        var stack = new Stack<AccessibleNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            count++;
            for (var i = 0; i < node.Children.Count; i++) stack.Push(node.Children[i]);
        }
        return count;
    }

    private static string SanitizeFileName(string value)
    {
        var sanitized = string.Concat(value.Trim().Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return string.IsNullOrWhiteSpace(sanitized) ? $"JavaRecording_{DateTime.Now:yyyyMMdd_HHmmss}" : sanitized;
    }

    private void UpdateWindowsPrivilegeState()
    {
        var inspectorElevated = IsInspectorElevated;
        WindowsPrivilegeSummary = inspectorElevated
            ? "Inspector is running elevated. Admin target windows can be inspected more completely."
            : "Inspector is not elevated. Admin target windows may expose partial trees or block actions.";

        if (SelectedWindowsWindow?.Model is not { } selectedWindow)
        {
            SelectedWindowsPrivilegeWarning = "";
            OnPropertyChanged(nameof(WindowsPrivilegeWarningVisibility));
            return;
        }

        SelectedWindowsPrivilegeWarning = selectedWindow.IsElevated && !inspectorElevated
            ? $"Privilege mismatch detected: '{selectedWindow.Title}' is running as administrator, but the inspector is not. Re-run the inspector as admin to inspect and automate this window reliably."
            : "";

        OnPropertyChanged(nameof(WindowsPrivilegeWarningVisibility));
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
        OnPropertyChanged(nameof(PropertyLocatorPathValue));
        OnPropertyChanged(nameof(PropertyIndexPathValue));
        OnPropertyChanged(nameof(PropertyXPathValue));
        OnPropertyChanged(nameof(PropertyTextPreviewValue));
        OnPropertyChanged(nameof(PropertyTextDetailsValue));
        OnPropertyChanged(nameof(PropertyValueDetailsValue));
        OnPropertyChanged(nameof(WindowsEvidenceSummary));
        OnPropertyChanged(nameof(WindowsLocatorCandidates));
        OnPropertyChanged(nameof(WindowsLocatorCandidatesSummary));
        OnPropertyChanged(nameof(WindowsMetadataSummary));
        OnPropertyChanged(nameof(SelectedWindowsLocatorCandidateDetails));
        OnPropertyChanged(nameof(WindowsProjectedSourcePreview));
        OnPropertyChanged(nameof(WindowsRawSourcePreview));
        OnPropertyChanged(nameof(WindowsPointProbePreview));
    }

    private string BuildWindowsLocatorPreview(WindowsAutomationNode node, DesktopElement? projectedElement)
    {
        var pointEvidence = projectedElement?.SourceType == DesktopElementSource.Win32 && !node.Bounds.IsEmpty
            ? _win32Scanner.InspectFromPoint(node.Bounds.X + Math.Max(node.Bounds.Width / 2, 1), node.Bounds.Y + Math.Max(node.Bounds.Height / 2, 1))
            : null;
        var payload = new
        {
            provider = "windows-desktop",
            backend = node.BackendKind.ToString(),
            projected = projectedElement is null ? null : new
            {
                id = projectedElement.Id,
                name = projectedElement.Name,
                role = projectedElement.Role,
                className = projectedElement.ClassName,
                controlId = projectedElement.ControlId,
                sourceType = projectedElement.SourceType.ToString(),
                elementKind = projectedElement.ElementKind.ToString(),
                bounds = projectedElement.Bounds,
                locators = projectedElement.Locators.Select(locator => new
                {
                    type = locator.Type.ToString(),
                    value = locator.Value,
                    score = locator.Score,
                    confidence = locator.Confidence,
                    priority = locator.Priority
                }),
                supportedActions = projectedElement.SupportedActions.Select(action => action.ToString()),
                metadata = projectedElement.Metadata
            },
            rawNode = new
            {
                path = BuildWindowsPath(node),
                role = node.Role,
                name = node.Name,
                className = node.ClassName,
                automationId = node.AutomationId,
                nativeHandle = $"0x{node.NativeHandle.ToInt64():X}",
                controlId = node.ControlId >= 0 ? (int?)node.ControlId : null,
                processId = node.ProcessId,
                threadId = node.ThreadId,
                isVisible = node.IsVisible,
                isEnabled = node.IsEnabled,
                style = $"0x{node.Style:X}",
                extendedStyle = $"0x{node.ExtendedStyle:X}",
                clientBounds = new { x = node.ClientBounds.X, y = node.ClientBounds.Y, width = node.ClientBounds.Width, height = node.ClientBounds.Height },
                metadata = node.Metadata
            },
            win32PointEvidence = pointEvidence is null ? null : new
            {
                hwnd = pointEvidence.Hwnd == IntPtr.Zero ? "" : $"0x{pointEvidence.Hwnd.ToInt64():X}",
                className = pointEvidence.ClassName,
                windowText = pointEvidence.WindowText,
                controlId = pointEvidence.ControlId,
                childCount = pointEvidence.ChildCount,
                isVisible = pointEvidence.IsVisible,
                isEnabled = pointEvidence.IsEnabled,
                bounds = pointEvidence.Bounds,
                metadata = pointEvidence.Metadata
            }
        };
        return JsonSerializer.Serialize(payload, JsonExportService.Options);
    }

    private void UpdateWindowsSourcePreviews(WindowsAutomationNode? node, DesktopElement? projectedElement)
    {
        if (node is null)
        {
            _windowsProjectedSourcePreview = "Select a Windows automation node to inspect projected source data.";
            _windowsRawSourcePreview = "Select a Windows automation node to inspect raw backend data.";
            _windowsPointProbePreview = "Select a Windows automation node to inspect point-probe evidence.";
            return;
        }

        _windowsProjectedSourcePreview = JsonSerializer.Serialize(new
        {
            provider = "windows-desktop",
            id = projectedElement?.Id ?? "",
            name = projectedElement?.Name ?? node.Name,
            role = projectedElement?.Role ?? node.Role,
            className = projectedElement?.ClassName ?? node.ClassName,
            sourceType = projectedElement?.SourceType.ToString() ?? "unknown",
            elementKind = projectedElement?.ElementKind.ToString() ?? "unknown",
            confidence = projectedElement?.Confidence ?? 0,
            bounds = projectedElement?.Bounds,
            supportedActions = projectedElement?.SupportedActions.Select(action => action.ToString()),
            metadata = projectedElement?.Metadata,
            childIds = projectedElement?.ChildIds,
            locatorTypes = projectedElement?.Locators.Select(locator => new
            {
                type = locator.Type.ToString(),
                priority = locator.Priority,
                score = locator.Score,
                confidence = locator.Confidence
            })
        }, JsonExportService.Options);

        _windowsRawSourcePreview = JsonSerializer.Serialize(new
        {
            backend = node.BackendKind.ToString(),
            path = BuildWindowsPath(node),
            indexPath = BuildWindowsIndexPath(node),
            classPath = BuildWindowsClassPath(node),
            name = node.Name,
            role = node.Role,
            className = node.ClassName,
            automationId = node.AutomationId,
            value = node.Value,
            nativeHandle = node.NativeHandle == IntPtr.Zero ? "" : $"0x{node.NativeHandle.ToInt64():X}",
            controlId = node.ControlId >= 0 ? (int?)node.ControlId : null,
            processId = node.ProcessId,
            threadId = node.ThreadId,
            isVisible = node.IsVisible,
            isEnabled = node.IsEnabled,
            style = $"0x{node.Style:X}",
            extendedStyle = $"0x{node.ExtendedStyle:X}",
            bounds = new { x = node.Bounds.X, y = node.Bounds.Y, width = node.Bounds.Width, height = node.Bounds.Height },
            clientBounds = new { x = node.ClientBounds.X, y = node.ClientBounds.Y, width = node.ClientBounds.Width, height = node.ClientBounds.Height },
            metadata = node.Metadata
        }, JsonExportService.Options);

        if (!node.Bounds.IsEmpty)
        {
            var probeX = node.Bounds.X + Math.Max(node.Bounds.Width / 2, 1);
            var probeY = node.Bounds.Y + Math.Max(node.Bounds.Height / 2, 1);

            if (projectedElement?.SourceType == DesktopElementSource.Win32)
            {
                var pointEvidence = _win32Scanner.InspectFromPoint(probeX, probeY);
                _windowsPointProbePreview = JsonSerializer.Serialize(new
                {
                    source = "Win32",
                    hwnd = pointEvidence.Hwnd == IntPtr.Zero ? "" : $"0x{pointEvidence.Hwnd.ToInt64():X}",
                    className = pointEvidence.ClassName,
                    windowText = pointEvidence.WindowText,
                    controlId = pointEvidence.ControlId,
                    childCount = pointEvidence.ChildCount,
                    isVisible = pointEvidence.IsVisible,
                    isEnabled = pointEvidence.IsEnabled,
                    bounds = pointEvidence.Bounds,
                    metadata = pointEvidence.Metadata
                }, JsonExportService.Options);
                return;
            }

            if (projectedElement?.SourceType == DesktopElementSource.Msaa || node.BackendKind == WindowsAutomationBackendKind.Msaa)
            {
                var pointEvidence = _msaaScanner.InspectFromPoint(probeX, probeY);
                _windowsPointProbePreview = pointEvidence is null
                    ? "MSAA point probe did not return an accessible object for the sampled point."
                    : JsonSerializer.Serialize(new
                    {
                        source = "Msaa",
                        name = pointEvidence.Name,
                        role = pointEvidence.Role,
                        value = pointEvidence.Value,
                        description = pointEvidence.Description,
                        defaultAction = pointEvidence.DefaultAction,
                        childCount = pointEvidence.ChildCount,
                        childId = pointEvidence.ChildId,
                        bounds = pointEvidence.Bounds,
                        elementRef = pointEvidence.ElementRef,
                        metadata = pointEvidence.Metadata
                    }, JsonExportService.Options);
                return;
            }
        }

        _windowsPointProbePreview = "Point-probe evidence is available for Win32 and MSAA nodes when valid bounds are available.";
    }

    private static string BuildWindowsPath(WindowsAutomationNode? node)
    {
        if (node is null)
        {
            return "";
        }

        var segments = new Stack<string>();
        for (var cursor = node; cursor is not null; cursor = cursor.Parent)
        {
            var role = string.IsNullOrWhiteSpace(cursor.Role) ? "node" : cursor.Role;
            segments.Push($"{role}[{Math.Max(cursor.IndexInParent, 0)}]");
        }

        return string.Join("/", segments);
    }

    private static string BuildWindowsDescription(WindowsAutomationNode? node)
    {
        if (node is null)
        {
            return "";
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(node.ClassName))
        {
            parts.Add(node.ClassName);
        }

        if (node.ProcessId > 0)
        {
            parts.Add($"PID {node.ProcessId}");
        }

        if (node.ThreadId > 0)
        {
            parts.Add($"TID {node.ThreadId}");
        }

        return parts.Count == 0
            ? "(no backend description exposed)"
            : string.Join(" · ", parts);
    }

    private static string BuildWindowsRoleSecondaryValue(WindowsAutomationNode? node, DesktopElement? projected)
    {
        if (node is null)
        {
            return "";
        }

        var parts = new List<string> { node.BackendKind.ToString() };

        if (projected is not null)
        {
            parts.Add(projected.SourceType.ToString());
        }

        if (!string.IsNullOrWhiteSpace(node.ClassName))
        {
            parts.Add(node.ClassName);
        }

        return string.Join(" · ", parts);
    }

    private static string BuildWindowsStatesValue(WindowsAutomationNode? node)
    {
        if (node is null)
        {
            return "";
        }

        var parts = new List<string>
        {
            node.IsVisible ? "visible" : "hidden",
            node.IsEnabled ? "enabled" : "disabled"
        };

        if (!string.IsNullOrWhiteSpace(node.AutomationId))
        {
            parts.Add($"automationId={node.AutomationId}");
        }

        return string.Join(" · ", parts);
    }

    private static string BuildWindowsStatesSecondaryValue(WindowsAutomationNode? node)
    {
        if (node is null)
        {
            return "";
        }

        var parts = new List<string>();

        if (node.ControlId > 0)
        {
            parts.Add($"ctrlId={node.ControlId}");
        }

        if (node.Style != 0)
        {
            parts.Add($"style=0x{node.Style:X}");
        }

        if (node.ExtendedStyle != 0)
        {
            parts.Add($"exStyle=0x{node.ExtendedStyle:X}");
        }

        if (node.Metadata.TryGetValue("msaa.isChildIdOnly", out var isChildIdOnly) && bool.TryParse(isChildIdOnly, out var childIdOnly) && childIdOnly)
        {
            parts.Add("msaaChildIdOnly=true");
        }

        return parts.Count == 0
            ? "(no additional native state)"
            : string.Join(" · ", parts);
    }

    private static string BuildWindowsIndexPath(WindowsAutomationNode? node)
    {
        if (node is null)
        {
            return "";
        }

        var segments = new Stack<string>();
        for (var cursor = node; cursor is not null; cursor = cursor.Parent)
        {
            segments.Push(Math.Max(cursor.IndexInParent, 0).ToString());
        }

        return string.Join("/", segments);
    }

    private static string BuildWindowsClassPath(WindowsAutomationNode? node)
    {
        if (node is null)
        {
            return "";
        }

        var segments = new Stack<string>();
        for (var cursor = node; cursor is not null; cursor = cursor.Parent)
        {
            var className = string.IsNullOrWhiteSpace(cursor.ClassName) ? "node" : cursor.ClassName;
            segments.Push($"/{className}[{Math.Max(cursor.IndexInParent, 0) + 1}]");
        }

        return string.Concat(segments);
    }

    private static string BuildWindowsTextPreview(WindowsAutomationNode? node)
    {
        if (node is null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(node.Value))
        {
            return node.Value;
        }

        if (node.Metadata.TryGetValue("msaa.value", out var msaaValue) && !string.IsNullOrWhiteSpace(msaaValue))
        {
            return msaaValue;
        }

        if (node.Metadata.TryGetValue("uia.helpText", out var helpText) && !string.IsNullOrWhiteSpace(helpText))
        {
            return helpText;
        }

        return "(no text/value exposed by current Windows backend)";
    }

    private static string BuildWindowsTextDetails(WindowsAutomationNode? node)
    {
        if (node is null)
        {
            return "";
        }

        var parts = new List<string>();

        if (node.Metadata.TryGetValue("uia.acceleratorKey", out var accelerator) && !string.IsNullOrWhiteSpace(accelerator))
        {
            parts.Add($"accelerator={accelerator}");
        }

        if (node.Metadata.TryGetValue("uia.accessKey", out var accessKey) && !string.IsNullOrWhiteSpace(accessKey))
        {
            parts.Add($"accessKey={accessKey}");
        }

        if (node.Metadata.TryGetValue("msaa.defaultAction", out var defaultAction) && !string.IsNullOrWhiteSpace(defaultAction))
        {
            parts.Add($"defaultAction={defaultAction}");
        }

        if (node.Metadata.TryGetValue("msaa.keyboardShortcut", out var shortcut) && !string.IsNullOrWhiteSpace(shortcut))
        {
            parts.Add($"shortcut={shortcut}");
        }

        return parts.Count == 0
            ? "(no additional text metadata)"
            : string.Join(" · ", parts);
    }

    private static string BuildWindowsValueDetails(DesktopElement? projected)
    {
        if (projected is null)
        {
            return "";
        }

        var parts = new List<string>();

        if (projected.SupportedActions.Count > 0)
        {
            parts.Add($"actions={string.Join(",", projected.SupportedActions)}");
        }

        if (projected.Locators.Count > 0)
        {
            parts.Add($"locators={projected.Locators.Count}");
        }

        if (projected.Metadata.Count > 0)
        {
            parts.Add($"metadata={projected.Metadata.Count}");
        }

        return parts.Count == 0
            ? "(no projected action/value metadata)"
            : string.Join(" · ", parts);
    }

    private static string FormatJavaBounds(AccessibleNode? node) =>
        node is null ? "" : $"{node.X}, {node.Y}  ·  {node.Width} x {node.Height}";

    private static string FormatWindowsBounds(WindowsAutomationNode? node) =>
        node is null ? "" : $"{node.Bounds.X}, {node.Bounds.Y}  ·  {node.Bounds.Width} x {node.Bounds.Height}";

    private static string FormatJavaIds(AccessibleNode? node) =>
        node is null ? "" : $"VM {node.VmId} · CTX {node.Context}";

    private static string FormatWindowsIds(WindowsAutomationNode? node) =>
        node is null ? "" : $"HWND 0x{node.NativeHandle.ToInt64():X}";

    private static string FormatTextPreview(AccessibleNode? node)
    {
        if (node is null) return "";
        if (!string.IsNullOrWhiteSpace(node.TextPreview))
            return $"{node.TextPreview}  [{node.TextPreviewSource}]";
        return node.AccessibleText || node.AccessibleValue || node.AccessibleSelection || node.ChildrenCount > 0
            ? "(no text/value exposed by JAB for this node)"
            : "(node does not expose AccessibleText/AccessibleValue)";
    }

    private static string FormatTextDetails(AccessibleNode? node)
    {
        if (node is null) return "";
        var parts = new List<string>();
        if (node.TextCharCount >= 0) parts.Add($"chars={node.TextCharCount}");
        if (node.TextCaretIndex >= 0) parts.Add($"caret={node.TextCaretIndex}");
        if (node.TextIndexAtPoint >= 0) parts.Add($"indexAtPoint={node.TextIndexAtPoint}");
        if (!string.IsNullOrWhiteSpace(node.TextSelected)) parts.Add($"selected=\"{node.TextSelected}\"");
        if (!string.IsNullOrWhiteSpace(node.TextWord)) parts.Add($"word=\"{node.TextWord}\"");
        if (!string.IsNullOrWhiteSpace(node.TextSentence)) parts.Add($"sentence=\"{node.TextSentence}\"");
        return parts.Count == 0 ? "(no AccessibleText details)" : string.Join(" · ", parts);
    }

    private static string FormatValueDetails(AccessibleNode? node)
    {
        if (node is null) return "";
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(node.CurrentValue)) parts.Add($"current={node.CurrentValue}");
        if (!string.IsNullOrWhiteSpace(node.MinimumValue)) parts.Add($"min={node.MinimumValue}");
        if (!string.IsNullOrWhiteSpace(node.MaximumValue)) parts.Add($"max={node.MaximumValue}");
        return parts.Count == 0 ? "(no AccessibleValue details)" : string.Join(" · ", parts);
    }

    private static string FormatActions(IReadOnlyList<string> actions) =>
        actions.Count == 0 ? "No semantic actions exposed" : string.Join("  ·  ", actions);

    private static string BuildWindowsBoundsValue(WindowsAutomationNode? node)
    {
        if (node is null)
        {
            return "";
        }

        return node.ClientBounds.Width > 0 && node.ClientBounds.Height > 0
            ? $"{node.Bounds.X}, {node.Bounds.Y}  ·  {node.Bounds.Width} x {node.Bounds.Height}  |  client {node.ClientBounds.X}, {node.ClientBounds.Y}  ·  {node.ClientBounds.Width} x {node.ClientBounds.Height}"
            : $"{node.Bounds.X}, {node.Bounds.Y}  ·  {node.Bounds.Width} x {node.Bounds.Height}";
    }

    private static string BuildWindowsRawIdsValue(WindowsAutomationNode? node)
    {
        if (node is null)
        {
            return "";
        }

        var parts = new List<string> { $"HWND 0x{node.NativeHandle.ToInt64():X}" };

        if (node.ControlId > 0)
        {
            parts.Add($"CTRL {node.ControlId}");
        }

        if (node.ProcessId > 0)
        {
            parts.Add($"PID {node.ProcessId}");
        }

        if (node.ThreadId > 0)
        {
            parts.Add($"TID {node.ThreadId}");
        }

        return string.Join(" · ", parts);
    }

    private static string BuildWindowsEvidenceSummary(WindowsAutomationNode? node, DesktopElement? projected)
    {
        if (node is null)
        {
            return "";
        }

        var parts = new List<string> { $"backend={node.BackendKind}", $"source={projected?.SourceType.ToString() ?? "unknown"}" };

        if (projected is not null)
        {
            parts.Add($"kind={projected.ElementKind}");
            parts.Add($"confidence={projected.Confidence:0.00}");
        }

        return string.Join(" · ", parts);
    }

    private static string BuildWindowsLocatorCandidatesSummary(DesktopElement? projected)
    {
        if (projected is null || projected.Locators.Count == 0)
        {
            return "(no locator candidates projected yet)";
        }

        return string.Join(Environment.NewLine, projected.Locators
            .OrderBy(locator => locator.Priority)
            .Take(4)
            .Select(locator => $"P{locator.Priority} [{locator.Type}] score={locator.Score}  {locator.Value}"));
    }

    private static string BuildWindowsMetadataSummary(DesktopElement? projected)
    {
        if (projected is null || projected.Metadata.Count == 0)
        {
            return "(no projected metadata)";
        }

        return string.Join(Environment.NewLine, projected.Metadata
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .OrderBy(pair => pair.Key)
            .Take(8)
            .Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string BuildWindowsLocatorCandidateDetails(LocatorCandidate? candidate)
    {
        if (candidate is null)
        {
            return "Select a locator candidate to inspect its payload and supporting properties.";
        }

        var lines = new List<string>
        {
            $"type={candidate.Type}",
            $"priority={candidate.Priority}",
            $"score={candidate.Score}",
            $"confidence={candidate.Confidence:0.00}",
            $"value={candidate.Value}"
        };

        if (candidate.Region is not null)
        {
            var region = candidate.Region.Value;
            lines.Add($"region={region.X},{region.Y},{region.Width},{region.Height}");
        }

        foreach (var property in candidate.Properties.OrderBy(pair => pair.Key))
        {
            if (!string.IsNullOrWhiteSpace(property.Value))
            {
                lines.Add($"{property.Key}={property.Value}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

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
            if (child is null && !HasElementIdentity(step))
                child = resolvedBase.Children.FirstOrDefault(x => string.Equals(x.RoleEnUs, step.RoleEnUs, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Role, step.Role, StringComparison.OrdinalIgnoreCase));
            if (child is null)
            {
                step.Parent = resolvedBase;
                step.HasManagedDescendantAncestor = resolvedBase.HasManagedDescendantAncestor || resolvedBase.ManagesDescendants;
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
        if (!string.IsNullOrWhiteSpace(left.Name) && !string.IsNullOrWhiteSpace(right.Name))
            return string.Equals(left.Name, right.Name, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(left.VirtualAccessibleName) && !string.IsNullOrWhiteSpace(right.VirtualAccessibleName))
            return string.Equals(left.VirtualAccessibleName, right.VirtualAccessibleName, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(left.Description) && !string.IsNullOrWhiteSpace(right.Description))
            return string.Equals(left.Description, right.Description, StringComparison.Ordinal);
        return true;
    }

    private static bool HasElementIdentity(AccessibleNode node) =>
        !string.IsNullOrWhiteSpace(node.Name) ||
        !string.IsNullOrWhiteSpace(node.VirtualAccessibleName) ||
        !string.IsNullOrWhiteSpace(node.Description);

    private AccessibleNode CreateNode(int vmId, long context, JabInspector.Native.AccessibleContextInfo info)
    {
        var node = new AccessibleNode { VmId = vmId, Context = context };
        ApplyInfo(node, info);
        return node;
    }

    private void ApplyInfo(AccessibleNode node, JabInspector.Native.AccessibleContextInfo x)
    {
        node.Name = x.Name ?? "";
        node.VirtualAccessibleName = _bridge.GetVirtualAccessibleName(node.VmId, node.Context);
        node.Description = x.Description ?? "";
        node.Role = string.IsNullOrWhiteSpace(x.Role) ? "unknown" : x.Role;
        node.RoleEnUs = x.RoleEnUs ?? "";
        node.States = x.States ?? "";
        node.StatesEnUs = x.StatesEnUs ?? "";
        node.IndexInParent = x.IndexInParent;
        node.ObjectDepth = _bridge.GetObjectDepth(node.VmId, node.Context);
        node.ChildrenCount = x.ChildrenCount;
        node.X = x.X;
        node.Y = x.Y;
        node.Width = x.Width;
        node.Height = x.Height;
        node.AccessibleComponent = x.AccessibleComponent;
        node.AccessibleAction = x.AccessibleAction;
        node.AccessibleSelection = x.AccessibleSelection;
        node.AccessibleText = x.AccessibleText;
        node.AccessibleValue = x.AccessibleValue;
        node.AccessibleTable = x.AccessibleTable;
        node.AccessibleInterfaces = x.AccessibleInterfaces;
        node.HasManagedDescendantAncestor = node.Parent?.HasManagedDescendantAncestor == true || node.Parent?.ManagesDescendants == true;
        node.ActionNames = node.AccessibleAction ? _bridge.GetAccessibleActions(node.VmId, node.Context).ToList() : [];
        _bridge.EnrichTextAndValue(node, node.X, node.Y);
        _bridge.EnrichContainerTextFromChildren(node);
    }
}

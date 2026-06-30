using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JabInspector.App.ViewModels;
using JabInspector.Core.Models;
using JabInspector.Core.Services;
using Microsoft.Win32;
using System.Windows.Interop;
using WinInspector.Core.Models;

namespace JabInspector.App;

public partial class MainWindow : Window, IJavaActionExecutionHost
{
    private readonly MainViewModel _viewModel = new();
    private readonly JavaActionExecutionService _javaActions = new();
    private readonly System.Windows.Threading.DispatcherTimer _hoverTimer;
    private readonly System.Windows.Threading.DispatcherTimer _recordingMonitorTimer;
    private RecordingStudioWindow? _recordingStudioWindow;
    private ObjectRepositoryWindow? _objectRepositoryWindow;
    private RecordingBadgeOverlay? _recordingBadgeOverlay;
    private bool _hoverInspecting;
    private bool _pickerActive;
    private readonly System.Windows.Threading.DispatcherTimer _pickerTimer;
    private bool _recordingLeftButtonDown;
    private bool _recordingCaptureInProgress;
    private NativePoint _recordingMouseDownPoint;
    private AccessibleNode? _recordingMouseDownNode;
    private ElementBounds _recordingMouseDownBounds = new(0, 0, 0, 0);
    private JavaWindowInfo? _recordingMouseDownWindow;
    private bool _recordingMouseDownOnNativeClose;
    private DateTime _lastPassiveClickAtUtc;
    private string _lastPassiveClickPath = "";
    private bool _playbackInProgress;
    private IntPtr _lastAutoAttachProbeHwnd;
    private DateTime _lastAutoAttachProbeAtUtc;
    private IntPtr _recordingBadgeTargetHwnd;
    private NativePoint? _lastHoverPoint;
    private AccessibleNode? _lastHierarchyNode;
    private AccessibleNode? _hoverSelectingNode;
    private AccessibleNode? _lastActivatedNode;
    private DateTime _lastActivationAt;
    private bool _startupPositioned;
    private bool _logScrollScheduled;
    public MainWindow()
    {
        InitializeComponent(); DataContext = _viewModel;
        _hoverTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        _hoverTimer.Tick += HoverTimer_Tick;
        _pickerTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _pickerTimer.Tick += PickerTimer_Tick;
        _recordingMonitorTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(55) };
        _recordingMonitorTimer.Tick += RecordingMonitorTimer_Tick;
        _recordingMonitorTimer.Start();
        _viewModel.Logs.CollectionChanged += Logs_CollectionChanged;
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += (_, _) =>
        {
            _hoverTimer.Stop();
            _pickerTimer.Stop();
            _recordingMonitorTimer.Stop();
            HighlightOverlay.HidePersistent();
            _recordingBadgeOverlay?.Detach();
            _recordingStudioWindow?.Close();
            _objectRepositoryWindow?.Close();
            _viewModel.Dispose();
        };
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        PositionStartupWindow();
    }

    private void PositionStartupWindow()
    {
        if (_startupPositioned) return;
        _startupPositioned = true;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        if (!GetCursorPos(out var cursor)) return;
        var monitor = MonitorFromPoint(cursor, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return;

        var info = MonitorInfo.Create();
        if (!GetMonitorInfo(monitor, ref info)) return;

        if (!GetWindowRect(hwnd, out var rect)) return;

        var workWidth = Math.Max(400, info.Work.Right - info.Work.Left);
        var workHeight = Math.Max(300, info.Work.Bottom - info.Work.Top);
        var desiredWidth = (int)Math.Round(workWidth * 0.64);
        var desiredHeight = (int)Math.Round(workHeight * 0.82);
        Width = Math.Clamp(desiredWidth, MinWidth, Math.Min(1180, workWidth - 24));
        Height = Math.Clamp(desiredHeight, MinHeight, Math.Min(820, workHeight - 24));
        UpdateLayout();

        if (!GetWindowRect(hwnd, out rect)) return;

        var width = Math.Min(rect.Right - rect.Left, workWidth - 24);
        var height = Math.Min(rect.Bottom - rect.Top, workHeight - 24);
        var left = info.Work.Left + Math.Max(12, (workWidth - width) / 2);
        var top = info.Work.Top + Math.Max(12, (workHeight - height) / 2);

        SetWindowPos(hwnd, IntPtr.Zero, left, top, width, height, SwpNoZOrder | SwpNoActivate);
    }

    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        switch (e.NewValue)
        {
            case AccessibleNode node:
                _viewModel.SelectedNode = node;
                if (ReferenceEquals(node, _hoverSelectingNode)) { _hoverSelectingNode = null; return; }
                ActivateHierarchyNode(node);
                break;
            case WindowsAutomationNode windowsNode:
                _viewModel.SelectedWindowsNode = windowsNode;
                ActivateHierarchyNode(windowsNode);
                break;
        }
    }

    private void Highlight_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsWindowsMode)
        {
            var node = _viewModel.SelectedWindowsNode;
            if (node is null) { _viewModel.Log("Select an element before highlighting."); return; }
            if (!TryGetHighlightBounds(node, out var visualNode, out var physicalBounds))
            { _viewModel.Log("The selected Windows element and its ancestors have no on-screen bounds and cannot be highlighted."); return; }
            HighlightOverlay.Show(physicalBounds);
            var fallback = ReferenceEquals(node, visualNode) ? "" : $" using nearest on-screen ancestor {visualNode.DisplayName}";
            _viewModel.Log($"Highlighted {node.DisplayName}{fallback} at physical bounds ({physicalBounds.X}, {physicalBounds.Y}, {physicalBounds.Width}, {physicalBounds.Height}).");
            return;
        }

        var javaNode = _viewModel.SelectedNode;
        if (javaNode is null) { _viewModel.Log("Select an element before highlighting."); return; }
        if (!TryGetHighlightBounds(javaNode, out var visualNodeJava, out var physicalBoundsJava))
        { _viewModel.Log("The selected element and its ancestors have no on-screen bounds and cannot be highlighted."); return; }
        HighlightOverlay.Show(physicalBoundsJava);
        var fallbackJava = ReferenceEquals(javaNode, visualNodeJava) ? "" : $" using nearest on-screen ancestor {visualNodeJava.DisplayName}";
        _viewModel.Log($"Highlighted {javaNode.DisplayName}{fallbackJava} at physical bounds ({physicalBoundsJava.X}, {physicalBoundsJava.Y}, {physicalBoundsJava.Width}, {physicalBoundsJava.Height}); JAB bounds were ({visualNodeJava.X}, {visualNodeJava.Y}, {visualNodeJava.Width}, {visualNodeJava.Height}).");
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshRequirements();
        DetailsTabs.SelectedItem = SettingsTab;
    }

    private void AccessibilityTree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        var container = FindTreeViewItem(source);
        switch (container?.DataContext)
        {
            case AccessibleNode node:
                _viewModel.SelectedNode = node;
                ActivateHierarchyNode(node);
                break;
            case WindowsAutomationNode windowsNode:
                _viewModel.SelectedWindowsNode = windowsNode;
                ActivateHierarchyNode(windowsNode);
                break;
        }
    }

    private void ActivateHierarchyNode(AccessibleNode node)
    {
        if (ReferenceEquals(node, _lastActivatedNode) && DateTime.UtcNow - _lastActivationAt < TimeSpan.FromMilliseconds(350)) return;
        _lastActivatedNode = node; _lastActivationAt = DateTime.UtcNow;
        if (TryGetHighlightBounds(node, out var visualNode, out var bounds))
        {
            HighlightOverlay.Show(bounds);
            var fallback = ReferenceEquals(node, visualNode) ? "" : $" using nearest on-screen ancestor {visualNode.DisplayName}";
            _viewModel.Log($"Hierarchy click selected and highlighted {node.DisplayName}{fallback}. Target application was not focused.");
        }
        else _viewModel.Log($"Hierarchy click selected {node.DisplayName}, but no on-screen bounds were available.");
    }

    private void ActivateHierarchyNode(WindowsAutomationNode node)
    {
        if (TryGetHighlightBounds(node, out var visualNode, out var bounds))
        {
            HighlightOverlay.Show(bounds);
            var fallback = ReferenceEquals(node, visualNode) ? "" : $" using nearest on-screen ancestor {visualNode.DisplayName}";
            _viewModel.Log($"Hierarchy click selected and highlighted {node.DisplayName}{fallback} through {node.BackendKind}. Target application was not focused.");
        }
        else _viewModel.Log($"Hierarchy click selected {node.DisplayName}, but no on-screen bounds were available.");
    }

    private static TreeViewItem? FindTreeViewItem(DependencyObject source)
    {
        for (DependencyObject? current = source; current is not null;)
        {
            if (current is TreeViewItem item) return item;
            current = current switch
            {
                FrameworkContentElement content => content.Parent,
                System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D => System.Windows.Media.VisualTreeHelper.GetParent(current),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }
        return null;
    }

    private bool TryGetHighlightBounds(AccessibleNode node, out AccessibleNode visualNode, out ElementBounds bounds)
    {
        visualNode = node;
        while (true)
        {
            _viewModel.RefreshBounds(visualNode);
            bounds = GetPhysicalBounds(visualNode);
            if (HasOnScreenBounds(bounds) || visualNode.Parent is null) break;
            visualNode = visualNode.Parent;
        }
        return HasOnScreenBounds(bounds);
    }

    private bool TryGetHighlightBounds(WindowsAutomationNode node, out WindowsAutomationNode visualNode, out ElementBounds bounds)
    {
        visualNode = node;
        while (true)
        {
            bounds = new ElementBounds(visualNode.Bounds.X, visualNode.Bounds.Y, visualNode.Bounds.Width, visualNode.Bounds.Height);
            if (HasOnScreenBounds(bounds) || visualNode.Parent is null) break;
            visualNode = visualNode.Parent;
        }
        return HasOnScreenBounds(bounds);
    }

    private void HoverInspect_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsWindowsMode)
        { _viewModel.Log("Hover inspection is currently implemented for Java mode only."); return; }
        if (!_hoverInspecting && (_viewModel.Root is null || _viewModel.CurrentWindow is null))
        { _viewModel.Log("Attach to a Java window before enabling hover inspection."); return; }
        _hoverInspecting = !_hoverInspecting;
        _lastHoverPoint = null;
        _lastHierarchyNode = null;
        _hoverSelectingNode = null;
        if (_hoverInspecting)
        {
            DetailsTabs.SelectedItem = PropertiesTab; _hoverTimer.Start();
            _viewModel.Log("Hover inspection enabled. Move the pointer over the attached Java window.");
        }
        else
        {
            _hoverTimer.Stop(); HighlightOverlay.HidePersistent();
            _viewModel.Log("Hover inspection disabled.");
        }
    }

    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        if (!_hoverInspecting) return;
        if (!GetCursorPos(out var point)) return;
        TryAutoAttachJavaWindowFromPoint(point, "hover inspect");
        if (_viewModel.Root is null || _viewModel.CurrentWindow is null) return;
        if (_lastHoverPoint is { } previous && Math.Abs(previous.X - point.X) < 2 && Math.Abs(previous.Y - point.Y) < 2) return;
        _lastHoverPoint = point;
        if (!GetWindowRect(_viewModel.CurrentWindow.Hwnd, out var native) || point.X < native.Left || point.X >= native.Right || point.Y < native.Top || point.Y >= native.Bottom)
        { HighlightOverlay.HidePersistent(); return; }

        _viewModel.RefreshBounds(_viewModel.Root);
        var root = _viewModel.Root;
        if (!root.HasValidBounds) return;
        var scaleX = (double)(native.Right - native.Left) / root.Width;
        var scaleY = (double)(native.Bottom - native.Top) / root.Height;
        var jabX = root.X + (int)Math.Round((point.X - native.Left) / scaleX);
        var jabY = root.Y + (int)Math.Round((point.Y - native.Top) / scaleY);
        // JAB distributions differ on whether hit-testing expects physical
        // screen pixels or Java's logical coordinates. Probe physical first,
        // then use the anchored inverse transform when the result misses.
        var node = _viewModel.InspectAt(point.X, point.Y);
        var bounds = node is null ? new ElementBounds(0, 0, 0, 0) : GetPhysicalBounds(node);
        if (node is null || !Contains(bounds, point))
        {
            var logicalNode = _viewModel.InspectAt(jabX, jabY);
            if (logicalNode is not null) { node = logicalNode; bounds = GetPhysicalBounds(logicalNode); }
        }
        if (node is null) { HighlightOverlay.HidePersistent(); return; }
        _viewModel.SelectedNode = node;
        if (!ReferenceEquals(_lastHierarchyNode, node))
        {
            _hoverSelectingNode = node;
            SelectNodeInHierarchy(node);
            _lastHierarchyNode = node;
        }
        DetailsTabs.SelectedItem = PropertiesTab;
        var visualNode = node;
        while (!HasOnScreenBounds(bounds) && visualNode.Parent is not null)
        { visualNode = visualNode.Parent; _viewModel.RefreshBounds(visualNode); bounds = GetPhysicalBounds(visualNode); }
        if (HasOnScreenBounds(bounds)) HighlightOverlay.ShowPersistent(bounds); else HighlightOverlay.HidePersistent();
    }

    private void PickerButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsWindowsMode)
        {
            _viewModel.Log("Picker is currently implemented for Java mode only.");
            return;
        }

        _pickerActive = true;
        _pickerTimer.Start();
        Mouse.Capture(PickerButton);
        _viewModel.Log("Picker active: drag over target application to inspect and select an element.");
        e.Handled = true;
    }

    private void PickerButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pickerActive) return;

        _pickerActive = false;
        _pickerTimer.Stop();
        Mouse.Capture(null);

        if (_viewModel.SelectedNode is not null)
        {
            _viewModel.Log("Picker complete: element selected in hierarchy.");
        }
        else
        {
            _viewModel.Log("Picker complete: no element selected.");
        }
        e.Handled = true;
    }

    private void PickerTimer_Tick(object? sender, EventArgs e)
    {
        if (!_pickerActive) return;
        if (!GetCursorPos(out var point)) return;

        TryAutoAttachJavaWindowFromPoint(point, "picker");

        if (_viewModel.Root is null || _viewModel.CurrentWindow is null)
        {
            HighlightOverlay.HidePersistent();
            return;
        }

        if (!GetWindowRect(_viewModel.CurrentWindow.Hwnd, out var native) ||
            point.X < native.Left || point.X >= native.Right ||
            point.Y < native.Top || point.Y >= native.Bottom)
        {
            HighlightOverlay.HidePersistent();
            return;
        }

        _viewModel.RefreshBounds(_viewModel.Root);
        var root = _viewModel.Root;
        if (!root.HasValidBounds) return;

        var scaleX = (double)(native.Right - native.Left) / root.Width;
        var scaleY = (double)(native.Bottom - native.Top) / root.Height;
        var jabX = root.X + (int)Math.Round((point.X - native.Left) / scaleX);
        var jabY = root.Y + (int)Math.Round((point.Y - native.Top) / scaleY);

        var node = _viewModel.InspectAt(point.X, point.Y);
        var bounds = node is null ? new ElementBounds(0, 0, 0, 0) : GetPhysicalBounds(node);

        if (node is null || !Contains(bounds, point))
        {
            var logicalNode = _viewModel.InspectAt(jabX, jabY);
            if (logicalNode is not null)
            {
                node = logicalNode;
                bounds = GetPhysicalBounds(logicalNode);
            }
        }

        if (node is null)
        {
            HighlightOverlay.HidePersistent();
            return;
        }

        _viewModel.SelectedNode = node;
        if (!ReferenceEquals(_lastHierarchyNode, node))
        {
            _hoverSelectingNode = node;
            SelectNodeInHierarchy(node);
            _lastHierarchyNode = node;
        }

        DetailsTabs.SelectedItem = PropertiesTab;

        var visualNode = node;
        while (!HasOnScreenBounds(bounds) && visualNode.Parent is not null)
        {
            visualNode = visualNode.Parent;
            _viewModel.RefreshBounds(visualNode);
            bounds = GetPhysicalBounds(visualNode);
        }

        if (HasOnScreenBounds(bounds))
            HighlightOverlay.ShowPersistent(bounds);
        else
            HighlightOverlay.HidePersistent();
    }

    private void RecordingMonitorTimer_Tick(object? sender, EventArgs e)
    {
        if (!_viewModel.IsRecordingActive || _viewModel.IsRecordingPaused || !_viewModel.IsJavaMode || _viewModel.Root is null || _viewModel.CurrentWindow is null || _playbackInProgress)
        {
            _recordingLeftButtonDown = false;
            return;
        }

        var leftDown = (GetAsyncKeyState(VkLeftButton) & 0x8000) != 0;
        if (leftDown && !_recordingLeftButtonDown)
        {
            _recordingLeftButtonDown = true;
            GetCursorPos(out _recordingMouseDownPoint);
            _viewModel.Log($"[RECORDER] Mouse down detected at ({_recordingMouseDownPoint.X}, {_recordingMouseDownPoint.Y}). CurrentWindow={_viewModel.CurrentWindow.HwndDisplay}, Root='{_viewModel.Root.DisplayName}'.");
            CapturePassiveRecordingSnapshot(_recordingMouseDownPoint);
            return;
        }

        if (!leftDown && _recordingLeftButtonDown)
        {
            _recordingLeftButtonDown = false;
            if (_recordingCaptureInProgress)
            {
                _viewModel.Log("[RECORDER] Mouse up ignored because a previous capture is still in progress.");
                return;
            }
            if (!GetCursorPos(out var releasePoint)) return;
            var deltaX = Math.Abs(releasePoint.X - _recordingMouseDownPoint.X);
            var deltaY = Math.Abs(releasePoint.Y - _recordingMouseDownPoint.Y);
            _viewModel.Log($"[RECORDER] Mouse up detected at ({releasePoint.X}, {releasePoint.Y}). Delta=({deltaX}, {deltaY}).");
            if (deltaX > 8 || deltaY > 8)
            {
                _viewModel.Log("[RECORDER] Mouse gesture ignored because movement exceeded click threshold.");
                return;
            }
            _recordingCaptureInProgress = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                try { TryRecordPassiveClick(releasePoint); }
                finally
                {
                    _recordingCaptureInProgress = false;
                    _viewModel.Log("[RECORDER] Capture pipeline completed.");
                }
            }));
        }
    }

    private static bool Contains(ElementBounds bounds, NativePoint point) =>
        bounds.Width > 0 && bounds.Height > 0 && point.X >= bounds.X && point.X < (long)bounds.X + bounds.Width &&
        point.Y >= bounds.Y && point.Y < (long)bounds.Y + bounds.Height;

    private void TryRecordPassiveClick(NativePoint point)
    {
        if (_viewModel.IsRecordingPaused)
        {
            _viewModel.Log($"[RECORDER] Capture skipped because recording is paused. Point=({point.X}, {point.Y}).");
            ClearPassiveRecordingSnapshot();
            return;
        }

        _viewModel.Log($"[RECORDER] Capture pipeline started for point ({point.X}, {point.Y}).");
        if (_recordingMouseDownWindow is not null && _recordingMouseDownOnNativeClose)
        {
            _viewModel.Log($"[RECORDER] Native close was detected on mouse-down for window '{_recordingMouseDownWindow.Title}'. Recording CloseWindow immediately.");
            if (TryRecordNativeWindowClose(_recordingMouseDownWindow, point, "mouse-down close latch"))
            {
                ClearPassiveRecordingSnapshot();
                return;
            }
        }

        var candidateWindows = GetCandidateWindowHandlesFromPoint(point);
        if (ShouldPreferMouseDownSnapshot(candidateWindows))
        {
            _viewModel.Log($"[RECORDER] Preferring mouse-down snapshot because the top candidate hwnd changed after the click. Candidates={FormatHwndList(candidateWindows)}.");
            if (TryRecordPassiveClickFromMouseDownSnapshot(point))
            {
                SchedulePostClickAutoAttach(point, "post-click modal switch");
                ClearPassiveRecordingSnapshot();
                return;
            }
        }

        TryAutoAttachJavaWindowFromPoint(point, "passive click");
        if (_viewModel.CurrentWindow is not null && IsPointInNativeCloseButtonRect(_viewModel.CurrentWindow, point))
        {
            if (TryRecordNativeWindowClose(_viewModel.CurrentWindow, point, "active window"))
            {
                ClearPassiveRecordingSnapshot();
                return;
            }
        }
        if (_viewModel.CurrentWindow is null || _viewModel.Root is null)
        {
            if (TryRecordPassiveClickFromMouseDownSnapshot(point))
            {
                ClearPassiveRecordingSnapshot();
                return;
            }

            _viewModel.Log("[RECORDER] Capture aborted because no Java window/root is attached after auto-attach probe.");
            ClearPassiveRecordingSnapshot();
            return;
        }

        if (!IsPointWithinCurrentJavaWindow(point))
        {
            if (TryRecordPassiveClickFromMouseDownSnapshot(point))
            {
                ClearPassiveRecordingSnapshot();
                return;
            }

            _viewModel.Log($"[RECORDER] Capture ignored because point ({point.X}, {point.Y}) is outside attached Java window {_viewModel.CurrentWindow.HwndDisplay}.");
            ClearPassiveRecordingSnapshot();
            return;
        }

        if (!TryResolveJavaNodeAtScreenPoint(point, out var node, out var bounds) || node is null)
        {
            if (TryRecordPassiveClickFromMouseDownSnapshot(point))
            {
                ClearPassiveRecordingSnapshot();
                return;
            }

            _viewModel.Log($"Passive recording could not resolve a Java node at ({point.X}, {point.Y}) using the hover hit-test path.");
            ClearPassiveRecordingSnapshot();
            return;
        }

        StabilizePassiveRecordingCandidate(point, ref node, ref bounds);

        _viewModel.SelectedNode = node;
        _hoverSelectingNode = node;
        _lastHierarchyNode = node;
        HighlightOverlay.Show(bounds, TimeSpan.FromSeconds(1.1));
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            SelectNodeInHierarchy(node);
        }));

        var isDoubleClick = !string.IsNullOrWhiteSpace(node.Path)
                            && string.Equals(_lastPassiveClickPath, node.Path, StringComparison.OrdinalIgnoreCase)
                            && DateTime.UtcNow - _lastPassiveClickAtUtc <= TimeSpan.FromMilliseconds(GetDoubleClickTime());

        _viewModel.Log($"Passive recording captured click candidate. Node='{node.DisplayName}', Path='{node.Path}', Point=({point.X}, {point.Y}), DoubleClickCandidate={isDoubleClick}.");
        _viewModel.Log($"[RECORDER] Candidate details: Role='{node.Role}', RoleEnUs='{node.RoleEnUs}', Name='{node.Name}', States='{node.States}', Bounds=({node.X},{node.Y},{node.Width},{node.Height}), PhysicalBounds=({bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}), Path='{node.Path}'.");

        int? windowOffsetX = null;
        int? windowOffsetY = null;
        if (_viewModel.CurrentWindow is not null && GetWindowRect(_viewModel.CurrentWindow.Hwnd, out var stepRect))
        {
            windowOffsetX = point.X - stepRect.Left;
            windowOffsetY = point.Y - stepRect.Top;
        }

        var recorded = isDoubleClick
            ? _viewModel.PromoteLastRecordedClickToDoubleClick()
            : _viewModel.RecordJavaAction(
                JavaRecordedActionKind.Click,
                recordedScreenX: point.X,
                recordedScreenY: point.Y,
                windowOffsetX: windowOffsetX,
                windowOffsetY: windowOffsetY) is not null;

        if (recorded)
        {
            _lastPassiveClickAtUtc = DateTime.UtcNow;
            _lastPassiveClickPath = node.Path;
            UpdateRecordingBadge();
            _viewModel.Log($"Passive recording stored {(isDoubleClick ? "double-click" : "click")} step for '{node.DisplayName}'.");
            _viewModel.Log($"[RECORDER] Step stored. StepCount={_viewModel.RecordingStepCount}, ObjectCount={_viewModel.RecordingObjectCount}, LastPath='{_lastPassiveClickPath}'.");
        }
        else
        {
            _viewModel.Log($"Passive recording did not create a step for '{node.DisplayName}'.");
        }

        ClearPassiveRecordingSnapshot();
    }

    private bool ShouldPreferMouseDownSnapshot(IReadOnlyList<IntPtr> candidateWindows)
    {
        if (_recordingMouseDownWindow is null) return false;
        if (candidateWindows.Count == 0) return false;

        var topCandidate = candidateWindows[0];
        if (topCandidate == IntPtr.Zero) return false;
        if (topCandidate == _recordingMouseDownWindow.Hwnd) return false;

        var snapshotName = _recordingMouseDownNode?.DisplayName ?? "(native window chrome)";
        _viewModel.Log($"[RECORDER] Mouse-down snapshot will be preferred. MouseDownWindow={_recordingMouseDownWindow.HwndDisplay}, TopCandidate=0x{topCandidate.ToInt64():X}, Node='{snapshotName}'.");
        return true;
    }

    private async void SchedulePostClickAutoAttach(NativePoint point, string reason)
    {
        await Task.Delay(140);
        if (!_viewModel.IsRecordingActive || _viewModel.IsRecordingPaused) return;

        TryAutoAttachJavaWindowFromPoint(point, reason);
    }

    private void CapturePassiveRecordingSnapshot(NativePoint point)
    {
        ClearPassiveRecordingSnapshot();
        TryAutoAttachJavaWindowFromPoint(point, "passive click pre-capture");
        if (_viewModel.CurrentWindow is null) return;

        // Keep the original window context even when the click lands on native title-bar chrome such as X.
        _recordingMouseDownWindow = _viewModel.CurrentWindow;
        _recordingMouseDownOnNativeClose = IsPointInNativeCloseButtonRect(_recordingMouseDownWindow, point);
        if (_recordingMouseDownOnNativeClose)
        {
            _viewModel.Log($"[RECORDER] Mouse-down landed on native close chrome for window '{_recordingMouseDownWindow.Title}'.");
        }

        if (_viewModel.Root is null) return;
        if (!IsPointWithinCurrentJavaWindow(point)) return;
        if (!TryResolveJavaNodeAtScreenPoint(point, out var node, out var bounds) || node is null) return;

        _recordingMouseDownNode = node;
        _recordingMouseDownBounds = bounds;
        _viewModel.Log($"[RECORDER] Mouse-down snapshot captured. Node='{node.DisplayName}', Window='{_recordingMouseDownWindow.Title}', Path='{node.Path}'.");
    }

    private bool TryRecordPassiveClickFromMouseDownSnapshot(NativePoint releasePoint)
    {
        if (_recordingMouseDownWindow is null)
        {
            _viewModel.Log("[RECORDER] Mouse-down snapshot fallback unavailable.");
            return false;
        }

        var window = _recordingMouseDownWindow;
        var node = _recordingMouseDownNode;
        var bounds = _recordingMouseDownBounds;
        _viewModel.Log($"[RECORDER] Using mouse-down snapshot fallback. Node='{node?.DisplayName ?? "(native window chrome)"}', Window='{window.Title}', ReleasePoint=({releasePoint.X}, {releasePoint.Y}).");

        if (_recordingMouseDownOnNativeClose || IsPointInNativeCloseButtonRect(window, releasePoint))
        {
            return TryRecordNativeWindowClose(window, releasePoint, "mouse-down snapshot");
        }

        if (node is null)
        {
            _viewModel.Log("[RECORDER] Mouse-down snapshot did not include a Java node and the release point was not on native close chrome.");
            return false;
        }

        if (HasOnScreenBounds(bounds))
        {
            HighlightOverlay.Show(bounds, TimeSpan.FromSeconds(1.0));
        }

        int? windowOffsetX = null;
        int? windowOffsetY = null;
        if (GetWindowRect(window.Hwnd, out var stepRect))
        {
            windowOffsetX = releasePoint.X - stepRect.Left;
            windowOffsetY = releasePoint.Y - stepRect.Top;
        }

        var step = _viewModel.RecordJavaActionForNode(
            JavaRecordedActionKind.Click,
            window,
            node,
            recordedScreenX: releasePoint.X,
            recordedScreenY: releasePoint.Y,
            windowOffsetX: windowOffsetX,
            windowOffsetY: windowOffsetY);

        if (step is null)
        {
            _viewModel.Log($"[RECORDER] Mouse-down snapshot fallback could not create a recorded step for '{node.DisplayName}'.");
            return false;
        }

        _lastPassiveClickAtUtc = DateTime.UtcNow;
        _lastPassiveClickPath = node.Path;
        UpdateRecordingBadge();
        _viewModel.Log($"[RECORDER] Mouse-down snapshot fallback recorded click step {step.Sequence} for '{node.DisplayName}'.");
        return true;
    }

    private bool TryRecordNativeWindowClose(JavaWindowInfo window, NativePoint point, string source)
    {
        int? windowOffsetX = null;
        int? windowOffsetY = null;
        if (GetWindowRect(window.Hwnd, out var rect))
        {
            windowOffsetX = point.X - rect.Left;
            windowOffsetY = point.Y - rect.Top;
        }

        var step = _viewModel.RecordJavaWindowAction(
            JavaRecordedActionKind.CloseWindow,
            window,
            recordedScreenX: point.X,
            recordedScreenY: point.Y,
            windowOffsetX: windowOffsetX,
            windowOffsetY: windowOffsetY);
        if (step is null)
        {
            _viewModel.Log($"[RECORDER] Native window close recording failed for '{window.Title}' from {source}.");
            return false;
        }

        UpdateRecordingBadge();
        _viewModel.Log($"[RECORDER] Recorded native window close step {step.Sequence} for '{window.Title}' from {source}.");
        return true;
    }

    private static bool TryGetNativeCloseButtonBounds(JavaWindowInfo window, out ElementBounds bounds)
    {
        bounds = new ElementBounds(0, 0, 0, 0);
        if (!GetWindowRect(window.Hwnd, out var rect)) return false;
        var buttonWidth = Math.Max(32, GetSystemMetrics(SmCxSize) + 12);
        var buttonHeight = Math.Max(24, GetSystemMetrics(SmCySize) + 8);
        var closeLeft = rect.Right - buttonWidth;
        bounds = new ElementBounds(closeLeft, rect.Top, buttonWidth, buttonHeight);
        return true;
    }

    private static bool IsPointInNativeCloseButtonRect(JavaWindowInfo window, NativePoint point)
    {
        if (!TryGetNativeCloseButtonBounds(window, out var bounds)) return false;
        return point.X >= bounds.X && point.X < bounds.X + bounds.Width && point.Y >= bounds.Y && point.Y < bounds.Y + bounds.Height;
    }

    private void ClearPassiveRecordingSnapshot()
    {
        _recordingMouseDownNode = null;
        _recordingMouseDownWindow = null;
        _recordingMouseDownOnNativeClose = false;
        _recordingMouseDownBounds = new ElementBounds(0, 0, 0, 0);
    }

    private void StabilizePassiveRecordingCandidate(NativePoint point, ref AccessibleNode node, ref ElementBounds bounds)
    {
        if (!IsTabLike(node)) return;

        var originalPath = node.Path;
        var originalName = node.Name;
        _viewModel.Log($"[RECORDER] Tab-like click candidate detected. Waiting briefly and refreshing tree before capture. Node='{node.DisplayName}', Path='{node.Path}'.");

        Thread.Sleep(110);
        if (!_viewModel.RefreshCurrentJavaTree("passive recorder tab settle"))
        {
            _viewModel.Log("[RECORDER] Tab settle refresh failed; using initial candidate.");
            return;
        }

        if (!TryResolveJavaNodeAtScreenPoint(point, out var settledNode, out var settledBounds) || settledNode is null)
        {
            _viewModel.Log("[RECORDER] Tab settle re-hit-test failed; using initial candidate.");
            return;
        }

        if (!IsBetterRecordingCandidate(settledNode, node)) return;

        node = settledNode;
        bounds = settledBounds;
        _viewModel.Log($"[RECORDER] Tab click candidate stabilized. From='{originalName}' Path='{originalPath}' To='{node.Name}' Path='{node.Path}'.");
    }

    private static bool IsBetterRecordingCandidate(AccessibleNode candidate, AccessibleNode original)
    {
        if (IsTab(candidate)) return true;
        if (!IsTab(original) && IsTabLike(candidate)) return true;
        return !string.IsNullOrWhiteSpace(candidate.Name) &&
               !string.Equals(candidate.Path, original.Path, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTabLike(AccessibleNode node) =>
        IsTab(node) ||
        ContainsRole(node, "page tab list") ||
        ContainsRole(node.Parent, "page tab list");

    private static bool IsTab(AccessibleNode node) => ContainsRole(node, "page tab");

    private static bool ContainsRole(AccessibleNode? node, string role) =>
        node is not null &&
        ((node.Role?.Contains(role, StringComparison.OrdinalIgnoreCase) ?? false) ||
         (node.RoleEnUs?.Contains(role, StringComparison.OrdinalIgnoreCase) ?? false));

    private bool TryAutoAttachJavaWindowFromPoint(NativePoint point, string reason)
    {
        var candidateWindows = GetCandidateWindowHandlesFromPoint(point);
        _viewModel.Log($"[RECORDER] Auto-attach probe for '{reason}' found {candidateWindows.Count} candidate hwnd(s): {FormatHwndList(candidateWindows)}.");
        var insideCurrentRect = IsPointInsideCurrentJavaWindowRect(point);
        if (candidateWindows.Count == 0)
        {
            if (insideCurrentRect)
            {
                _viewModel.Log($"[RECORDER] Auto-attach probe for '{reason}' found no hwnd candidates, but point is still inside current Java window.");
                return true;
            }

            return false;
        }

        if (_viewModel.CurrentWindow is not null && candidateWindows.Contains(_viewModel.CurrentWindow.Hwnd))
        {
            var topCandidate = candidateWindows[0];
            if (topCandidate == _viewModel.CurrentWindow.Hwnd)
            {
                _viewModel.Log($"[RECORDER] Auto-attach probe resolved to current Java window {_viewModel.CurrentWindow.HwndDisplay}.");
                return true;
            }

            _viewModel.Log($"[RECORDER] Auto-attach probe detected overlapping/owned hwnd 0x{topCandidate.ToInt64():X} while current Java window is {_viewModel.CurrentWindow.HwndDisplay}; attempting modal switch.");
        }
        else if (insideCurrentRect)
        {
            _viewModel.Log($"[RECORDER] Auto-attach probe found a different hwnd while point is inside current Java window bounds; attempting modal switch for '{reason}'.");
        }

        var now = DateTime.UtcNow;
        var probeKey = candidateWindows[0];
        if (probeKey == _lastAutoAttachProbeHwnd && now - _lastAutoAttachProbeAtUtc < TimeSpan.FromMilliseconds(600))
        {
            _viewModel.Log($"[RECORDER] Auto-attach probe throttled for hwnd 0x{probeKey.ToInt64():X}.");
            return false;
        }
        _lastAutoAttachProbeHwnd = probeKey;
        _lastAutoAttachProbeAtUtc = now;

        foreach (var candidate in candidateWindows)
        {
            var attached = _viewModel.TryAutoAttachJavaWindow(candidate, reason);
            if (!attached) continue;
            UpdateRecordingBadge();
            return true;
        }

        return false;
    }

    private bool IsPointWithinCurrentJavaWindow(NativePoint point)
    {
        if (_viewModel.CurrentWindow is null) return false;
        return IsPointInsideCurrentJavaWindowRect(point) || GetCandidateWindowHandlesFromPoint(point).Contains(_viewModel.CurrentWindow.Hwnd);
    }

    private bool IsPointInsideCurrentJavaWindowRect(NativePoint point)
    {
        if (_viewModel.CurrentWindow is null) return false;
        if (!GetWindowRect(_viewModel.CurrentWindow.Hwnd, out var rect)) return false;
        var inside = point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom;
        _viewModel.Log($"[RECORDER] Current Java window bounds check: Hwnd={_viewModel.CurrentWindow.HwndDisplay}, Rect=({rect.Left},{rect.Top},{rect.Right},{rect.Bottom}), Point=({point.X},{point.Y}), Inside={inside}.");
        return inside;
    }

    private List<IntPtr> GetCandidateWindowHandlesFromPoint(NativePoint point)
    {
        var hwndAtPoint = WindowFromPoint(point);
        return hwndAtPoint == IntPtr.Zero ? [] : GetCandidateWindowHandles(hwndAtPoint);
    }

    private static List<IntPtr> GetCandidateWindowHandles(IntPtr hwnd)
    {
        var candidates = new List<IntPtr>();
        var seen = new HashSet<nint>();

        static void AddCandidate(List<IntPtr> list, HashSet<nint> visited, IntPtr value)
        {
            if (value == IntPtr.Zero) return;
            if (visited.Add(value)) list.Add(value);
        }

        var current = hwnd;
        for (var depth = 0; depth < 8 && current != IntPtr.Zero; depth++)
        {
            AddCandidate(candidates, seen, current);
            AddCandidate(candidates, seen, GetAncestor(current, GaRoot));
            AddCandidate(candidates, seen, GetWindow(current, GwOwner));
            current = GetParent(current);
        }

        return candidates;
    }

    private static string FormatHwndList(IReadOnlyList<IntPtr> hwnds) =>
        hwnds.Count == 0 ? "(none)" : string.Join(", ", hwnds.Select(hwnd => $"0x{hwnd.ToInt64():X}"));

    private bool TryResolveJavaNodeAtScreenPoint(NativePoint point, out AccessibleNode? node, out ElementBounds bounds)
    {
        node = null;
        bounds = new ElementBounds(0, 0, 0, 0);
        if (_viewModel.Root is null || _viewModel.CurrentWindow is null) return false;
        if (!GetWindowRect(_viewModel.CurrentWindow.Hwnd, out var native)) return false;
        if (point.X < native.Left || point.X >= native.Right || point.Y < native.Top || point.Y >= native.Bottom)
        {
            _viewModel.Log($"[RECORDER] Resolve skipped because point is outside native window rect. Point=({point.X},{point.Y}), Rect=({native.Left},{native.Top},{native.Right},{native.Bottom}).");
            return false;
        }

        _viewModel.RefreshBounds(_viewModel.Root);
        var root = _viewModel.Root;
        if (!root.HasValidBounds) return false;

        var scaleX = (double)(native.Right - native.Left) / root.Width;
        var scaleY = (double)(native.Bottom - native.Top) / root.Height;
        var jabX = root.X + (int)Math.Round((point.X - native.Left) / scaleX);
        var jabY = root.Y + (int)Math.Round((point.Y - native.Top) / scaleY);
        _viewModel.Log($"[RECORDER] Resolving point. Physical=({point.X},{point.Y}), NativeRect=({native.Left},{native.Top},{native.Right},{native.Bottom}), RootBounds=({root.X},{root.Y},{root.Width},{root.Height}), Scale=({scaleX:0.###},{scaleY:0.###}), LogicalProbe=({jabX},{jabY}).");

        node = _viewModel.InspectAt(point.X, point.Y);
        bounds = node is null ? new ElementBounds(0, 0, 0, 0) : GetPhysicalBounds(node);
        _viewModel.Log(node is null
            ? "[RECORDER] Physical JAB hit-test returned no node."
            : $"[RECORDER] Physical JAB hit-test returned '{node.DisplayName}' with physical bounds ({bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}). ContainsPoint={Contains(bounds, point)}.");
        if (node is null || !Contains(bounds, point))
        {
            var logicalNode = _viewModel.InspectAt(jabX, jabY);
            if (logicalNode is not null)
            {
                node = logicalNode;
                bounds = GetPhysicalBounds(logicalNode);
                _viewModel.Log($"[RECORDER] Logical JAB hit-test returned '{node.DisplayName}' with physical bounds ({bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}). ContainsPoint={Contains(bounds, point)}.");
            }
            else
            {
                _viewModel.Log("[RECORDER] Logical JAB hit-test returned no node.");
            }
        }

        if (node is null) return false;

        var visualNode = node;
        while (!HasOnScreenBounds(bounds) && visualNode.Parent is not null)
        {
            visualNode = visualNode.Parent;
            _viewModel.RefreshBounds(visualNode);
            bounds = GetPhysicalBounds(visualNode);
        }

        return HasOnScreenBounds(bounds);
    }

    private void SelectNodeInHierarchy(AccessibleNode node, int attempt = 0)
    {
        var path = new Stack<AccessibleNode>();
        for (var cursor = node; cursor is not null; cursor = cursor.Parent) path.Push(cursor);
        ItemsControl owner = AccessibilityTree;
        while (path.Count > 0)
        {
            var item = path.Pop();
            owner.UpdateLayout();
            var container = owner.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (container is null)
            {
                if (owner is TreeViewItem parent) { parent.IsExpanded = true; parent.UpdateLayout(); }
                AccessibilityTree.UpdateLayout();
                container = owner.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            }
            if (container is null)
            {
                if (attempt < 6)
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                        new Action(() => SelectNodeInHierarchy(node, attempt + 1)));
                return;
            }
            if (path.Count == 0)
            {
                container.IsSelected = true;
                container.BringIntoView();
                return;
            }
            container.IsExpanded = true;
            container.BringIntoView();
            container.UpdateLayout();
            AccessibilityTree.UpdateLayout();
            owner = container;
        }
    }

    private void DetailsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || e.Source != DetailsTabs || DetailsTabs.SelectedItem is not TabItem tab ||
            !(tab.Header?.ToString()?.Contains("AUTOMATION", StringComparison.OrdinalIgnoreCase) ?? false)) return;
        _viewModel.RefreshSupportedActions();
    }

    private void FocusAction_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsJavaMode)
        {
            ExecuteJavaRecordedAction(JavaRecordedActionKind.Focus, "", captureStep: true);
            return;
        }
        _viewModel.FocusSelected();
    }

    private void ClickAction_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsJavaMode)
        {
            ExecuteJavaRecordedAction(JavaRecordedActionKind.Click, "", captureStep: true);
            return;
        }

        if (_viewModel.IsWindowsMode)
        {
            var windowsNode = _viewModel.SelectedWindowsNode;
            if (windowsNode is null) { _viewModel.ReportAutomation("Select a Windows element first."); return; }
            if (!_viewModel.InvokeDefaultAction()) PhysicalClick(windowsNode, 1);
            return;
        }

        if (_viewModel.SelectedNode is null) { _viewModel.ReportAutomation("Select an element first."); return; }
        if (!_viewModel.InvokeDefaultAction()) PhysicalClick(_viewModel.SelectedNode, 1);
    }

    private void DoubleClickAction_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsJavaMode)
        {
            ExecuteJavaRecordedAction(JavaRecordedActionKind.DoubleClick, "", captureStep: true);
            return;
        }

        if (_viewModel.IsWindowsMode)
        {
            var windowsNode = _viewModel.SelectedWindowsNode;
            if (windowsNode is null) { _viewModel.ReportAutomation("Select a Windows element first."); return; }
            PhysicalClick(windowsNode, 2);
            return;
        }

        if (_viewModel.SelectedNode is null) { _viewModel.ReportAutomation("Select an element first."); return; }
        PhysicalClick(_viewModel.SelectedNode, 2);
    }

    private void SetTextAction_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsJavaMode)
        {
            ExecuteJavaRecordedAction(JavaRecordedActionKind.SetText, AutomationInput.Text, captureStep: true);
            return;
        }
        _viewModel.SetSelectedText(AutomationInput.Text);
    }

    private void TypeTextAction_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsJavaMode)
        {
            ExecuteJavaRecordedAction(JavaRecordedActionKind.TypeText, AutomationInput.Text, captureStep: true);
            return;
        }

        if (_viewModel.IsWindowsMode)
        {
            var windowsNode = _viewModel.SelectedWindowsNode;
            if (windowsNode is null) { _viewModel.ReportAutomation("Select a Windows element first."); return; }
            _viewModel.FocusSelected();
            if (_viewModel.SelectedWindowsWindow?.Model is { } selectedWindow) SetForegroundWindow(selectedWindow.Hwnd);
            else if (windowsNode.NativeHandle != IntPtr.Zero) SetForegroundWindow(windowsNode.NativeHandle);
            Thread.Sleep(100);
            var sentWindows = SendUnicodeText(AutomationInput.Text);
            _viewModel.ReportAutomation($"Typed {sentWindows} of {AutomationInput.Text.Length} Unicode character(s). Text was inserted at the control's current caret position.");
            _viewModel.Log($"Typed {sentWindows} character(s) into {windowsNode.DisplayName}.");
            return;
        }

        if (_viewModel.SelectedNode is null) { _viewModel.ReportAutomation("Select an element first."); return; }
        _viewModel.FocusSelected();
        if (_viewModel.CurrentWindow is not null) SetForegroundWindow(_viewModel.CurrentWindow.Hwnd);
        Thread.Sleep(100);
        var sent = SendUnicodeText(AutomationInput.Text);
        _viewModel.ReportAutomation($"Typed {sent} of {AutomationInput.Text.Length} Unicode character(s). Text was inserted at the control's current caret position.");
        _viewModel.Log($"Typed {sent} character(s) into {_viewModel.SelectedNode.DisplayName}.");
    }

    private void GetTextAction_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsJavaMode)
        {
            ExecuteJavaRecordedAction(JavaRecordedActionKind.GetText, "", captureStep: true);
            return;
        }
        _viewModel.GetSelectedText();
    }

    private bool ExecuteJavaRecordedAction(JavaRecordedActionKind actionKind, string inputText, bool captureStep)
    {
        _viewModel.Log($"ExecuteJavaRecordedAction invoked. Action={actionKind}, CaptureStep={captureStep}, InputLength={inputText.Length}, HasSelectedNode={_viewModel.SelectedNode is not null}, RecordingActive={_viewModel.IsRecordingActive}, Paused={_viewModel.IsRecordingPaused}.");
        if (actionKind != JavaRecordedActionKind.CloseWindow && _viewModel.SelectedNode is null)
        {
            _viewModel.ReportAutomation("Select a Java element first.");
            _viewModel.Log("ExecuteJavaRecordedAction aborted because no Java node is selected.");
            return false;
        }

        var actionNode = _viewModel.SelectedNode ?? _viewModel.Root;
        if (actionNode is null)
        {
            _viewModel.ReportAutomation("Attach to a Java window first.");
            _viewModel.Log("ExecuteJavaRecordedAction aborted because no Java root is available.");
            return false;
        }

        var result = _javaActions.Execute(actionKind, actionNode, inputText, this);
        var success = result.Success;
        _viewModel.ReportAutomation(result.Text is null ? result.Message : $"{result.Message}{Environment.NewLine}{result.Text}");

        _viewModel.Log($"ExecuteJavaRecordedAction completed. Action={actionKind}, Success={success}, CaptureStep={captureStep}.");
        if (captureStep && success && !_viewModel.IsRecordingPaused)
        {
            var recordedStep = actionKind == JavaRecordedActionKind.CloseWindow && _viewModel.CurrentWindow is not null
                ? _viewModel.RecordJavaWindowAction(actionKind, _viewModel.CurrentWindow, inputText)
                : _viewModel.RecordJavaAction(actionKind, inputText);
            _viewModel.Log(recordedStep is null
                ? $"ExecuteJavaRecordedAction did not create a recorded step for action {actionKind}."
                : $"ExecuteJavaRecordedAction recorded step {recordedStep.Sequence} for action {actionKind}.");
        }
        else if (captureStep && success && _viewModel.IsRecordingPaused)
        {
            _viewModel.Log($"ExecuteJavaRecordedAction skipped step capture for action {actionKind} because recording is paused.");
        }
        if (_viewModel.IsRecordingActive) UpdateRecordingBadge();
        return success;
    }

    private bool ExecutePlaybackAction(JavaRecordedStep step, JavaRecordedStep? nextStep)
    {
        if (step.ActionKind == JavaRecordedActionKind.CloseWindow)
        {
            return ExecuteJavaRecordedAction(step.ActionKind, step.InputText, captureStep: false);
        }

        var expectsTransition = _viewModel.DoesStepRequireWindowTransition(step, nextStep);
        if (!expectsTransition)
        {
            return ExecuteJavaRecordedAction(step.ActionKind, step.InputText, captureStep: false);
        }

        _viewModel.Log($"Playback step {step.Sequence} expects window/modal transition; using transition-aware execution.");
        return step.ActionKind switch
        {
            JavaRecordedActionKind.Click when _viewModel.SelectedNode is not null => PhysicalClickForPlayback(step, _viewModel.SelectedNode, 1),
            JavaRecordedActionKind.DoubleClick when _viewModel.SelectedNode is not null => PhysicalClickForPlayback(step, _viewModel.SelectedNode, 2),
            _ => ExecuteJavaRecordedAction(step.ActionKind, step.InputText, captureStep: false)
        };
    }

    bool IJavaActionExecutionHost.Focus(AccessibleNode node, out string message)
    {
        var success = _viewModel.FocusSelected();
        message = success ? "Focus requested successfully." : $"Focus request failed for {node.DisplayName}.";
        return success;
    }

    bool IJavaActionExecutionHost.CloseWindow(AccessibleNode? node, out string message)
    {
        if (_viewModel.CurrentWindow is null)
        {
            message = "No active Java window is attached.";
            return false;
        }

        var success = PostMessage(_viewModel.CurrentWindow.Hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
        message = success
            ? $"Close requested for window '{_viewModel.CurrentWindow.Title}'."
            : $"Close request failed for window '{_viewModel.CurrentWindow.Title}'.";
        _viewModel.Log(message);
        return success;
    }

    bool IJavaActionExecutionHost.InvokeDefaultAction(AccessibleNode node, out string message)
    {
        var success = _viewModel.InvokeDefaultAction();
        message = success ? $"Executed semantic action on {node.DisplayName}." : $"No semantic action was available for {node.DisplayName}.";
        return success;
    }

    bool IJavaActionExecutionHost.SetText(AccessibleNode node, string text, out string message)
    {
        var success = _viewModel.SetSelectedText(text);
        message = success ? $"Text set successfully on {node.DisplayName} ({text.Length} characters)." : $"Set text failed for {node.DisplayName}.";
        return success;
    }

    string IJavaActionExecutionHost.GetText(AccessibleNode node, out string message)
    {
        var text = _viewModel.GetSelectedText();
        message = $"Read text from {node.DisplayName}.";
        return text;
    }

    bool IJavaActionExecutionHost.PhysicalClick(AccessibleNode node, int count, out string message)
    {
        var success = PhysicalClick(node, count);
        message = success
            ? $"{(count == 2 ? "Double-clicked" : "Clicked")} {node.DisplayName} using physical input."
            : $"Physical click failed for {node.DisplayName}.";
        return success;
    }

    int IJavaActionExecutionHost.TypeUnicodeText(AccessibleNode node, string text, out string message)
    {
        _viewModel.FocusSelected();
        if (_viewModel.CurrentWindow is not null) SetForegroundWindow(_viewModel.CurrentWindow.Hwnd);
        Thread.Sleep(100);
        var sent = SendUnicodeText(text);
        message = $"Typed {sent} of {text.Length} Unicode character(s) into {node.DisplayName}.";
        _viewModel.Log(message);
        return sent;
    }

    void IJavaActionExecutionHost.BeforeAction(AccessibleNode node) => HighlightCurrentJavaSelection();

    void IJavaActionExecutionHost.BetweenVirtualKeyClicks() => Thread.Sleep(80);

    private bool PhysicalClick(AccessibleNode node, int count)
    {
        var visualNode = node;
        ElementBounds bounds;
        while (true)
        {
            _viewModel.RefreshBounds(visualNode);
            bounds = GetPhysicalBounds(visualNode);
            if (HasOnScreenBounds(bounds) || visualNode.Parent is null) break;
            visualNode = visualNode.Parent;
        }
        if (!HasOnScreenBounds(bounds))
        {
            _viewModel.ReportAutomation("The selected element has no usable on-screen bounds.");
            return false;
        }

        if (_viewModel.CurrentWindow is not null)
        {
            SetForegroundWindow(_viewModel.CurrentWindow.Hwnd);
            Thread.Sleep(70);
        }

        PhysicalClickAtPoint(
            new NativePoint { X = bounds.X + bounds.Width / 2, Y = bounds.Y + bounds.Height / 2 },
            count,
            node.DisplayName,
            "physical input");
        return true;
    }

    private bool PhysicalClickForResult(AccessibleNode node, int count) => PhysicalClick(node, count);

    private void PhysicalClickAtPoint(NativePoint point, int count, string displayName, string source)
    {
        GetCursorPos(out var original);
        SetCursorPos(point.X, point.Y);
        for (var i = 0; i < count; i++)
        {
            MouseEvent(MouseLeftDown, 0, 0, 0, UIntPtr.Zero);
            MouseEvent(MouseLeftUp, 0, 0, 0, UIntPtr.Zero);
            if (i + 1 < count) Thread.Sleep(100);
        }
        SetCursorPos(original.X, original.Y);
        var action = count == 2 ? "Double-clicked" : "Clicked";
        _viewModel.ReportAutomation($"{action} {displayName} at physical point ({point.X}, {point.Y}).");
        _viewModel.Log($"{action} {displayName} using {source}.");
    }

    private bool PhysicalClickForPlayback(JavaRecordedStep step, AccessibleNode node, int count)
    {
        _viewModel.FocusSelected();
        if (_viewModel.CurrentWindow is not null)
        {
            SetForegroundWindow(_viewModel.CurrentWindow.Hwnd);
            Thread.Sleep(70);
        }

        if (TryGetRecordedPlaybackPoint(step, out var playbackPoint))
        {
            PhysicalClickAtPoint(playbackPoint, count, node.DisplayName, "recorded window-relative point");
            Thread.Sleep(count == 2 ? 180 : 140);
            return true;
        }

        PhysicalClick(node, count);
        Thread.Sleep(count == 2 ? 180 : 140);
        return true;
    }

    private bool TryGetRecordedPlaybackPoint(JavaRecordedStep step, out NativePoint point)
    {
        point = default;
        if (_viewModel.CurrentWindow is null || !step.WindowOffsetX.HasValue || !step.WindowOffsetY.HasValue) return false;
        if (!GetWindowRect(_viewModel.CurrentWindow.Hwnd, out var rect)) return false;

        point = new NativePoint
        {
            X = rect.Left + step.WindowOffsetX.Value,
            Y = rect.Top + step.WindowOffsetY.Value
        };

        return point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom;
    }

    private void PhysicalClick(WindowsAutomationNode node, int count)
    {
        if (!TryGetHighlightBounds(node, out var visualNode, out var bounds))
        {
            _viewModel.ReportAutomation("The selected Windows element has no usable on-screen bounds.");
            return;
        }

        if (_viewModel.SelectedWindowsWindow?.Model is { } selectedWindow) SetForegroundWindow(selectedWindow.Hwnd);
        else if (visualNode.NativeHandle != IntPtr.Zero) SetForegroundWindow(visualNode.NativeHandle);

        GetCursorPos(out var original);
        SetCursorPos(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        for (var i = 0; i < count; i++)
        {
            MouseEvent(MouseLeftDown, 0, 0, 0, UIntPtr.Zero);
            MouseEvent(MouseLeftUp, 0, 0, 0, UIntPtr.Zero);
            if (i + 1 < count) Thread.Sleep(100);
        }
        SetCursorPos(original.X, original.Y);

        var action = count == 2 ? "Double-clicked" : "Clicked";
        var fallback = ReferenceEquals(node, visualNode) ? "" : $" using nearest on-screen ancestor {visualNode.DisplayName}";
        _viewModel.ReportAutomation($"{action} {node.DisplayName}{fallback} at physical point ({bounds.X + bounds.Width / 2}, {bounds.Y + bounds.Height / 2}).");
        _viewModel.Log($"{action} {node.DisplayName}{fallback} using physical input.");
    }

    private static int SendUnicodeText(string text)
    {
        var inputs = new List<NativeInput>(text.Length * 2);
        foreach (var character in text)
        {
            inputs.Add(NativeInput.Unicode(character, false));
            inputs.Add(NativeInput.Unicode(character, true));
        }
        return inputs.Count == 0 ? 0 : (int)SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<NativeInput>()) / 2;
    }

    private ElementBounds GetPhysicalBounds(AccessibleNode node)
    {
        var root = _viewModel.Root;
        var window = _viewModel.CurrentWindow;
        if (root is null || window is null || !node.HasValidBounds) return new(node.X, node.Y, node.Width, node.Height);
        _viewModel.RefreshBounds(root);
        if (!root.HasValidBounds || !GetWindowRect(window.Hwnd, out var native)) return new(node.X, node.Y, node.Width, node.Height);

        var direct = new ElementBounds(node.X, node.Y, node.Width, node.Height);
        if (LooksLikePhysicalScreenBounds(direct, native))
        {
            return direct;
        }

        var nativeWidth = native.Right - native.Left;
        var nativeHeight = native.Bottom - native.Top;
        var scaleX = (double)nativeWidth / root.Width;
        var scaleY = (double)nativeHeight / root.Height;
        if (scaleX is < 0.5 or > 4 || scaleY is < 0.5 or > 4) return new(node.X, node.Y, node.Width, node.Height);

        return new ElementBounds(
            native.Left + (int)Math.Round((node.X - root.X) * scaleX),
            native.Top + (int)Math.Round((node.Y - root.Y) * scaleY),
            Math.Max(1, (int)Math.Round(node.Width * scaleX)),
            Math.Max(1, (int)Math.Round(node.Height * scaleY)));
    }

    private static bool LooksLikePhysicalScreenBounds(ElementBounds bounds, NativeRect window)
    {
        if (!HasOnScreenBounds(bounds)) return false;
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;

        var windowBounds = new ElementBounds(
            window.Left,
            window.Top,
            Math.Max(1, window.Right - window.Left),
            Math.Max(1, window.Bottom - window.Top));

        if (IntersectionArea(bounds, windowBounds) > 0) return true;

        var nearHorizontal = bounds.X < window.Right + 80 && bounds.X + bounds.Width > window.Left - 80;
        var nearVertical = bounds.Y < window.Bottom + 120 && bounds.Y + bounds.Height > window.Top - 120;
        return nearHorizontal && nearVertical;
    }

    private static long IntersectionArea(ElementBounds a, ElementBounds b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min((long)a.X + a.Width, (long)b.X + b.Width);
        var bottom = Math.Min((long)a.Y + a.Height, (long)b.Y + b.Height);
        return right <= left || bottom <= top ? 0 : (right - left) * (bottom - top);
    }

    private static bool HasOnScreenBounds(ElementBounds bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;
        var left = GetSystemMetrics(SmXVirtualScreen);
        var top = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen);
        var height = GetSystemMetrics(SmCyVirtualScreen);
        var right = (long)left + width;
        var bottom = (long)top + height;
        return bounds.X < right && (long)bounds.X + bounds.Width > left &&
               bounds.Y < bottom && (long)bounds.Y + bounds.Height > top;
    }

    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const int SmCxSize = 30;
    private const int SmCySize = 31;
    private const int VkLeftButton = 0x01;
    private const uint GaRoot = 2;
    private const uint WmClose = 0x0010;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;

        public static MonitorInfo Create() => new() { Size = Marshal.SizeOf<MonitorInfo>() };
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint MonitorDefaultToNearest = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint command);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint KeyEventUnicode = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint GwOwner = 4;

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public InputUnion Data;
        public static NativeInput Unicode(char value, bool keyUp) => new() { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Scan = value, Flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0) } } };
    }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint MouseData, Flags, Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey, Scan; public uint Flags, Time; public UIntPtr ExtraInfo; }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll", EntryPoint = "mouse_event")] private static extern void MouseEvent(uint flags, uint x, uint y, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsWindowsMode)
        {
            _viewModel.Log("Windows tree export is the next Windows-mode integration step.");
            return;
        }
        var snapshot = _viewModel.CreateSnapshot();
        if (snapshot is null) { _viewModel.Log("Attach to a Java window before exporting its tree."); return; }
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export accessibility tree", Filter = "JSON files (*.json)|*.json", FileName = $"jab-tree-{DateTime.Now:yyyyMMdd-HHmmss}.json", DefaultExt = ".json" };
        if (dialog.ShowDialog(this) != true) return;
        try { JsonExportService.ExportSnapshot(dialog.FileName, snapshot); _viewModel.Log($"Tree exported to {dialog.FileName}"); }
        catch (Exception ex) { _viewModel.Log($"Export failed: {ex.Message}"); }
    }

    private void Logs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_logScrollScheduled) return;
        _logScrollScheduled = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
        {
            _logScrollScheduled = false;
            if (_viewModel.Logs.Count > 0) LogList.ScrollIntoView(_viewModel.Logs[^1]);
        }));
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        if (LogList.SelectedItems.Count > 0) CopySelectedLogs();
        else CopyAllLogs();
    }

    private void CopySelectedLogs_Click(object sender, RoutedEventArgs e) => CopySelectedLogs();
    private void CopyAllLogs_Click(object sender, RoutedEventArgs e) => CopyAllLogs();
    private void LogList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.C || Keyboard.Modifiers != ModifierKeys.Control) return;
        CopySelectedLogs();
        e.Handled = true;
    }

    private void CopySelectedLogs()
    {
        var lines = LogList.SelectedItems.Cast<string>().ToArray();
        if (lines.Length == 0) { _viewModel.Log("Select one or more console entries to copy."); return; }
        System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, lines));
        _viewModel.Log($"Copied {lines.Length} selected log line(s) to the clipboard.");
    }

    private void CopyAllLogs()
    {
        if (_viewModel.Logs.Count == 0) return;
        System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, _viewModel.Logs));
        _viewModel.Log($"Copied {_viewModel.Logs.Count} log line(s) to the clipboard.");
    }

    private void OpenRecording_Click(object sender, RoutedEventArgs e)
    {
        OpenRecordingStudio();
    }

    private void OpenObjectRepository_Click(object sender, RoutedEventArgs e)
    {
        OpenObjectRepositoryManager();
    }

    private void OpenObjectRepositoryManager()
    {
        if (_objectRepositoryWindow is null || !_objectRepositoryWindow.IsLoaded)
        {
            _objectRepositoryWindow = new ObjectRepositoryWindow(_viewModel, this) { Owner = this };
            _objectRepositoryWindow.Closed += (_, _) => _objectRepositoryWindow = null;
            _objectRepositoryWindow.Show();
        }
        else
        {
            _objectRepositoryWindow.Activate();
        }
    }

    private void OpenRecordingStudio()
    {
        if (_recordingStudioWindow is null || !_recordingStudioWindow.IsLoaded)
        {
            _recordingStudioWindow = new RecordingStudioWindow(_viewModel, this);
            _recordingStudioWindow.Closed += (_, _) => _recordingStudioWindow = null;
            _recordingStudioWindow.Show();
        }
        else
        {
            _recordingStudioWindow.Activate();
        }
    }

    private void RecordingBadgeOverlay_PauseResumeRequested(object? sender, EventArgs e)
    {
        _viewModel.ToggleJavaRecordingPause();
        UpdateRecordingBadge();
    }

    private void RecordingBadgeOverlay_StopRequested(object? sender, EventArgs e)
    {
        _viewModel.StopJavaRecordingSession();
        UpdateRecordingBadge();
        HighlightOverlay.HidePersistent();
        _viewModel.Log("Recording stopped from floating badge controls.");
    }

    private void RecordingBadgeOverlay_StudioRequested(object? sender, EventArgs e)
    {
        OpenRecordingStudio();
    }

    private void StartRecordingSession_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsJavaMode || _viewModel.CurrentWindow is null || _viewModel.Root is null)
        {
            _viewModel.Log("Attach to a Java window before starting a recording session.");
            OpenRecordingStudio();
            return;
        }

        var dialog = new RecordingSessionWindow(_viewModel.CurrentWindow.Title) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (_viewModel.StartJavaRecordingSession(dialog.SessionName, dialog.ApplicationAlias))
        {
            _viewModel.Log($"Started Java recording session '{dialog.SessionName}'.");
            OpenRecordingStudio();
            UpdateRecordingBadge();
            BringCurrentJavaWindowToForeground("start recording");
        }
    }

    public void BringCurrentJavaWindowToForeground(string reason)
    {
        if (_viewModel.CurrentWindow is null)
        {
            _viewModel.Log($"Foreground request skipped for '{reason}' because no Java window is attached.");
            return;
        }

        var hwnd = _viewModel.CurrentWindow.Hwnd;
        if (hwnd == IntPtr.Zero)
        {
            _viewModel.Log($"Foreground request skipped for '{reason}' because the target window handle is zero.");
            return;
        }

        if (IsIconic(hwnd))
        {
            ShowWindow(hwnd, SwRestore);
        }

        SetForegroundWindow(hwnd);
        _viewModel.Log($"Brought Java window '{_viewModel.CurrentWindow.Title}' to foreground for '{reason}'.");
    }

    private void AddSelectedObject_Click(object sender, RoutedEventArgs e)
    {
        var entry = _viewModel.AddSelectedNodeToRepository();
        if (entry is null) _viewModel.Log("Select a Java element before adding it to the repository.");
        else _viewModel.Log($"Captured repository object {entry.ObjectKey} for playback.");
    }

    private void AddPropertySelectionToRepository_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsJavaMode || _viewModel.SelectedNode is null)
        {
            _viewModel.Log("Select a Java element before adding it to an object repository.");
            return;
        }

        var targetDialog = new AddToRepositoryTargetWindow(_viewModel.CurrentRepositorySummary, _viewModel.RepositoryStorageDirectory) { Owner = this };
        if (targetDialog.ShowDialog() != true || targetDialog.SelectedTarget is null) return;

        switch (targetDialog.SelectedTarget.Value)
        {
            case AddToRepositoryTarget.Current:
            {
                var entry = _viewModel.AddSelectedNodeToRepository();
                if (entry is null) return;
                _viewModel.Log($"Added selected element to current object repository as {entry.ObjectKey}.");
                OpenObjectRepositoryManager();
                break;
            }
            case AddToRepositoryTarget.ExistingFile:
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Choose existing object repository",
                    Filter = "Java recording project (*.jrecording.json)|*.jrecording.json|JSON files (*.json)|*.json",
                    CheckFileExists = true,
                    InitialDirectory = _viewModel.RepositoryStorageDirectory
                };
                if (dialog.ShowDialog(this) != true) return;
                var entry = _viewModel.AddSelectedNodeToRepositoryFile(dialog.FileName, createNew: false);
                if (entry is not null) _viewModel.Log($"Added selected element to repository file as {entry.ObjectKey}: {dialog.FileName}");
                break;
            }
            case AddToRepositoryTarget.NewFile:
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Create object repository",
                    Filter = "Java recording project (*.jrecording.json)|*.jrecording.json|JSON files (*.json)|*.json",
                    FileName = _viewModel.GetDefaultRecordingProjectFileName(),
                    DefaultExt = ".jrecording.json",
                    InitialDirectory = _viewModel.RepositoryStorageDirectory
                };
                if (dialog.ShowDialog(this) != true) return;
                var entry = _viewModel.AddSelectedNodeToRepositoryFile(dialog.FileName, createNew: true);
                if (entry is not null) _viewModel.Log($"Created repository file with selected element as {entry.ObjectKey}: {dialog.FileName}");
                break;
            }
        }
    }

    private void ToggleRecordingPause_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleJavaRecordingPause();
        UpdateRecordingBadge();
    }

    private void SaveRecording_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Java recording project",
            Filter = "Java recording project (*.jrecording.json)|*.jrecording.json|JSON files (*.json)|*.json",
            FileName = string.IsNullOrWhiteSpace(_viewModel.RecordingProjectPath) ? $"{_viewModel.RecordingSessionName}.jrecording.json" : Path.GetFileName(_viewModel.RecordingProjectPath),
            DefaultExt = ".jrecording.json",
            InitialDirectory = !string.IsNullOrWhiteSpace(_viewModel.RecordingProjectPath)
                ? (Path.GetDirectoryName(_viewModel.RecordingProjectPath) ?? _viewModel.RepositoryStorageDirectory)
                : _viewModel.RepositoryStorageDirectory
        };
        if (dialog.ShowDialog(this) != true) return;
        if (_viewModel.SaveRecordingProject(dialog.FileName))
        {
            _viewModel.Log($"Saved recording project to {dialog.FileName}.");
        }
    }

    private void LoadRecording_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Load Java recording project",
            Filter = "Java recording project (*.jrecording.json)|*.jrecording.json|JSON files (*.json)|*.json",
            CheckFileExists = true,
            InitialDirectory = _viewModel.RepositoryStorageDirectory
        };
        if (dialog.ShowDialog(this) != true) return;
        _viewModel.LoadRecordingProject(dialog.FileName);
        _viewModel.Log($"Loaded recording project from {dialog.FileName}.");
        OpenRecordingStudio();
        UpdateRecordingBadge();
    }

    private async void PlayRecording_Click(object sender, RoutedEventArgs e)
    {
        await PlayRecordingAsync();
    }

    public async Task PlayRecordingAsync()
    {
        _playbackInProgress = true;
        _viewModel.Log($"Playback requested. StepCount={_viewModel.RecordedSteps.Count}, HasRoot={_viewModel.Root is not null}, HasWindow={_viewModel.CurrentWindow is not null}.");
        try
        {
            if (!_viewModel.IsJavaMode || _viewModel.Root is null || _viewModel.CurrentWindow is null)
            {
                _viewModel.ReportPlayback("Attach to a Java window before playback.");
                _viewModel.Log("Playback rejected because Java mode is inactive, root is null, or no current window is attached.");
                return;
            }
            if (_viewModel.RecordedSteps.Count == 0)
            {
                _viewModel.ReportPlayback("No recorded steps are available to play.");
                _viewModel.Log("Playback rejected because there are no recorded steps.");
                return;
            }

            var orderedSteps = _viewModel.RecordedSteps.OrderBy(x => x.Sequence).ToList();
            var lines = new List<string>();
            var playbackResolutionPolicy = new ResolutionPolicy(
                TimeoutMs: 5000,
                PollIntervalMs: 200,
                RefreshTreeOnFailure: true,
                RequireUnique: true,
                MaxCandidates: 5);
            for (var index = 0; index < orderedSteps.Count; index++)
            {
                var step = orderedSteps[index];
                var nextStep = index + 1 < orderedSteps.Count ? orderedSteps[index + 1] : null;
                if (!_viewModel.TryAutoAttachJavaWindowForRecordedStep(step, out var attachMessage))
                {
                    lines.Add($"Step {step.Sequence}: FAILED - {attachMessage}");
                    _viewModel.ReportPlayback(string.Join(Environment.NewLine, lines));
                    _viewModel.Log($"Playback failed at step {step.Sequence}: {attachMessage}");
                    return;
                }
                UpdateRecordingBadge();

                var message = "";
                if (step.ActionKind != JavaRecordedActionKind.CloseWindow)
                {
                    var node = _viewModel.ResolveRecordedStep(step, out message, playbackResolutionPolicy);
                    if (node is null)
                    {
                        lines.Add($"Step {step.Sequence}: FAILED - {message}");
                        _viewModel.ReportPlayback(string.Join(Environment.NewLine, lines));
                        _viewModel.Log($"Playback failed at step {step.Sequence}: {message}");
                        return;
                    }

                    _viewModel.SelectedNode = node;
                    SelectNodeInHierarchy(node);
                    ActivateHierarchyNode(node);
                    await Task.Delay(120);
                }
                else
                {
                    message = $"Window-level action routed to '{step.WindowTitle}'.";
                }

                var success = ExecutePlaybackAction(step, nextStep);
                var outcome = success ? "OK" : "FAILED";
                lines.Add($"Step {step.Sequence}: {outcome} - {step.ActionKind} -> {step.ObjectKey}");
                _viewModel.ReportPlayback(string.Join(Environment.NewLine, lines));
                _viewModel.Log($"Playback step {step.Sequence} {outcome}: {message}");
                if (!success) return;

                if (_viewModel.DoesStepRequireWindowTransition(step, nextStep))
                {
                    _viewModel.Log($"Playback expects a modal/window transition after step {step.Sequence} to '{nextStep!.WindowTitle}'. Waiting for next window.");
                    if (!_viewModel.WaitForRecordedStepWindow(nextStep!, 1800, 220, out var waitMessage))
                    {
                        _viewModel.Log($"Expected next modal/window did not appear after step {step.Sequence}. Retrying the triggering action once.");
                        var retrySuccess = ExecutePlaybackAction(step, nextStep);
                        _viewModel.Log($"Playback retry for step {step.Sequence} completed. Success={retrySuccess}.");
                        if (!retrySuccess || !_viewModel.WaitForRecordedStepWindow(nextStep!, 2500, 250, out waitMessage))
                        {
                            lines.Add($"Step {step.Sequence}: FAILED - {waitMessage}");
                            _viewModel.ReportPlayback(string.Join(Environment.NewLine, lines));
                            _viewModel.Log($"Playback failed waiting for next modal/window after step {step.Sequence}: {waitMessage}");
                            return;
                        }
                    }

                    UpdateRecordingBadge();
                }

                await Task.Delay(160);
            }

            _viewModel.Log("Playback completed successfully.");
        }
        finally
        {
            _playbackInProgress = false;
        }
    }

    public bool ExecuteJavaRecordedActionFromStudio(JavaRecordedActionKind actionKind, string inputText)
    {
        _viewModel.Log($"Recorder studio action requested. Action={actionKind}, InputLength={inputText.Length}.");
        return ExecuteJavaRecordedAction(actionKind, inputText, captureStep: true);
    }

    public void HighlightCurrentJavaSelection()
    {
        if (_viewModel.SelectedNode is null) return;
        if (!TryGetHighlightBounds(_viewModel.SelectedNode, out _, out var bounds)) return;
        HighlightOverlay.Show(bounds, TimeSpan.FromSeconds(2.2));
    }

    public void HighlightRepositorySelection()
    {
        var node = _viewModel.ResolveSelectedRepositoryEntry(out var message);
        if (node is null)
        {
            _viewModel.Log($"Repository highlight failed: {message}");
            return;
        }

        _viewModel.SelectedNode = node;
        SelectNodeInHierarchy(node);
        ActivateHierarchyNode(node);
        DetailsTabs.SelectedItem = PropertiesTab;
        _viewModel.Log($"Repository highlight resolved and selected {node.DisplayName}. {message}");
    }

    public void HighlightRecordedStep(JavaRecordedStep step)
    {
        if (!_viewModel.TryAutoAttachJavaWindowForRecordedStep(step, out var attachMessage))
        {
            _viewModel.Log($"Recorded-step highlight failed: {attachMessage}");
            return;
        }

        if (step.ActionKind == JavaRecordedActionKind.CloseWindow)
        {
            if (_viewModel.CurrentWindow is not null && TryGetNativeCloseButtonBounds(_viewModel.CurrentWindow, out var closeBounds))
            {
                HighlightOverlay.Show(closeBounds, TimeSpan.FromSeconds(2.2));
                _viewModel.Log($"Recorded-step highlight resolved native close chrome for step {step.Sequence} on window '{_viewModel.CurrentWindow.Title}'.");
                return;
            }

            _viewModel.Log($"Recorded-step highlight failed to resolve native close chrome for step {step.Sequence}. {attachMessage}");
            return;
        }

        var node = _viewModel.ResolveRecordedStep(step, out var resolveMessage);
        if (node is null)
        {
            _viewModel.Log($"Recorded-step highlight failed for step {step.Sequence}: {resolveMessage}");
            return;
        }

        _viewModel.SelectedNode = node;
        SelectNodeInHierarchy(node);
        ActivateHierarchyNode(node);
        DetailsTabs.SelectedItem = PropertiesTab;
        _viewModel.Log($"Recorded-step highlight resolved {node.DisplayName} for step {step.Sequence}. {resolveMessage}");
    }

    public void UpdateRecordingBadge()
    {
        if (!_viewModel.IsRecordingActive)
        {
            _recordingBadgeOverlay?.Detach();
            _recordingBadgeTargetHwnd = IntPtr.Zero;
            _viewModel.Log("Recording badge hidden because recording is inactive.");
            return;
        }

        var targetHwnd = _viewModel.CurrentWindow?.Hwnd ?? _recordingBadgeTargetHwnd;
        if (targetHwnd == IntPtr.Zero)
        {
            _viewModel.Log("Recording badge update skipped because no Java window handle is available yet.");
            return;
        }

        _recordingBadgeTargetHwnd = targetHwnd;
        if (_recordingBadgeOverlay is null || !_recordingBadgeOverlay.IsLoaded)
        {
            _recordingBadgeOverlay = new RecordingBadgeOverlay();
            _recordingBadgeOverlay.PauseResumeRequested += RecordingBadgeOverlay_PauseResumeRequested;
            _recordingBadgeOverlay.StopRequested += RecordingBadgeOverlay_StopRequested;
            _recordingBadgeOverlay.StudioRequested += RecordingBadgeOverlay_StudioRequested;
            _recordingBadgeOverlay.ActionRequested += RecordingBadgeOverlay_ActionRequested;
        }
        _recordingBadgeOverlay.UpdateBadgeText(_viewModel.RecordingBadgeText);
        _recordingBadgeOverlay.AttachToTarget(targetHwnd, _viewModel.RecordingBadgeText);
        var hwndDisplay = _viewModel.CurrentWindow?.HwndDisplay ?? $"0x{targetHwnd.ToInt64():X}";
        _viewModel.Log($"Recording badge updated. BadgeText='{_viewModel.RecordingBadgeText}', Hwnd={hwndDisplay}.");
    }

    private void RecordingBadgeOverlay_ActionRequested(object? sender, RecordingBadgeActionRequestedEventArgs e)
    {
        if (!_viewModel.IsRecordingActive)
        {
            _viewModel.Log("Recording badge action ignored because recording is not active.");
            return;
        }

        if (_viewModel.SelectedNode is null)
        {
            _viewModel.ReportAutomation("Select or capture a Java element first, then use recorder actions.");
            _viewModel.Log($"Recording badge action '{e.ActionKind}' ignored because no Java element is selected.");
            return;
        }

        string input = "";
        if (e.ActionKind is JavaRecordedActionKind.SetText or JavaRecordedActionKind.TypeText)
        {
            var title = e.ActionKind == JavaRecordedActionKind.SetText ? "Record Set Text" : "Record Type Text";
            var description = e.ActionKind == JavaRecordedActionKind.SetText
                ? "Enter the text value to set directly on the selected Java element."
                : "Enter the text to type using the recorder typing strategy.";
            var inputWindow = new RecordingActionInputWindow(title, description) { Owner = this };
            if (inputWindow.ShowDialog() != true)
            {
                _viewModel.Log($"Recording badge action '{e.ActionKind}' canceled by user.");
                return;
            }

            input = inputWindow.EnteredText;
        }

        _viewModel.Log($"Recording badge action requested. Action={e.ActionKind}, InputLength={input.Length}, SelectedNode='{_viewModel.SelectedNode.DisplayName}'.");
        ExecuteJavaRecordedActionFromStudio(e.ActionKind, input);
        if (_viewModel.IsRecordingActive) UpdateRecordingBadge();
    }

    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);
}

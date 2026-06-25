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

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly HighlightManager _highlightManager = new();
    private readonly System.Windows.Threading.DispatcherTimer _hoverTimer;
    private readonly System.Windows.Threading.DispatcherTimer _recordingMonitorTimer;
    private RecordingStudioWindow? _recordingStudioWindow;
    private RecordingBadgeOverlay? _recordingBadgeOverlay;
    private bool _hoverInspecting;
    private bool _pickerActive;
    private readonly System.Windows.Threading.DispatcherTimer _pickerTimer;
    private bool _recordingLeftButtonDown;
    private bool _recordingCaptureInProgress;
    private NativePoint _recordingMouseDownPoint;
    private DateTime _lastPassiveClickAtUtc;
    private string _lastPassiveClickPath = "";
    private bool _playbackInProgress;
    private IntPtr _lastAutoAttachProbeHwnd;
    private DateTime _lastAutoAttachProbeAtUtc;
    private bool _autoAttachInProgress;
    private IntPtr _autoAttachInProgressHwnd;
    private DateTime _lastAutoAttachLogAtUtc;
    private NativePoint? _lastHoverPoint;
    private AccessibleNode? _lastHierarchyNode;
    private AccessibleNode? _hoverSelectingNode;
    private AccessibleNode? _lastActivatedNode;
    private DateTime _lastActivationAt;
    private bool _startupPositioned;
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
            _highlightManager.ClearAll();
            _recordingBadgeOverlay?.Detach();
            _recordingStudioWindow?.Close();
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
            _highlightManager.Flash(physicalBounds, HighlightMode.HierarchySelectionFlash);
            var fallback = ReferenceEquals(node, visualNode) ? "" : $" using nearest on-screen ancestor {visualNode.DisplayName}";
            _viewModel.Log($"Highlighted {node.DisplayName}{fallback} at physical bounds ({physicalBounds.X}, {physicalBounds.Y}, {physicalBounds.Width}, {physicalBounds.Height}).");
            return;
        }

        var javaNode = _viewModel.SelectedNode;
        if (javaNode is null) { _viewModel.Log("Select an element before highlighting."); return; }
        if (!TryGetHighlightBounds(javaNode, out var visualNodeJava, out var physicalBoundsJava))
        { _viewModel.Log("The selected element and its ancestors have no on-screen bounds and cannot be highlighted."); return; }
        _highlightManager.Flash(physicalBoundsJava, HighlightMode.HierarchySelectionFlash);
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
            _highlightManager.Flash(bounds, HighlightMode.HierarchySelectionFlash);
            var fallback = ReferenceEquals(node, visualNode) ? "" : $" using nearest on-screen ancestor {visualNode.DisplayName}";
            _viewModel.Log($"Highlighted {node.DisplayName}{fallback}.");
        }
        else _viewModel.Log($"Hierarchy click selected {node.DisplayName}, but no on-screen bounds were available.");
    }

    private void ActivateHierarchyNode(WindowsAutomationNode node)
    {
        if (TryGetHighlightBounds(node, out var visualNode, out var bounds))
        {
            _highlightManager.Flash(bounds, HighlightMode.HierarchySelectionFlash);
            var fallback = ReferenceEquals(node, visualNode) ? "" : $" using nearest on-screen ancestor {visualNode.DisplayName}";
            _viewModel.Log($"Highlighted {node.DisplayName}{fallback}.");
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
        var result = _viewModel.ResolveJavaNodeBounds(node, GetPhysicalBounds, "[HIGHLIGHT]");
        if (result is not null && HasOnScreenBounds(result.PhysicalBounds))
        {
            visualNode = result.VisibleAncestor;
            bounds = result.PhysicalBounds;
            return true;
        }

        visualNode = node;
        bounds = new ElementBounds(0, 0, 0, 0);
        return false;
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
            _hoverTimer.Stop(); _highlightManager.ClearPersistent();
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
        if (!TryResolveJavaNodeAtScreenPoint(point, out var node, out var bounds) || node is null)
        {
            _highlightManager.ClearPersistent();
            return;
        }

        _viewModel.SelectedNode = node;
        if (!ReferenceEquals(_lastHierarchyNode, node))
        {
            _hoverSelectingNode = node;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => SelectNodeInHierarchy(node)));
            _lastHierarchyNode = node;
        }
        DetailsTabs.SelectedItem = PropertiesTab;
        if (HasOnScreenBounds(bounds)) _highlightManager.ShowPersistent(bounds, HighlightMode.TransientHover); else _highlightManager.ClearPersistent();
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
        _viewModel.Log("Picker active.");
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
            _viewModel.Log("Picker complete.");
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
            _highlightManager.ClearPersistent();
            return;
        }

        if (!TryResolveJavaNodeAtScreenPoint(point, out var node, out var bounds) || node is null)
        {
            _highlightManager.ClearPersistent();
            return;
        }

        _viewModel.SelectedNode = node;
        if (!ReferenceEquals(_lastHierarchyNode, node))
        {
            _hoverSelectingNode = node;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => SelectNodeInHierarchy(node)));
            _lastHierarchyNode = node;
        }

        DetailsTabs.SelectedItem = PropertiesTab;

        if (HasOnScreenBounds(bounds))
            _highlightManager.ShowPersistent(bounds, HighlightMode.TransientHover);
        else
            _highlightManager.ClearPersistent();
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
            return;
        }

        if (!leftDown && _recordingLeftButtonDown)
        {
            _recordingLeftButtonDown = false;
            if (_recordingCaptureInProgress)
            {
                return;
            }
            if (!GetCursorPos(out var releasePoint)) return;
            var deltaX = Math.Abs(releasePoint.X - _recordingMouseDownPoint.X);
            var deltaY = Math.Abs(releasePoint.Y - _recordingMouseDownPoint.Y);
            if (deltaX > 8 || deltaY > 8)
            {
                return;
            }
            _recordingCaptureInProgress = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                try { TryRecordPassiveClick(releasePoint); }
                finally
                {
                    _recordingCaptureInProgress = false;
                }
            }));
        }
    }

    private static bool Contains(ElementBounds bounds, NativePoint point) =>
        bounds.Width > 0 && bounds.Height > 0 && point.X >= bounds.X && point.X < (long)bounds.X + bounds.Width &&
        point.Y >= bounds.Y && point.Y < (long)bounds.Y + bounds.Height;

    private void TryRecordPassiveClick(NativePoint point)
    {
        if (_recordingBadgeOverlay?.ContainsScreenPoint(point.X, point.Y) == true)
        {
            return;
        }

        if (_viewModel.IsRecordingPaused)
        {
            return;
        }

        TryAutoAttachJavaWindowFromPoint(point, "passive click");
        if (_viewModel.CurrentWindow is null || _viewModel.Root is null)
        {
            _viewModel.Log("[RECORDER] Capture aborted because no Java window/root is attached after auto-attach probe.");
            return;
        }

        if (!IsPointWithinCurrentJavaWindow(point))
        {
            return;
        }

        if (!TryResolveJavaNodeAtScreenPoint(point, out var node, out var bounds) || node is null)
        {
            _viewModel.Log($"Passive recording could not resolve a Java node at ({point.X}, {point.Y}) using the hover hit-test path.");
            return;
        }

        _viewModel.SelectedNode = node;
        _hoverSelectingNode = node;
        _lastHierarchyNode = node;
        _highlightManager.Flash(bounds, HighlightMode.RecorderActionFlash);
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            SelectNodeInHierarchy(node);
        }));

        var isDoubleClick = !string.IsNullOrWhiteSpace(node.Path)
                            && string.Equals(_lastPassiveClickPath, node.Path, StringComparison.OrdinalIgnoreCase)
                            && DateTime.UtcNow - _lastPassiveClickAtUtc <= TimeSpan.FromMilliseconds(GetDoubleClickTime());

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
        }
        else
        {
            _viewModel.Log($"Passive recording did not create a step for '{node.DisplayName}'.");
        }
    }

    private bool TryAutoAttachJavaWindowFromPoint(NativePoint point, string reason)
    {
        if (IsPointInsideCurrentJavaWindowRect(point))
        {
            return true;
        }

        var candidateWindows = GetCandidateWindowHandlesFromPoint(point);
        if (candidateWindows.Count == 0) return false;
        if (_viewModel.CurrentWindow is not null && candidateWindows.Contains(_viewModel.CurrentWindow.Hwnd))
        {
            return true;
        }
        var now = DateTime.UtcNow;
        var probeKey = candidateWindows[0];
        if (probeKey == _lastAutoAttachProbeHwnd && now - _lastAutoAttachProbeAtUtc < TimeSpan.FromMilliseconds(850))
        {
            return false;
        }
        _lastAutoAttachProbeHwnd = probeKey;
        _lastAutoAttachProbeAtUtc = now;

        foreach (var candidate in candidateWindows)
        {
            QueueAutoAttachJavaWindow(candidate, reason, candidateWindows);
            return true;
        }

        return false;
    }

    private void QueueAutoAttachJavaWindow(IntPtr hwnd, string reason, IReadOnlyList<IntPtr> candidates)
    {
        if (hwnd == IntPtr.Zero) return;
        if (_autoAttachInProgress)
        {
            return;
        }

        _autoAttachInProgress = true;
        _autoAttachInProgressHwnd = hwnd;
        _lastAutoAttachLogAtUtc = DateTime.UtcNow;
        _viewModel.Log($"Auto-attaching Java modal/window for {reason}...");

        _ = AttachJavaWindowFromPointAsync(hwnd, reason);
    }

    private async Task AttachJavaWindowFromPointAsync(IntPtr hwnd, string reason)
    {
        try
        {
            var attached = await _viewModel.TryAutoAttachJavaWindowAsync(hwnd, reason);
            if (attached) UpdateRecordingBadge();
        }
        catch (Exception ex)
        {
            _viewModel.Log($"[AUTO-ATTACH] Async attach failed for hwnd 0x{hwnd.ToInt64():X}: {ex.Message}");
        }
        finally
        {
            _autoAttachInProgress = false;
            _autoAttachInProgressHwnd = IntPtr.Zero;
        }
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
        return point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom;
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
        var corePoint = new JabInspector.Core.Models.NativePoint(point.X, point.Y);
        var result = _viewModel.InspectJavaAtScreenPoint(
            corePoint,
            _viewModel.InspectAt,
            GetPhysicalBounds,
            (candidateBounds, candidatePoint) => Contains(candidateBounds, new NativePoint { X = candidatePoint.X, Y = candidatePoint.Y }),
            "[INSPECT]");

        if (result is null || !HasOnScreenBounds(result.PhysicalBounds)) return false;
        node = result.ResolvedNode;
        bounds = result.PhysicalBounds;
        return true;
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
        _viewModel.Log($"Java action requested: {actionKind}.");
        if (_viewModel.SelectedNode is null)
        {
            _viewModel.ReportAutomation("Select a Java element first.");
            _viewModel.Log("ExecuteJavaRecordedAction aborted because no Java node is selected.");
            return false;
        }

        HighlightCurrentJavaSelection();

        var success = actionKind switch
        {
            JavaRecordedActionKind.Focus => _viewModel.FocusSelected(),
            JavaRecordedActionKind.Click => _viewModel.InvokeDefaultAction() || PhysicalClickForResult(_viewModel.SelectedNode, 1),
            JavaRecordedActionKind.DoubleClick => PhysicalClickForResult(_viewModel.SelectedNode, 2),
            JavaRecordedActionKind.SetText => _viewModel.SetSelectedText(inputText),
            JavaRecordedActionKind.TypeText => TypeJavaText(inputText),
            JavaRecordedActionKind.GetText => !string.IsNullOrWhiteSpace(_viewModel.GetSelectedText()),
            _ => false
        };

        if (captureStep && success && !_viewModel.IsRecordingPaused)
        {
            var recordedStep = _viewModel.RecordJavaAction(actionKind, inputText);
            if (recordedStep is not null) _viewModel.Log($"Recorded step {recordedStep.Sequence}: {actionKind}.");
        }
        else if (captureStep && success && _viewModel.IsRecordingPaused)
        {
            _viewModel.Log($"Action {actionKind} succeeded; recording is paused so no step was captured.");
        }
        if (_viewModel.IsRecordingActive) UpdateRecordingBadge();
        return success;
    }

    private bool ExecutePlaybackAction(JavaRecordedStep step, JavaRecordedStep? nextStep)
    {
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

    private bool TypeJavaText(string text)
    {
        if (_viewModel.SelectedNode is null)
        {
            _viewModel.ReportAutomation("Select an element first.");
            return false;
        }
        _viewModel.FocusSelected();
        if (_viewModel.CurrentWindow is not null) SetForegroundWindow(_viewModel.CurrentWindow.Hwnd);
        Thread.Sleep(100);
        var sent = SendUnicodeText(text);
        _viewModel.ReportAutomation($"Typed {sent} of {text.Length} Unicode character(s). Text was inserted at the control's current caret position.");
        _viewModel.Log($"Typed {sent} character(s) into {_viewModel.SelectedNode.DisplayName}.");
        return sent > 0 || text.Length == 0;
    }

    private void PhysicalClick(AccessibleNode node, int count)
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
        if (!HasOnScreenBounds(bounds)) { _viewModel.ReportAutomation("The selected element has no usable on-screen bounds."); return; }
        if (_viewModel.CurrentWindow is not null) SetForegroundWindow(_viewModel.CurrentWindow.Hwnd);
        PhysicalClickAtPoint(
            new NativePoint { X = bounds.X + bounds.Width / 2, Y = bounds.Y + bounds.Height / 2 },
            count,
            node.DisplayName,
            "physical input");
    }

    private bool PhysicalClickForResult(AccessibleNode node, int count)
    {
        PhysicalClick(node, count);
        return true;
    }

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
    private const int VkLeftButton = 0x01;
    private const uint GaRoot = 2;

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
    private const int SwRestore = 9;

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
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll", EntryPoint = "mouse_event")] private static extern void MouseEvent(uint flags, uint x, uint y, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);

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
    { if (_viewModel.Logs.Count > 0) LogList.ScrollIntoView(_viewModel.Logs[^1]); }

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

    private async void StartRecordingSession_Click(object sender, RoutedEventArgs e)
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
            _recordingStudioWindow?.Close();
            await ShowFloatingRecorderForCurrentJavaWindowAsync();
        }
    }

    private void AddSelectedObject_Click(object sender, RoutedEventArgs e)
    {
        var entry = _viewModel.AddSelectedNodeToRepository();
        if (entry is null) _viewModel.Log("Select a Java element before adding it to the repository.");
        else _viewModel.Log($"Captured repository object {entry.ObjectKey} for playback.");
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
            DefaultExt = ".jrecording.json"
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
            CheckFileExists = true
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

                var node = _viewModel.ResolveRecordedStep(step, out var message);
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
        _viewModel.Log($"Recorder action requested: {actionKind}.");
        return ExecuteJavaRecordedAction(actionKind, inputText, captureStep: true);
    }

    public void HighlightCurrentJavaSelection()
    {
        if (_viewModel.SelectedNode is null) return;
        if (!TryGetHighlightBounds(_viewModel.SelectedNode, out _, out var bounds)) return;
        _highlightManager.Flash(bounds, HighlightMode.HierarchySelectionFlash, TimeSpan.FromSeconds(2.2));
    }

    public void ClearHighlights() => _highlightManager.ClearAll();

    public void UpdateRecordingBadge()
    {
        if (!_viewModel.IsRecordingActive)
        {
            _recordingBadgeOverlay?.Detach();
            return;
        }

        if (_recordingBadgeOverlay is null)
        {
            _recordingBadgeOverlay = new RecordingBadgeOverlay();
            _recordingBadgeOverlay.PauseResumeRequested += (_, _) =>
            {
                _viewModel.ToggleJavaRecordingPause();
                UpdateRecordingBadge();
            };
            _recordingBadgeOverlay.StopRequested += (_, _) => StopRecordingFromFloatingPanel();
            _recordingBadgeOverlay.StudioRequested += (_, _) => OpenRecordingStudio();
        }
        _recordingBadgeOverlay.UpdateBadgeText(_viewModel.RecordingBadgeText);
        _recordingBadgeOverlay.AttachToTarget(_viewModel.CurrentWindow?.Hwnd ?? IntPtr.Zero, _viewModel.RecordingBadgeText);
    }

    public async Task ShowFloatingRecorderForCurrentJavaWindowAsync()
    {
        if (!_viewModel.IsRecordingActive || _viewModel.CurrentWindow is null)
        {
            UpdateRecordingBadge();
            return;
        }

        BringJavaTargetToForeground();
        await Task.Delay(180);
        UpdateRecordingBadge();
        HighlightCurrentJavaSelectionOrRoot();
        _viewModel.Log("Floating recorder panel opened on the selected Java application.");
    }

    private void BringJavaTargetToForeground()
    {
        if (_viewModel.CurrentWindow is null) return;
        ShowWindow(_viewModel.CurrentWindow.Hwnd, SwRestore);
        SetForegroundWindow(_viewModel.CurrentWindow.Hwnd);
    }

    private void HighlightCurrentJavaSelectionOrRoot()
    {
        if (_viewModel.SelectedNode is not null)
        {
            if (TryGetHighlightBounds(_viewModel.SelectedNode, out _, out var selectedBounds))
            {
            _highlightManager.ShowPersistent(selectedBounds, HighlightMode.Persistent);
            }
            return;
        }

        if (_viewModel.Root is null) return;
        if (!TryGetHighlightBounds(_viewModel.Root, out _, out var bounds)) return;
        _highlightManager.ShowPersistent(bounds);
    }

    private void StopRecordingFromFloatingPanel()
    {
        _viewModel.StopJavaRecordingSession();
        _recordingBadgeOverlay?.Detach();
        _highlightManager.ClearPersistent();
        Show();
        Activate();
        OpenRecordingStudio();
    }
}

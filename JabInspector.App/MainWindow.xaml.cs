using System.Collections.Specialized;
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
    private readonly System.Windows.Threading.DispatcherTimer _hoverTimer;
    private bool _hoverInspecting;
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
        _viewModel.Logs.CollectionChanged += Logs_CollectionChanged;
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += (_, _) => { _hoverTimer.Stop(); HighlightOverlay.HidePersistent(); _viewModel.Dispose(); };
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        PositionStartupWindow();
    }

    private void PositionStartupWindow()
    {
        if (_startupPositioned) return;
        _startupPositioned = true;

        Width = Math.Clamp(Width, MinWidth, 1360);
        Height = Math.Clamp(Height, MinHeight, 860);
        UpdateLayout();

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
        if (_viewModel.CurrentWindow is not null) SetForegroundWindow(_viewModel.CurrentWindow.Hwnd);
        var focused = _viewModel.FocusSelected();
        if (TryGetHighlightBounds(node, out var visualNode, out var bounds))
        {
            HighlightOverlay.Show(bounds);
            var fallback = ReferenceEquals(node, visualNode) ? "" : $" using nearest on-screen ancestor {visualNode.DisplayName}";
            _viewModel.Log($"Hierarchy click {(focused ? "focused" : "selected")} and highlighted {node.DisplayName}{fallback}.");
        }
        else _viewModel.Log($"Hierarchy click selected {node.DisplayName}, but no on-screen bounds were available.");
    }

    private void ActivateHierarchyNode(WindowsAutomationNode node)
    {
        if (_viewModel.SelectedWindowsWindow?.Model is { } selectedWindow) SetForegroundWindow(selectedWindow.Hwnd);
        else if (node.NativeHandle != IntPtr.Zero) SetForegroundWindow(node.NativeHandle);

        var focused = _viewModel.FocusSelected();

        if (TryGetHighlightBounds(node, out var visualNode, out var bounds))
        {
            HighlightOverlay.Show(bounds);
            var fallback = ReferenceEquals(node, visualNode) ? "" : $" using nearest on-screen ancestor {visualNode.DisplayName}";
            _viewModel.Log($"Hierarchy click {(focused ? "focused" : "selected")} and highlighted {node.DisplayName}{fallback} through {node.BackendKind}.");
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
        HoverInspectButton.Content = _hoverInspecting ? "●  Hover: ON" : "◎  Hover inspect";
        HoverInspectButton.Background = _hoverInspecting ? (System.Windows.Media.Brush)FindResource("AccentBrush") : System.Windows.Media.Brushes.White;
        HoverInspectButton.Foreground = _hoverInspecting ? System.Windows.Media.Brushes.White : (System.Windows.Media.Brush)FindResource("InkBrush");
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
        if (!_hoverInspecting || _viewModel.Root is null || _viewModel.CurrentWindow is null) return;
        if (!GetCursorPos(out var point)) return;
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

    private static bool Contains(ElementBounds bounds, NativePoint point) =>
        bounds.Width > 0 && bounds.Height > 0 && point.X >= bounds.X && point.X < (long)bounds.X + bounds.Width &&
        point.Y >= bounds.Y && point.Y < (long)bounds.Y + bounds.Height;

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

    private void FocusAction_Click(object sender, RoutedEventArgs e) => _viewModel.FocusSelected();

    private void ClickAction_Click(object sender, RoutedEventArgs e)
    {
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

    private void SetTextAction_Click(object sender, RoutedEventArgs e) => _viewModel.SetSelectedText(AutomationInput.Text);

    private void TypeTextAction_Click(object sender, RoutedEventArgs e)
    {
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

    private void GetTextAction_Click(object sender, RoutedEventArgs e) => _viewModel.GetSelectedText();

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
        _viewModel.ReportAutomation($"{action} {node.DisplayName} at physical point ({bounds.X + bounds.Width / 2}, {bounds.Y + bounds.Height / 2}).");
        _viewModel.Log($"{action} {node.DisplayName} using physical input.");
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
}

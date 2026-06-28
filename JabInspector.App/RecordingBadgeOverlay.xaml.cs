using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using JabInspector.Core.Diagnostics;
using JabInspector.Core.Models;

namespace JabInspector.App;

public partial class RecordingBadgeOverlay : Window
{
    private readonly DispatcherTimer _timer;
    private readonly InspectorLogger _logger = new();
    private IntPtr _targetHwnd;
    private NativeRect? _lastKnownTargetRect;
    private int _targetRectMissCount;
    private bool _isPaused;
    private bool _hasManualPosition;
    private int _manualX;
    private int _manualY;

    public event EventHandler? PauseResumeRequested;
    public event EventHandler? StopRequested;
    public event EventHandler? StudioRequested;
    public event EventHandler<RecordingBadgeActionRequestedEventArgs>? ActionRequested;

    public RecordingBadgeOverlay()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _timer.Tick += (_, _) => UpdatePosition();
        Loaded += (_, _) =>
        {
            ApplyToolWindowStyle();
            UpdatePosition();
        };
    }

    public void AttachToTarget(IntPtr hwnd, string text)
    {
        _logger.Debug($"[RECORDER-OVERLAY] Attach requested. TargetHwnd=0x{hwnd.ToInt64():X}, Text='{text}', IsVisible={IsVisible}.");
        _targetHwnd = hwnd;
        UpdateBadgeText(text);
        if (!IsVisible) Show();
        ApplyToolWindowStyle();
        UpdatePosition();
        _timer.Start();
    }

    public void UpdateBadgeText(string text)
    {
        BadgeTextBlock.Text = ExtractStepCount(text);
        SetPaused(text.Contains("PAUSED", StringComparison.OrdinalIgnoreCase));
        _logger.Debug($"[RECORDER-OVERLAY] Badge text updated. Raw='{text}', Display='{BadgeTextBlock.Text}'.");
    }

    public void Detach()
    {
        _logger.Debug($"[RECORDER-OVERLAY] Detach requested. PreviousTarget=0x{_targetHwnd.ToInt64():X}, IsVisible={IsVisible}.");
        _timer.Stop();
        Hide();
        _targetHwnd = IntPtr.Zero;
        _lastKnownTargetRect = null;
        _targetRectMissCount = 0;
        ActionsPopup.IsOpen = false;
    }

    private void UpdatePosition()
    {
        if (_targetHwnd == IntPtr.Zero || !GetWindowRect(_targetHwnd, out var rect) || !IsUsableRect(rect))
        {
            _targetRectMissCount++;
            if (_targetRectMissCount < 8 && _lastKnownTargetRect is { } lastRect)
            {
                rect = lastRect;
                _logger.Debug($"[RECORDER-OVERLAY] Reusing last target rect after transient miss {_targetRectMissCount}. TargetHwnd=0x{_targetHwnd.ToInt64():X}.");
            }
            else
            {
                rect = GetPrimaryFallbackRect();
                _logger.Debug($"[RECORDER-OVERLAY] Using fallback screen rect after {_targetRectMissCount} target misses. TargetHwnd=0x{_targetHwnd.ToInt64():X}.");
            }
        }
        else
        {
            _lastKnownTargetRect = rect;
            _targetRectMissCount = 0;
        }

        if (!IsVisible)
        {
            _logger.Debug("[RECORDER-OVERLAY] Position skipped because overlay is not visible yet.");
            return;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            _logger.Debug("[RECORDER-OVERLAY] Position skipped because overlay hwnd is zero.");
            return;
        }

        ApplyResponsiveSize(rect);
        var width = (int)Math.Round(Width);
        var height = (int)Math.Round(Height);
        var x = _hasManualPosition ? _manualX : rect.Left + Math.Max(10, ((rect.Right - rect.Left) - width) / 2);
        var y = _hasManualPosition ? _manualY : rect.Top + 10;
        ClampToVirtualScreen(ref x, ref y, width, height);
        if (_hasManualPosition)
        {
            _manualX = x;
            _manualY = y;
        }
        SetWindowPos(hwnd, HwndTopmost, x, y, width, height, SwpNoActivate | SwpShowWindow);
        _logger.Debug($"[RECORDER-OVERLAY] Positioned overlay. OverlayHwnd=0x{hwnd.ToInt64():X}, TargetRect=({rect.Left},{rect.Top},{rect.Right},{rect.Bottom}), OverlayRect=({x},{y},{(int)Math.Round(Width)},{(int)Math.Round(Height)}).");
    }

    private void BadgeSurface_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<System.Windows.Controls.Button>(source) is not null)
        {
            return;
        }

        try
        {
            DragMove();
            _hasManualPosition = true;
            _manualX = (int)Math.Round(Left);
            _manualY = (int)Math.Round(Top);
            _logger.Debug($"[RECORDER-OVERLAY] Manual drag applied. OverlayRect=({_manualX},{_manualY},{(int)Math.Round(Width)},{(int)Math.Round(Height)}).");
            e.Handled = true;
        }
        catch
        {
            // DragMove can throw if the mouse capture changes mid-drag.
        }
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var current = source; current is not null;)
        {
            if (current is T match) return match;
            current = current switch
            {
                Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(current),
                FrameworkContentElement content => content.Parent,
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return null;
    }

    private void ApplyToolWindowStyle()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            _logger.Debug("[RECORDER-OVERLAY] Click-through style skipped because overlay hwnd is zero.");
            return;
        }

        var style = GetWindowLongPtr(hwnd, GwlExStyle);
        var desiredValue = (style.ToInt64() | WsExToolWindow.ToInt64()) & ~WsExTransparent.ToInt64() & ~WsExNoActivate.ToInt64();
        var desired = new IntPtr(desiredValue);
        if (desired != style) SetWindowLongPtr(hwnd, GwlExStyle, desired);
        _logger.Debug($"[RECORDER-OVERLAY] Tool window style applied. OverlayHwnd=0x{hwnd.ToInt64():X}, OldStyle=0x{style.ToInt64():X}, NewStyle=0x{desired.ToInt64():X}.");
    }

    private void SetPaused(bool paused)
    {
        _isPaused = paused;
        LiveText.Text = paused ? "PAUSED" : "RECORDING";
        PauseLeftBar.Visibility = paused ? Visibility.Collapsed : Visibility.Visible;
        PauseRightBar.Visibility = paused ? Visibility.Collapsed : Visibility.Visible;
        ResumeTriangle.Visibility = paused ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyResponsiveSize(NativeRect targetRect)
    {
        if (_hasManualPosition) return;

        var targetWidth = Math.Max(180, targetRect.Right - targetRect.Left);
        var desiredWidth = Math.Clamp((int)Math.Round(targetWidth * 0.42), 280, 360);
        if (Math.Abs(Width - desiredWidth) > 0.5)
        {
            Width = desiredWidth;
        }
    }

    private void ActionsButton_Click(object sender, RoutedEventArgs e)
    {
        ActionsPopup.IsOpen = !ActionsPopup.IsOpen;
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e) => PauseResumeRequested?.Invoke(this, EventArgs.Empty);
    private void StopButton_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);
    private void StudioButton_Click(object sender, RoutedEventArgs e) => StudioRequested?.Invoke(this, EventArgs.Empty);
    private void ActionFocus_Click(object sender, RoutedEventArgs e) => RaiseAction(JavaRecordedActionKind.Focus);
    private void ActionClick_Click(object sender, RoutedEventArgs e) => RaiseAction(JavaRecordedActionKind.Click);
    private void ActionDoubleClick_Click(object sender, RoutedEventArgs e) => RaiseAction(JavaRecordedActionKind.DoubleClick);
    private void ActionSetText_Click(object sender, RoutedEventArgs e) => RaiseAction(JavaRecordedActionKind.SetText);
    private void ActionTypeText_Click(object sender, RoutedEventArgs e) => RaiseAction(JavaRecordedActionKind.TypeText);
    private void ActionGetText_Click(object sender, RoutedEventArgs e) => RaiseAction(JavaRecordedActionKind.GetText);
    private void ActionAssertVisible_Click(object sender, RoutedEventArgs e) => RaiseAction(JavaRecordedActionKind.AssertVisible);

    private void RaiseAction(JavaRecordedActionKind actionKind)
    {
        ActionsPopup.IsOpen = false;
        ActionRequested?.Invoke(this, new RecordingBadgeActionRequestedEventArgs(actionKind));
    }

    public bool ContainsScreenPoint(int x, int y)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect)) return false;
        return x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;
    }

    private static string ExtractStepCount(string text)
    {
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? "0" : digits;
    }

    private static bool IsUsableRect(NativeRect rect) =>
        rect.Right > rect.Left && rect.Bottom > rect.Top && rect.Right - rect.Left > 40 && rect.Bottom - rect.Top > 40;

    private static NativeRect GetPrimaryFallbackRect()
    {
        var workArea = SystemParameters.WorkArea;
        return new NativeRect
        {
            Left = (int)Math.Round(workArea.Left),
            Top = (int)Math.Round(workArea.Top),
            Right = (int)Math.Round(workArea.Right),
            Bottom = (int)Math.Round(workArea.Bottom)
        };
    }

    private static void ClampToVirtualScreen(ref int x, ref int y, int width, int height)
    {
        var left = (int)Math.Round(SystemParameters.VirtualScreenLeft);
        var top = (int)Math.Round(SystemParameters.VirtualScreenTop);
        var right = left + (int)Math.Round(SystemParameters.VirtualScreenWidth);
        var bottom = top + (int)Math.Round(SystemParameters.VirtualScreenHeight);

        x = Math.Max(left + 8, Math.Min(x, right - width - 8));
        y = Math.Max(top + 8, Math.Min(y, bottom - height - 8));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static readonly IntPtr HwndTopmost = new(-1);
    private const int GwlExStyle = -20;
    private static readonly IntPtr WsExTransparent = new(0x00000020);
    private static readonly IntPtr WsExToolWindow = new(0x00000080);
    private static readonly IntPtr WsExNoActivate = new(0x08000000);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
}

public sealed class RecordingBadgeActionRequestedEventArgs(JavaRecordedActionKind actionKind) : EventArgs
{
    public JavaRecordedActionKind ActionKind { get; } = actionKind;
}

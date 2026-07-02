using System.Drawing;

namespace WinInspector.Core.Models;

public sealed class DesktopWindowInfo
{
    public required IntPtr Hwnd { get; init; }
    public required string Title { get; init; }
    public required string ClassName { get; init; }
    public required uint ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public required Rectangle Bounds { get; init; }
    public required bool IsVisible { get; init; }
    public required WindowsApplicationKind ApplicationKind { get; init; }
    public required bool IsElevated { get; init; }

    public string HwndDisplay => $"0x{Hwnd.ToInt64():X}";
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? $"{ClassName} ({ProcessName})" : $"{Title} ({ProcessName})";
}

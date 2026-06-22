namespace JabInspector.Core.Models;

public sealed class JavaWindowInfo
{
    public IntPtr Hwnd { get; init; }
    public string Title { get; init; } = "";
    public int VmId { get; init; }
    public long RootContext { get; init; }
    public string ClassName { get; init; } = "";
    public int ProcessId { get; init; }
    public string HwndDisplay => $"0x{Hwnd.ToInt64():X}";
}

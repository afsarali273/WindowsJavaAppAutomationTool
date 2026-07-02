namespace WinInspector.Core.Models;

public sealed class WindowsScreenContext
{
    public DesktopWindowInfo? ActiveWindow { get; set; }
    public string ScreenName { get; set; } = "";
    public string ModalName { get; set; } = "";
    public List<WindowsScreenRegion> Regions { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

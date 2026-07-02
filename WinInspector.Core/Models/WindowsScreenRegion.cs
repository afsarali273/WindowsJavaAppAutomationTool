namespace WinInspector.Core.Models;

public sealed class WindowsScreenRegion
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public WindowsRect Bounds { get; set; }
    public double XRatio { get; set; }
    public double YRatio { get; set; }
    public double WidthRatio { get; set; }
    public double HeightRatio { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

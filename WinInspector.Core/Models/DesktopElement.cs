namespace WinInspector.Core.Models;

public sealed class DesktopElement
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public WindowsRect Bounds { get; set; }
    public DesktopElementSource SourceType { get; set; }
    public ElementKind ElementKind { get; set; }
    public IntPtr? Hwnd { get; set; }
    public string ClassName { get; set; } = "";
    public int? ControlId { get; set; }
    public string ParentId { get; set; } = "";
    public List<string> ChildIds { get; set; } = [];
    public double Confidence { get; set; }
    public List<LocatorCandidate> Locators { get; set; } = [];
    public List<SupportedAction> SupportedActions { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"({Role})" : $"{Name} ({Role})";
}

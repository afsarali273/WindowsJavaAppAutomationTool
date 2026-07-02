namespace WinInspector.Core.Models;

public sealed class LocatorCandidate
{
    public string Id { get; set; } = "";
    public LocatorType Type { get; set; }
    public string Value { get; set; } = "";
    public WindowsRect? Region { get; set; }
    public double Confidence { get; set; }
    public int Priority { get; set; }
    public int Score { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

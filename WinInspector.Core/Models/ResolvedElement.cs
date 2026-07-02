namespace WinInspector.Core.Models;

public sealed class ResolvedElement
{
    public required DesktopElement Element { get; init; }
    public required LocatorCandidate Locator { get; init; }
    public WindowsRect ResolvedBounds { get; init; }
    public IntPtr? ResolvedHandle { get; init; }
    public DesktopElementSource ResolvedSource { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

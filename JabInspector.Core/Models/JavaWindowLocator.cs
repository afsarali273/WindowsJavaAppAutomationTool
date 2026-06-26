namespace JabInspector.Core.Models;

public enum JavaWindowTitleMatch
{
    Exact,
    Contains,
    Regex
}

public sealed class JavaWindowLocator
{
    public string WindowKey { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string Title { get; set; } = "";
    public JavaWindowTitleMatch TitleMatch { get; set; } = JavaWindowTitleMatch.Exact;
    public string ClassName { get; set; } = "";
    public string HwndDisplay { get; set; } = "";
    public string OwnerHwndDisplay { get; set; } = "";
    public int ProcessId { get; set; }
    public int VmId { get; set; }
    public string RootRole { get; set; } = "";
    public string RootRoleEnUs { get; set; } = "";
    public string RootName { get; set; } = "";
    public string RootVirtualAccessibleName { get; set; } = "";
    public string RootDescription { get; set; } = "";
    public string RootPath { get; set; } = "";
    public int OpenedByStep { get; set; } = -1;
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}

namespace JabInspector.Core.Models;

public sealed class JavaRecordingProject
{
    public int SchemaVersion { get; set; } = 2;
    public string SessionName { get; set; } = "";
    public string ApplicationAlias { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public string WindowTitle { get; set; } = "";
    public string WindowClassName { get; set; } = "";
    public List<JavaWindowLocator> Windows { get; set; } = [];
    public List<JavaObjectRepositoryEntry> Repository { get; set; } = [];
    public List<JavaRecordedStep> Steps { get; set; } = [];
}

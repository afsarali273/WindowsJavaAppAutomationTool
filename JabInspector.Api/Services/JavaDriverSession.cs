using JabInspector.Core.Models;

namespace JabInspector.Api.Services;

internal sealed class JavaDriverSession
{
    public required string Id { get; init; }
    public required JavaWindowInfo Window { get; set; }
    public AccessibleNode? Root { get; set; }
    public int NodeCount { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime LastRefreshedAtUtc { get; set; } = DateTime.UtcNow;
    public List<JavaObjectRepositoryEntry> Repository { get; } = [];
}

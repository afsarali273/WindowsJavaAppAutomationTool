using System.Text.Json.Serialization;

namespace JabInspector.Core.Models;

public enum ResolutionStatus
{
    Found,
    NotFound,
    Ambiguous
}

public sealed record ResolutionPolicy(
    int MinimumScore = 72,
    int AmbiguityScoreDelta = 18,
    int MaxCandidates = 5,
    bool RequireUnique = true,
    int TimeoutMs = 5000,
    int PollIntervalMs = 200,
    bool RefreshTreeOnFailure = true,
    bool AllowCoordinateFallback = false)
{
    public static ResolutionPolicy Default { get; } = new();

    public ResolutionPolicy Sanitize() => this with
    {
        MinimumScore = Math.Clamp(MinimumScore, 0, 500),
        AmbiguityScoreDelta = Math.Clamp(AmbiguityScoreDelta, 0, 200),
        MaxCandidates = Math.Clamp(MaxCandidates, 1, 25),
        TimeoutMs = Math.Clamp(TimeoutMs, 0, 60000),
        PollIntervalMs = Math.Clamp(PollIntervalMs, 50, 5000)
    };
}

public sealed record LocatorStrategy(
    string Name,
    int Priority,
    IReadOnlyList<string> MustMatch,
    IReadOnlyList<string> ShouldMatch);

public sealed record ResolutionCandidate(
    string DisplayName,
    int Score,
    string Role,
    string RoleEnUs,
    string Name,
    string VirtualAccessibleName,
    string Description,
    string Path,
    string IndexPath,
    string XPath,
    string ParentRole,
    string ParentName,
    ElementBounds Bounds,
    IReadOnlyList<string> Mismatches);

public sealed class ResolutionResult
{
    public ResolutionStatus Status { get; init; }
    public string Message { get; init; } = "";
    public string StrategyName { get; init; } = "";
    public int CandidateCount { get; init; }
    public IReadOnlyList<ResolutionCandidate> Candidates { get; init; } = [];

    [JsonIgnore]
    public AccessibleNode? Node { get; init; }

    public bool Success => Status == ResolutionStatus.Found && Node is not null;

    public static ResolutionResult Found(AccessibleNode node, string strategyName, IReadOnlyList<ResolutionCandidate> candidates) => new()
    {
        Status = ResolutionStatus.Found,
        Message = $"Resolved '{node.DisplayName}' using {strategyName}.",
        StrategyName = strategyName,
        CandidateCount = candidates.Count,
        Candidates = candidates,
        Node = node
    };

    public static ResolutionResult NotFound(string message, IReadOnlyList<ResolutionCandidate> candidates) => new()
    {
        Status = ResolutionStatus.NotFound,
        Message = message,
        CandidateCount = candidates.Count,
        Candidates = candidates
    };

    public static ResolutionResult Ambiguous(string message, string strategyName, IReadOnlyList<ResolutionCandidate> candidates) => new()
    {
        Status = ResolutionStatus.Ambiguous,
        Message = message,
        StrategyName = strategyName,
        CandidateCount = candidates.Count,
        Candidates = candidates
    };
}

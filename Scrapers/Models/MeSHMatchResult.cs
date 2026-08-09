namespace Scrapers.Models;

public class MeSHMatchResult
{
    public required string Value { get; init; }
    public string StudyNctId { get; init; } = "";
    public string Source { get; init; } = "condition";
    public bool SideBMatched { get; init; }
    public string MeshTerm { get; init; } = "";
    public string MeshCui { get; init; } = "";
    public string Category { get; init; } = "unmapped";
    public float Similarity { get; init; }
    public bool Accepted => SideBMatched;
    public string RejectionReason => Accepted ? "" : $"below_threshold (sim={Similarity:F4})";
}

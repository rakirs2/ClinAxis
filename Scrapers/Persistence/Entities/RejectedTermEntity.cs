using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities;

public class RejectedTermEntity
{
    [Key]
    public int Id { get; set; }
    public string StudyNctId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Source { get; set; } = "condition";
    public bool SideBMatched { get; set; }
    public string SideBMeshTerm { get; set; } = string.Empty;
    public string SideBMeshCui { get; set; } = string.Empty;
    public string SideBCategory { get; set; } = "unmapped";
    public float SideBSimilarity { get; set; }
    public bool Accepted { get; set; }
    public string RejectionReason { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

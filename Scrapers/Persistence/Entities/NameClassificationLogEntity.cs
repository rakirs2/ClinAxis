using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities;

public class NameClassificationLogEntity
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? StudyNctId { get; set; }
    public string NameFilterDecision { get; set; } = string.Empty;
    public string? NameFilterReason { get; set; }
    public string MlDecision { get; set; } = string.Empty;
    public double MlConfidence { get; set; }
    public string? UserClassification { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities;

public class PipelineModelDisagreementEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public string RuleBasedResult { get; set; } = string.Empty;
    public string? RuleBasedNpi { get; set; }
    public string? BertTopCandidate { get; set; }
    public double? BertTopScore { get; set; }
    public string? BertRecommendedResult { get; set; }
    public string DisagreementType { get; set; } = string.Empty;
    public string? InvestigatorContext { get; set; }
    public string? CandidatesJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public InvestigatorPersonEntity Person { get; set; } = null!;
}

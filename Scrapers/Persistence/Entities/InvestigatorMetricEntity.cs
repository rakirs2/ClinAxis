using System.ComponentModel.DataAnnotations;

namespace Scrapers.Persistence.Entities;

/// <summary>
/// Stores investigator citation metrics from external sources (e.g., Semantic Scholar, OpenAlex).
/// Supports multiple sources per investigator and future metric types.
/// </summary>
public class InvestigatorMetricEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to InvestigatorPersonEntity.
    /// </summary>
    public Guid InvestigatorPersonId { get; set; }

    /// <summary>
    /// Source of metrics (e.g., "SemanticScholar", "OpenAlex").
    /// Allows storing metrics from multiple sources per investigator.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// H-Index: the largest number h such that the investigator has at least h papers with h or more citations.
    /// </summary>
    public int? HIndex { get; set; }

    /// <summary>
    /// Total number of citations across all papers.
    /// </summary>
    public int? CitationCount { get; set; }

    /// <summary>
    /// I10-Index: number of papers with 10 or more citations.
    /// </summary>
    public int? I10Index { get; set; }

    /// <summary>
    /// Total number of papers from this source (may differ from PubMed count).
    /// </summary>
    public int? TotalPapers { get; set; }

    /// <summary>
    /// External identifier from the source (e.g., Semantic Scholar authorId).
    /// </summary>
    public string? ExternalAuthorId { get; set; }

    /// <summary>
    /// Timestamp when we last attempted to fetch metrics.
    /// </summary>
    public DateTime? LookupAttemptedAt { get; set; }

    /// <summary>
    /// Result of the lookup: "found", "not_found", "error", "ambiguous".
    /// </summary>
    public string? LookupResult { get; set; }

    /// <summary>
    /// Error message if lookup failed.
    /// </summary>
    public string? LookupErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the parent investigator.
    /// </summary>
    public InvestigatorPersonEntity InvestigatorPerson { get; set; } = null!;
}

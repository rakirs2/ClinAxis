using Scrapers.Persistence.Entities;

namespace Scrapers.Services.CrawlServices;

/// <summary>
/// Interface for pivot enricher services that extend studies with additional data sources.
/// Implementations are auto-discovered and registered via reflection.
/// Each service is responsible for enriching studies with a specific data source (PubMed, Investigator networks, etc.)
/// </summary>
public interface IPivotEnricherService
{
    /// <summary>
    /// Display name of the pivot (e.g., "PubMed", "InvestigatorNetwork")
    /// Must be unique and match database pivot name.
    /// </summary>
    string PivotName { get; }

    /// <summary>
    /// Enrich a single study with data from this pivot's data source.
    /// Should handle:
    /// - Checking source_fetch_history to avoid re-fetching if recently cached
    /// - Recording fetch in source_fetch_history after successful enrichment
    /// - Enqueuing next-step events (e.g., "pubmed.complete")
    /// - Graceful handling of API errors / missing data
    /// </summary>
    Task EnrichAsync(StudyEntity study, CancellationToken ct = default);

    /// <summary>
    /// Check if this pivot should attempt to enrich the given study.
    /// Can return false if pivot is disabled, study doesn't have required data, etc.
    /// </summary>
    Task<bool> CanEnrichAsync(StudyEntity study, CancellationToken ct = default);
}

namespace Scrapers.Persistence.Entities;

/// <summary>
/// Configuration for scraper pivot services (data enrichment sources).
/// Allows ops to enable/disable pivots and configure cache TTL without code changes.
/// </summary>
public sealed class ScraperPivotEntity
{
    public int Id { get; set; }
    
    /// <summary>Display name of the pivot (e.g., "PubMed", "InvestigatorNetwork")</summary>
    public string Name { get; set; } = null!;
    
    /// <summary>Fully qualified service type name for auto-discovery</summary>
    public string ServiceType { get; set; } = null!;
    
    /// <summary>Whether this pivot is currently enabled</summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>Cache TTL in days (skip re-fetching if last fetch within this period)</summary>
    public int CacheTtlDays { get; set; } = 90;
    
    /// <summary>Batch size for processing (how many studies to enrich per batch)</summary>
    public int BatchSize { get; set; } = 100;
    
    /// <summary>When the pivot was created</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Last updated timestamp</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

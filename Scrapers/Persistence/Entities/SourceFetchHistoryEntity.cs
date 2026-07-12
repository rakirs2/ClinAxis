namespace Scrapers.Persistence.Entities;

/// <summary>
/// Tracks fetch history per study/source combination to enable deduplication.
/// Prevents re-fetching the same data within the configured TTL (cache validity period).
/// </summary>
public sealed class SourceFetchHistoryEntity
{
    public int Id { get; set; }
    
    /// <summary>NCT ID of the study</summary>
    public string StudyNctId { get; set; } = null!;
    
    /// <summary>Data source type (e.g., "PubMed", "Investigator", "ExternalDataset")</summary>
    public string SourceType { get; set; } = null!;
    
    /// <summary>Timestamp of last successful fetch</summary>
    public DateTime? LastFetchTimestamp { get; set; }
    
    /// <summary>Hash of fetched content (optional, for detecting actual changes)</summary>
    public string? ContentHash { get; set; }
    
    /// <summary>When the record was created</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Last updated timestamp</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

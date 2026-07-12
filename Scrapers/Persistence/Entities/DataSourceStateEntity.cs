namespace Scrapers.Persistence.Entities;

/// <summary>
/// Tracks the state of each data source (CT.gov, PubMed, etc.) including last sync timestamp
/// to enable resumable, incremental scraping without re-downloading unchanged data.
/// </summary>
public sealed class DataSourceStateEntity
{
    public int Id { get; set; }
    
    /// <summary>Unique source name (e.g., "ClinicalTrials.gov", "PubMed", "Aggregations")</summary>
    public string SourceName { get; set; } = null!;
    
    /// <summary>Timestamp of last successful sync (used for incremental fetches)</summary>
    public DateTime? LastSyncTimestamp { get; set; }
    
    /// <summary>Hash of last sync result (optional, for change detection)</summary>
    public string? LastSyncHash { get; set; }
    
    /// <summary>Current status: idle, syncing, failed</summary>
    public string Status { get; set; } = "idle";
    
    /// <summary>Error message if status is 'failed'</summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>Last updated timestamp</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

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

    /// <summary>Cumulative count of keywords rejected during ingestion for this source</summary>
    public int RejectedKeywordsTotal { get; set; }

    /// <summary>Predicted next run time (set by the background service each loop)</summary>
    public DateTime? NextScheduledRun { get; set; }

    /// <summary>Full-corpus sweep state: idle, in-progress, complete, failed.</summary>
    public string? BackfillStatus { get; set; }

    /// <summary>Studies still missing from the DB while a sweep is in progress.</summary>
    public int? BackfillRemainingStudies { get; set; }

    /// <summary>UTC start of the current/last sweep; used as the reconciliation cutoff.</summary>
    public DateTime? BackfillStartedUtc { get; set; }

    /// <summary>UTC completion of the last full sweep (lastFullSweepUtc).</summary>
    public DateTime? BackfillCompletedUtc { get; set; }

    /// <summary>Pending operator-requested run: null = none, "incremental" or "full".</summary>
    public string? ManualRunMode { get; set; }
}

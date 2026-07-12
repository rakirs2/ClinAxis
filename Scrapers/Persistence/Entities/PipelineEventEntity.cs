namespace Scrapers.Persistence.Entities;

/// <summary>
/// Represents an event in the pub/sub event queue for coordinating pipeline services.
/// Supports claim-process-complete pattern with exponential backoff retries and dead-letter queue.
/// </summary>
public sealed class PipelineEventEntity
{
    public int Id { get; set; }
    
    /// <summary>Event type identifier (e.g., "studies.discovered", "pubmed.complete")</summary>
    public string EventType { get; set; } = null!;
    
    /// <summary>Minimal JSON payload (typically just IDs to fetch from DB)</summary>
    public string? Data { get; set; }
    
    /// <summary>Current status: pending, processing, completed, failed, dead-letter</summary>
    public string Status { get; set; } = "pending";
    
    /// <summary>Service instance that claimed this event</summary>
    public string? ClaimedBy { get; set; }
    
    /// <summary>Timestamp when event was claimed</summary>
    public DateTime? ClaimedAt { get; set; }
    
    /// <summary>Timestamp when event was completed successfully</summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>Error message from failed processing attempt</summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>Number of retry attempts (incremented each failure)</summary>
    public int RetryCount { get; set; }
    
    /// <summary>Timestamp of last error</summary>
    public DateTime? LastErrorAt { get; set; }
    
    /// <summary>When the event was created</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Last updated timestamp</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

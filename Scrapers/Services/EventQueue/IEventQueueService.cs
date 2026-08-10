using Scrapers.Persistence.Entities;

namespace Scrapers.Services.EventQueue;

/// <summary>
/// Service for managing the event queue with pub/sub semantics.
/// Supports claim-process-complete pattern with exponential backoff retries and dead-letter queue.
/// </summary>
public interface IEventQueueService
{
    /// <summary>
    /// Enqueue a new event for processing.
    /// </summary>
    Task EnqueueAsync(string eventType, string? data = null, CancellationToken ct = default);

    /// <summary>
    /// Claim the next pending event for processing.
    /// Returns null if no events are pending or all are claimed.
    /// </summary>
    Task<PipelineEventEntity?> ClaimNextPendingEventAsync(string claimedBy, string[]? eventTypes = null, CancellationToken ct = default);

    /// <summary>
    /// Returns whether an event of the given type is pending or processing.
    /// </summary>
    Task<bool> HasActiveEventAsync(string eventType, CancellationToken ct = default);

    /// <summary>
    /// Returns whether an unresolved discovery event covers the given window start.
    /// Legacy count-only dead letters are excluded because they have no recoverable window.
    /// </summary>
    Task<bool> HasUnresolvedDiscoveryEventAsync(DateTime? lastUpdatedPost, CancellationToken ct = default);

    /// <summary>
    /// Mark an event as successfully completed.
    /// </summary>
    Task CompleteEventAsync(int eventId, CancellationToken ct = default);

    /// <summary>
    /// Record incremental progress for a claimed (processing) event so operators can
    /// observe long-running work between completions. No-op if the event is not claimed.
    /// </summary>
    Task UpdateEventProgressAsync(int eventId, int processed, int total, CancellationToken ct = default);

    /// <summary>
    /// Mark an event as failed and schedule for retry with exponential backoff.
    /// After 4 failures, event moves to dead-letter queue.
    /// </summary>
    Task FailEventAsync(int eventId, string errorMessage, CancellationToken ct = default);

    /// <summary>
    /// Get events in dead-letter queue (failed after max retries).
    /// Paginated by created_at DESC.
    /// </summary>
    Task<List<PipelineEventEntity>> GetDeadLetterEventsAsync(int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Get queue statistics for monitoring.
    /// </summary>
    Task<EventQueueStats> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Manually retry a dead-letter event (ops intervention).
    /// Resets status to 'pending' and retry_count to 0.
    /// </summary>
    Task RetryDeadLetterEventAsync(int eventId, CancellationToken ct = default);

    /// <summary>
    /// Reset every dead-letter event (optionally of one event type) back to pending
    /// for reprocessing after a pipeline fix. Returns the number of events reset.
    /// </summary>
    Task<int> RetryAllDeadLetterEventsAsync(string? eventType = null, CancellationToken ct = default);

    /// <summary>
    /// Mark a dead-letter event as ignored (won't be retried).
    /// Status becomes 'completed' with error_message preserved.
    /// </summary>
    Task IgnoreDeadLetterEventAsync(int eventId, CancellationToken ct = default);

    /// <summary>
    /// Release a single claimed event back to pending for another service to claim.
    /// </summary>
    Task ReleaseEventAsync(int eventId, CancellationToken ct = default);

    /// <summary>
    /// Release claimed events that have been stuck for timeout period.
    /// Allows other services to re-claim and retry.
    /// </summary>
    Task ReleaseStuckEventsAsync(TimeSpan claimTimeout, CancellationToken ct = default);

    /// <summary>
    /// Get event type breakdown with duration percentiles.
    /// </summary>
    Task<List<EventTypeBreakdown>> GetEventTypeBreakdownAsync(CancellationToken ct = default);

    /// <summary>
    /// Get time-bucketed duration history for trend display.
    /// </summary>
    Task<List<DurationHistoryPoint>> GetDurationHistoryAsync(string? eventType = null, string period = "24h", int bucketMinutes = 60, CancellationToken ct = default);
}

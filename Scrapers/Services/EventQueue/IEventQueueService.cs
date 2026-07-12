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
    Task<PipelineEventEntity?> ClaimNextPendingEventAsync(string claimedBy, CancellationToken ct = default);

    /// <summary>
    /// Mark an event as successfully completed.
    /// </summary>
    Task CompleteEventAsync(int eventId, CancellationToken ct = default);

    /// <summary>
    /// Mark an event as failed and schedule for retry with exponential backoff.
    /// After 3 failures, event moves to dead-letter queue.
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
    /// Mark a dead-letter event as ignored (won't be retried).
    /// Status becomes 'completed' with error_message preserved.
    /// </summary>
    Task IgnoreDeadLetterEventAsync(int eventId, CancellationToken ct = default);

    /// <summary>
    /// Release claimed events that have been stuck for timeout period.
    /// Allows other services to re-claim and retry.
    /// </summary>
    Task ReleaseStuckEventsAsync(TimeSpan claimTimeout, CancellationToken ct = default);
}

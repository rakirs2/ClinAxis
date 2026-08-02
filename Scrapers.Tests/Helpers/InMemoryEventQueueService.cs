using Scrapers.Persistence.Entities;
using Scrapers.Services.EventQueue;

namespace Scrapers.Tests.Helpers;

/// <summary>
/// In-memory <see cref="IEventQueueService"/> for unit tests. No database involved.
/// </summary>
internal sealed class InMemoryEventQueueService : IEventQueueService
{
    public List<PipelineEventEntity> DeadLetterEvents { get; set; } = [];

    public Exception? ThrowOnGetDeadLetter { get; set; }

    public Task EnqueueAsync(string eventType, string? data = null, CancellationToken ct = default) => Task.CompletedTask;

    public Task<PipelineEventEntity?> ClaimNextPendingEventAsync(
        string claimedBy,
        string[]? eventTypes = null,
        CancellationToken ct = default) => Task.FromResult<PipelineEventEntity?>(null);

    public Task CompleteEventAsync(int eventId, CancellationToken ct = default) => Task.CompletedTask;

    public Task FailEventAsync(int eventId, string errorMessage, CancellationToken ct = default) => Task.CompletedTask;

    public Task<List<PipelineEventEntity>> GetDeadLetterEventsAsync(int limit = 100, CancellationToken ct = default)
    {
        if (ThrowOnGetDeadLetter != null)
        {
            throw ThrowOnGetDeadLetter;
        }

        return Task.FromResult(DeadLetterEvents.Take(limit).ToList());
    }

    public Task<EventQueueStats> GetStatsAsync(CancellationToken ct = default) =>
        Task.FromResult(new EventQueueStats());

    public Task RetryDeadLetterEventAsync(int eventId, CancellationToken ct = default) => Task.CompletedTask;

    public Task IgnoreDeadLetterEventAsync(int eventId, CancellationToken ct = default) => Task.CompletedTask;

    public Task ReleaseEventAsync(int eventId, CancellationToken ct = default) => Task.CompletedTask;

    public Task ReleaseStuckEventsAsync(TimeSpan claimTimeout, CancellationToken ct = default) => Task.CompletedTask;

    public Task<List<EventTypeBreakdown>> GetEventTypeBreakdownAsync(CancellationToken ct = default) =>
        Task.FromResult<List<EventTypeBreakdown>>([]);

    public Task<List<DurationHistoryPoint>> GetDurationHistoryAsync(
        string? eventType = null,
        string period = "24h",
        int bucketMinutes = 60,
        CancellationToken ct = default) => Task.FromResult<List<DurationHistoryPoint>>([]);
}

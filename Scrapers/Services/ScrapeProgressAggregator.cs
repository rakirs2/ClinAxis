using Scrapers.Persistence.Entities;

namespace Scrapers.Services;

/// <summary>
/// Aggregated scrape progress for one source, derived from <c>scrape_events</c>.
/// A "run" starts at the latest <c>discovery.completed</c> event and consumes the
/// <c>batch.completed</c> events that follow it.
/// </summary>
/// <param name="TotalInRun">Studies the latest discovery found (or the number processed if unknown).</param>
/// <param name="RecordsProcessed">Records processed across batches since the latest discovery.</param>
/// <param name="BatchesCompleted">Batches completed since the latest discovery.</param>
/// <param name="EstimatedRemaining">Best-effort remaining estimate (<c>TotalInRun - RecordsProcessed</c>, never negative).</param>
/// <param name="Status"><c>completed</c>, <c>in_progress</c> or <c>pending</c>.</param>
public sealed record ScrapeProgressSummary(
    int TotalInRun,
    int RecordsProcessed,
    int BatchesCompleted,
    int EstimatedRemaining,
    string Status,
    string? LastBatchNctRange,
    DateTime? LastBatchAt,
    long? LastBatchDurationMs,
    int? LastBatchRecords,
    DateTime? RunStartedAt);

public static class ScrapeProgressAggregator
{
    public const string DiscoveryEventType = "discovery.completed";
    public const string BatchEventType = "batch.completed";
    public const string RunCompletedEventType = "run.completed";

    /// <summary>
    /// Summarizes scrape progress for <paramref name="source"/> from events ordered by Id.
    /// Returns <c>null</c> when the source has no discovery event yet.
    /// </summary>
    public static ScrapeProgressSummary? Summarize(IEnumerable<ScrapeEventEntity> events, string source)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));
        }

        var ordered = events.OrderBy(e => e.Id).ToList();
        var discoveryIndex = ordered.FindLastIndex(e => e.EventType == DiscoveryEventType && e.Source == source);
        if (discoveryIndex < 0)
        {
            return null;
        }

        var discovery = ordered[discoveryIndex];
        var tail = ordered
            .Skip(discoveryIndex + 1)
            .Where(e => e.Source == source)
            .ToList();

        var batchEvents = tail.Where(e => e.EventType == BatchEventType).ToList();
        var processed = batchEvents.Sum(e => e.RecordsAffected ?? 0);
        var total = discovery.RecordsAffected ?? processed;
        var lastBatch = batchEvents.LastOrDefault();

        var status = tail.Any(e => e.EventType == RunCompletedEventType)
            ? "completed"
            : batchEvents.Count == 0
                ? "pending"
                : processed >= total && total > 0
                    ? "completed"
                    : "in_progress";

        return new ScrapeProgressSummary(
            TotalInRun: total,
            RecordsProcessed: processed,
            BatchesCompleted: batchEvents.Count,
            EstimatedRemaining: Math.Max(0, total - processed),
            Status: status,
            LastBatchNctRange: lastBatch?.Message,
            LastBatchAt: lastBatch?.Timestamp,
            LastBatchDurationMs: lastBatch?.DurationMs,
            LastBatchRecords: lastBatch?.RecordsAffected,
            RunStartedAt: discovery.Timestamp);
    }
}

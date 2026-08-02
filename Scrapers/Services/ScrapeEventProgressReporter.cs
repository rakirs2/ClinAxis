using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Services;

/// <summary>
/// Writes ingestion progress as <c>scrape_events</c> rows so progress is queryable
/// from the running instance (see <see cref="ScrapeProgressAggregator"/>).
/// </summary>
public sealed class ScrapeEventProgressReporter : INgestionProgressReporter
{
    private readonly string _connectionString;
    private readonly string _source;

    public ScrapeEventProgressReporter(string connectionString, string source = "ClinicalTrials.gov")
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must be provided.", nameof(connectionString));
        }
        _connectionString = connectionString;
        _source = source;
    }

    public async Task ReportDiscoveryCompletedAsync(int studyCount, DateTime? since, CancellationToken cancellationToken = default)
    {
        await AddEventAsync(BuildDiscoveryEvent(_source, studyCount, since), cancellationToken).ConfigureAwait(false);
    }

    public async Task ReportBatchCompletedAsync(IngestBatchInfo batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        await AddEventAsync(BuildBatchEvent(_source, batch), cancellationToken).ConfigureAwait(false);
    }

    public async Task ReportRunCompletedAsync(int totalRecords, TimeSpan elapsed, CancellationToken cancellationToken = default)
    {
        await AddEventAsync(BuildRunEvent(_source, totalRecords, elapsed), cancellationToken).ConfigureAwait(false);
    }

    internal static ScrapeEventEntity BuildDiscoveryEvent(string source, int studyCount, DateTime? since)
    {
        return new ScrapeEventEntity
        {
            Source = source,
            EventType = "discovery.completed",
            RecordsAffected = studyCount,
            Message = since.HasValue ? $"Studies updated since {since.Value:O}" : "Full re-scan",
            Timestamp = DateTime.UtcNow
        };
    }

    internal static ScrapeEventEntity BuildBatchEvent(string source, IngestBatchInfo batch)
    {
        return new ScrapeEventEntity
        {
            Source = source,
            EventType = "batch.completed",
            RecordsAffected = batch.RecordsProcessed,
            DurationMs = (long)batch.Elapsed.TotalMilliseconds,
            Message = BuildNctRange(batch.NctIds),
            Timestamp = DateTime.UtcNow
        };
    }

    internal static ScrapeEventEntity BuildRunEvent(string source, int totalRecords, TimeSpan elapsed)
    {
        return new ScrapeEventEntity
        {
            Source = source,
            EventType = "run.completed",
            RecordsAffected = totalRecords,
            DurationMs = (long)elapsed.TotalMilliseconds,
            Message = $"Ingest run completed: {totalRecords} records",
            Timestamp = DateTime.UtcNow
        };
    }

    internal static string BuildNctRange(IReadOnlyList<string> nctIds)
    {
        if (nctIds.Count == 0)
        {
            return string.Empty;
        }

        var range = nctIds.Count == 1
            ? nctIds[0]
            : $"{nctIds[0]} ... {nctIds[^1]}";

        return range.Length <= 200 ? range : range[..200];
    }

    private async Task AddEventAsync(ScrapeEventEntity evt, CancellationToken cancellationToken)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);
        context.ScrapeEvents.Add(evt);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

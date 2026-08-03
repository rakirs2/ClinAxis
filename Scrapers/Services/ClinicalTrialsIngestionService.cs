using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;

namespace Scrapers.Services;

public class ClinicalTrialsIngestionService
{
    private static readonly Action<ILogger, int, int, long, string, string, Exception?> LogBatchCompleted =
        LoggerMessage.Define<int, int, long, string, string>(
            LogLevel.Information,
            new EventId(1, "BatchCompleted"),
            "Ingest batch {BatchNumber}: {RecordCount} records in {ElapsedMs}ms (range {FirstNct} ... {LastNct})");

    private static readonly Action<ILogger, int, long, Exception?> LogRunCompleted =
        LoggerMessage.Define<int, long>(
            LogLevel.Information,
            new EventId(2, "RunCompleted"),
            "Ingest run completed: {TotalIngested} records in {ElapsedMs}ms");

    private readonly ClinicalTrialsGov _client;
    private readonly StudyRepository _repository;
    private readonly INgestionProgressReporter? _progressReporter;
    private readonly ILogger<ClinicalTrialsIngestionService>? _logger;

    public ClinicalTrialsIngestionService(
        ClinicalTrialsGov client,
        StudyRepository repository,
        INgestionProgressReporter? progressReporter = null,
        ILogger<ClinicalTrialsIngestionService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _progressReporter = progressReporter;
        _logger = logger;
    }

    /// <summary>
    /// Fetches up to <paramref name="count"/> studies and upserts them.
    /// </summary>
    /// <param name="count">Upper bound of records to fetch.</param>
    /// <param name="lastUpdatedPost">When set, fetches only studies updated since this date.
    /// Used by the incremental loop.</param>
    /// <param name="lastUpdatedPostTo">When set together with <paramref name="lastUpdatedPost"/>,
    /// fetches only studies updated in the inclusive window — used by backfill chunk events.</param>
    public async Task<int> IngestAsync(
        int count,
        DateTime? lastUpdatedPost = null,
        DateTime? lastUpdatedPostTo = null,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return 0;
        }

        await _repository.MigrateSchemaAsync(cancellationToken).ConfigureAwait(false);

        var totalIngested = 0;
        var batchNumber = 0;
        var runSw = Stopwatch.StartNew();

        await _client.GetTrialRecordsBatchedAsync(count, async batch =>
        {
            var batchSw = Stopwatch.StartNew();
            var ingested = await _repository.UpdateStudiesWithClinicalTrialsAsync(batch, cancellationToken).ConfigureAwait(false);
            batchSw.Stop();
            totalIngested += ingested;
            batchNumber++;

            var nctIds = batch.Select(r => r.NctId).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).ToList();
            if (_logger != null && _logger.IsEnabled(LogLevel.Information))
            {
                LogBatchCompleted(
                    _logger,
                    batchNumber,
                    batch.Count,
                    batchSw.ElapsedMilliseconds,
                    nctIds.FirstOrDefault() ?? "-",
                    nctIds.LastOrDefault() ?? "-",
                    null);
            }

            if (_progressReporter != null)
            {
                await _progressReporter
                    .ReportBatchCompletedAsync(new IngestBatchInfo(batchNumber, batch.Count, batchSw.Elapsed, nctIds), cancellationToken)
                    .ConfigureAwait(false);
            }
        }, lastUpdatedPost: lastUpdatedPost, lastUpdatedPostTo: lastUpdatedPostTo, cancellationToken: cancellationToken).ConfigureAwait(false);

        runSw.Stop();
        if (_progressReporter != null)
        {
            await _progressReporter.ReportRunCompletedAsync(totalIngested, runSw.Elapsed, cancellationToken).ConfigureAwait(false);
        }

        if (_logger != null && _logger.IsEnabled(LogLevel.Information))
        {
            LogRunCompleted(_logger, totalIngested, runSw.ElapsedMilliseconds, null);
        }

        return totalIngested;
    }
}

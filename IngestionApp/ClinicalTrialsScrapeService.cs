using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Services;
using Scrapers.Services.Backfill;
using Scrapers.Services.EventQueue;
using Scrapers.Utilities;

namespace IngestionApp;

/// <summary>
/// Long-running background service that periodically scrapes ClinicalTrials.gov.
/// Runs every configured interval and fetches new/updated studies since last sync.
/// For each discovered study, enqueues a "studies.discovered" event for downstream processing.
///
/// Also owns the full-corpus backfill lifecycle (#394): when the coverage gap exceeds
/// BACKFILL_THRESHOLD it plans date-window chunks (oldest first) and enqueues one
/// "studies.backfill" event per window; while a sweep is in flight it monitors the chunk
/// queue, and when every chunk completes it runs the reconciliation that flags studies
/// clinicaltrials.gov no longer has (never deletes rows).
/// </summary>
internal sealed class ClinicalTrialsScrapeService : BackgroundService
{
    private readonly IEventQueueService _eventQueueService;
    private readonly IDataSourceStateService _dataSourceStateService;
    private readonly INgestionProgressReporter? _progressReporter;
    private readonly int _scrapeIntervalMinutes;
    private readonly int _pageSize;
    private readonly int _backfillThreshold;
    private readonly int _backfillChunkMaxStudies;
    private readonly DateOnly _backfillEarliestDate;
    private readonly ClinicalTrialsGov _ctClient;
    private readonly BackfillCoordinatorService _backfillCoordinator;
    private readonly StudyRepository _repository;

    private const string SourceName = "ClinicalTrials.gov";
    private DateTime _lastRunTime = DateTime.MinValue;
    private int _consecutiveFailures;
    private bool _forceFullSweep;

    public ClinicalTrialsScrapeService(
        IEventQueueService eventQueueService,
        IDataSourceStateService dataSourceStateService,
        INgestionProgressReporter? progressReporter = null,
        int scrapeIntervalMinutes = 60,
        int pageSize = 500,
        int backfillThreshold = 10_000,
        int backfillChunkMaxStudies = 10_000,
        DateOnly backfillEarliestDate = default,
        ClinicalTrialsGov? ctClient = null,
        BackfillCoordinatorService? backfillCoordinator = null,
        StudyRepository? repository = null)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _dataSourceStateService = dataSourceStateService ?? throw new ArgumentNullException(nameof(dataSourceStateService));
        _progressReporter = progressReporter;
        _scrapeIntervalMinutes = scrapeIntervalMinutes;
        _pageSize = pageSize;
        _backfillThreshold = backfillThreshold;
        _backfillChunkMaxStudies = backfillChunkMaxStudies;
        _backfillEarliestDate = backfillEarliestDate == default ? new DateOnly(1999, 1, 1) : backfillEarliestDate;
        _ctClient = ctClient ?? new ClinicalTrialsGov(pageSize: _pageSize);
        _backfillCoordinator = backfillCoordinator ?? throw new ArgumentNullException(nameof(backfillCoordinator));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize data source state
        await _dataSourceStateService.InitializeSourceAsync(SourceName, stoppingToken).ConfigureAwait(false);

        // Run scrape loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Consume any operator-requested run (POST /api/ingest/run) before gating
                // on the scheduled interval, so the next tick fires immediately.
                var manualRunMode = await _dataSourceStateService
                    .ConsumeManualRunAsync(SourceName, stoppingToken)
                    .ConfigureAwait(false);
                if (manualRunMode is not null)
                {
                    if (string.Equals(manualRunMode, ManualRunRequest.FullMode, StringComparison.OrdinalIgnoreCase))
                    {
                        _forceFullSweep = true;
                    }

                    _lastRunTime = DateTime.MinValue;
                }

                var timeSinceLastRun = DateTime.UtcNow - _lastRunTime;

                if (timeSinceLastRun >= TimeSpan.FromMinutes(_scrapeIntervalMinutes))
                {
                    await PerformScrapeAsync(_ctClient, stoppingToken).ConfigureAwait(false);
                    _lastRunTime = DateTime.UtcNow;
                    _consecutiveFailures = 0;
                }

                // Update predicted next run time each loop iteration
                var nextRun = _lastRunTime == DateTime.MinValue
                    ? DateTime.UtcNow
                    : _lastRunTime.AddMinutes(_scrapeIntervalMinutes);
                await _dataSourceStateService.UpdateNextScheduledRunAsync(SourceName, nextRun, stoppingToken).ConfigureAwait(false);

                // Sleep briefly to avoid busy-waiting
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures = Math.Min(_consecutiveFailures + 1, 3);
                await _dataSourceStateService.SetStatusAsync(
                    SourceName,
                    "failed",
                    $"Scrape error: {ex.Message}",
                    stoppingToken).ConfigureAwait(false);

                await Task.Delay(
                    ScrapeBackoffCalculator.GetDelay(_consecutiveFailures),
                    stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task PerformScrapeAsync(ClinicalTrialsGov ctClient, CancellationToken ct)
    {
        await _dataSourceStateService.SetStatusAsync(SourceName, "syncing", ct: ct).ConfigureAwait(false);

        // Get last sync timestamp for incremental fetching
        var state = await _dataSourceStateService.GetStateAsync(SourceName, ct).ConfigureAwait(false);
        var lastSyncTimestamp = state?.LastSyncTimestamp;
        var windowEnd = DateTime.UtcNow;

        // Count new/updated studies from CT.gov API since last sync (lightweight countTotal call)
        var studyCount = await ctClient.CountStudiesAsync(
            lastUpdatedPost: lastSyncTimestamp,
            lastUpdatedPostTo: windowEnd,
            cancellationToken: ct).ConfigureAwait(false);

        if (_progressReporter != null)
        {
            await _progressReporter
                .ReportDiscoveryCompletedAsync(studyCount, lastSyncTimestamp, ct)
                .ConfigureAwait(false);
        }

        var hasUnresolvedDiscoveryEvent = await _eventQueueService
            .HasUnresolvedDiscoveryEventAsync(lastSyncTimestamp, ct)
            .ConfigureAwait(false);

        if (studyCount > 0 && !hasUnresolvedDiscoveryEvent)
        {
            var eventData = new IncrementalDiscoveryEventPayload(
                studyCount,
                lastSyncTimestamp,
                windowEnd).ToJson();
            await _eventQueueService.EnqueueAsync("studies.discovered", eventData, ct)
                .ConfigureAwait(false);
        }

        // Full-corpus backfill lifecycle (idempotent; runs after the incremental discovery)
        await ManageBackfillAsync(ct).ConfigureAwait(false);

        // A discovery event owns the cursor until its ingestion succeeds. When there
        // is no work, advance it here because no event needs to acknowledge the window.
        if (studyCount == 0 && !hasUnresolvedDiscoveryEvent)
        {
            await _dataSourceStateService.UpdateLastSyncAsync(SourceName, windowEnd, null, ct)
                .ConfigureAwait(false);
        }
    }

    private async Task ManageBackfillAsync(CancellationToken ct)
    {
        var state = await _dataSourceStateService.GetStateAsync(SourceName, ct).ConfigureAwait(false);
        var status = state?.BackfillStatus;
        var snapshot = await _backfillCoordinator.GetChunkQueueSnapshotAsync(ct).ConfigureAwait(false);

        // Operator-requested full re-sync (POST /api/ingest/run?mode=full): replan every
        // window, ignoring completed-window coverage. When a sweep is already in flight it
        // covers the whole corpus, so the request is simply consumed.
        if (_forceFullSweep)
        {
            _forceFullSweep = false;
            if (status != "in-progress")
            {
                await PlanAndEnqueueSweepAsync(forceFull: true, ct).ConfigureAwait(false);
            }

            return;
        }

        if (status == "in-progress")
        {
            if (snapshot.PendingOrProcessing > 0)
            {
                // Sweep is running: keep the remaining-studies figure fresh for the UI.
                await UpdateBackfillRemainingAsync(state?.BackfillStartedUtc, ct).ConfigureAwait(false);
                return;
            }

            if (snapshot.DeadLettered > 0)
            {
                // Some chunks failed all retries — the sweep is incomplete. Mark it failed so
                // the next tick re-enqueues the uncovered windows (completed windows are skipped).
                await _dataSourceStateService.UpdateBackfillStateAsync(SourceName, "failed", null, null, null, ct).ConfigureAwait(false);
                return;
            }

            // All chunks completed (or none were ever enqueued because a previous sweep just
            // finished). The state update happens only AFTER enqueueing, so "in-progress" with
            // an empty queue means the sweep truly ran to completion. Reconcile and finish.
            if (state?.BackfillStartedUtc is DateTime sweepStart)
            {
                var removed = await _backfillCoordinator.ReconcileAndCompleteAsync(sweepStart, ct).ConfigureAwait(false);
                await _dataSourceStateService.UpdateBackfillStateAsync(SourceName, "complete", 0, sweepStart, DateTime.UtcNow, ct).ConfigureAwait(false);
            }
            return;
        }

        // Not in progress (idle, complete, or failed) — possibly (re)start a sweep.
        var totalAvailable = await _ctClient.CountStudiesAsync(cancellationToken: ct).ConfigureAwait(false);
        var totalInDb = await _repository.CountStudiesAsync(ct).ConfigureAwait(false);
        var gap = totalAvailable - totalInDb;

        if (status == "failed" && gap <= _backfillThreshold)
        {
            // Gap closed by other means (incremental) — nothing to backfill anymore.
            await _dataSourceStateService.UpdateBackfillStateAsync(SourceName, "complete", 0, state?.BackfillStartedUtc, DateTime.UtcNow, ct).ConfigureAwait(false);
            return;
        }

        if (gap <= _backfillThreshold)
        {
            return;
        }

        await PlanAndEnqueueSweepAsync(forceFull: false, ct).ConfigureAwait(false);
    }

    private async Task PlanAndEnqueueSweepAsync(bool forceFull, CancellationToken ct)
    {
        var totalAvailable = await _ctClient.CountStudiesAsync(cancellationToken: ct).ConfigureAwait(false);
        var totalInDb = await _repository.CountStudiesAsync(ct).ConfigureAwait(false);
        var gap = totalAvailable - totalInDb;

        var sweepStartedUtc = DateTime.UtcNow;
        var completedWindows = forceFull
            ? Array.Empty<(DateOnly From, DateOnly To)>()
            : await _backfillCoordinator.GetCompletedChunkWindowsAsync(ct).ConfigureAwait(false);
        var chunks = await BackfillChunkPlanner.PlanAsync(
            (from, to, ct2) => _ctClient.CountStudiesAsync(
                lastUpdatedPost: from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                lastUpdatedPostTo: to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                cancellationToken: ct2),
            _backfillEarliestDate,
            DateOnly.FromDateTime(DateTime.UtcNow),
            _backfillChunkMaxStudies,
            ct).ConfigureAwait(false);

        var enqueued = 0;
        foreach (var chunk in chunks)
        {
            if (!forceFull && BackfillWindowHelper.IsCovered((chunk.DateFrom, chunk.DateTo), completedWindows))
            {
                continue;
            }

            var eventData = JsonSerializer.Serialize(new
            {
                chunkIndex = enqueued,
                count = chunk.Count,
                dateFrom = chunk.DateFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                dateTo = chunk.DateTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                sweepStartedUtc
            });
            await _eventQueueService.EnqueueAsync("studies.backfill", eventData, ct).ConfigureAwait(false);
            enqueued++;
        }

        // State is marked in-progress AFTER enqueueing: a crash in between simply re-enqueues
        // on the next tick (idempotent upserts), and can never reconcile an empty sweep.
        await _dataSourceStateService.UpdateBackfillStateAsync(SourceName, "in-progress", gap, sweepStartedUtc, null, ct).ConfigureAwait(false);
    }

    private async Task UpdateBackfillRemainingAsync(DateTime? startedUtc, CancellationToken ct)
    {
        var totalAvailable = await _ctClient.CountStudiesAsync(cancellationToken: ct).ConfigureAwait(false);
        var totalInDb = await _repository.CountStudiesAsync(ct).ConfigureAwait(false);
        var remaining = Math.Max(0, totalAvailable - totalInDb);
        await _dataSourceStateService.UpdateBackfillStateAsync(SourceName, "in-progress", remaining, startedUtc, null, ct).ConfigureAwait(false);
    }
}

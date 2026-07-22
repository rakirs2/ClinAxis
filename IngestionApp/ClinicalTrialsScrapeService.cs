using Microsoft.Extensions.Hosting;
using Scrapers;
using Scrapers.Services.EventQueue;

namespace IngestionApp;

/// <summary>
/// Long-running background service that periodically scrapes ClinicalTrials.gov.
/// Runs every configured interval and fetches new/updated studies since last sync.
/// For each discovered study, enqueues a "studies.discovered" event for downstream processing.
/// </summary>
internal sealed class ClinicalTrialsScrapeService : BackgroundService
{
    private readonly IEventQueueService _eventQueueService;
    private readonly IDataSourceStateService _dataSourceStateService;
    private readonly int _scrapeIntervalMinutes;

    private const string SourceName = "ClinicalTrials.gov";
    private DateTime _lastRunTime = DateTime.MinValue;

    public ClinicalTrialsScrapeService(
        IEventQueueService eventQueueService,
        IDataSourceStateService dataSourceStateService,
        int scrapeIntervalMinutes = 60)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _dataSourceStateService = dataSourceStateService ?? throw new ArgumentNullException(nameof(dataSourceStateService));
        _scrapeIntervalMinutes = scrapeIntervalMinutes;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize data source state
        await _dataSourceStateService.InitializeSourceAsync(SourceName, stoppingToken).ConfigureAwait(false);

        using var httpClient = new HttpClient();
        var ctClient = new ClinicalTrialsGov(httpClient);

        // Run scrape loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var timeSinceLastRun = DateTime.UtcNow - _lastRunTime;

                if (timeSinceLastRun >= TimeSpan.FromMinutes(_scrapeIntervalMinutes))
                {
                    await PerformScrapeAsync(ctClient, stoppingToken).ConfigureAwait(false);
                    _lastRunTime = DateTime.UtcNow;
                }

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
                await _dataSourceStateService.SetStatusAsync(
                    SourceName,
                    "failed",
                    $"Scrape error: {ex.Message}",
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

        // Fetch new/updated studies from CT.gov API since last sync
        var studyCount = 0;
        await ctClient.GetTrialRecordsBatchedAsync(
            count: int.MaxValue,
            lastUpdatedPost: lastSyncTimestamp,
            onBatch: async batch =>
            {
                studyCount += batch.Count;
            },
            cancellationToken: ct).ConfigureAwait(false);

        if (studyCount > 0)
        {
            var eventData = System.Text.Json.JsonSerializer.Serialize(new { count = studyCount });
            await _eventQueueService.EnqueueAsync("studies.discovered", eventData, ct).ConfigureAwait(false);
        }

        // Update data source state
        await _dataSourceStateService.UpdateLastSyncAsync(
            SourceName,
            DateTime.UtcNow,
            null,
            ct).ConfigureAwait(false);

        await _dataSourceStateService.SetStatusAsync(SourceName, "idle", ct: ct).ConfigureAwait(false);
    }
}

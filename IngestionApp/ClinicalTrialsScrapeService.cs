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
    private readonly int _localDevelopmentStudyCount;
    private readonly bool _isDevelopment;

    private const string SourceName = "ClinicalTrials.gov";
    private DateTime _lastRunTime = DateTime.MinValue;

    public ClinicalTrialsScrapeService(
        IEventQueueService eventQueueService,
        IDataSourceStateService dataSourceStateService,
        int scrapeIntervalMinutes = 60,
        int localDevelopmentStudyCount = 1000,
        bool isDevelopment = false)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _dataSourceStateService = dataSourceStateService ?? throw new ArgumentNullException(nameof(dataSourceStateService));
        _scrapeIntervalMinutes = scrapeIntervalMinutes;
        _localDevelopmentStudyCount = localDevelopmentStudyCount;
        _isDevelopment = isDevelopment;
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

        // Get last sync timestamp
        var state = await _dataSourceStateService.GetStateAsync(SourceName, ct).ConfigureAwait(false);
        var lastSyncTimestamp = state?.LastSyncTimestamp ?? DateTime.MinValue;

        // For development, limit to configured study count
        var studyLimit = _isDevelopment ? _localDevelopmentStudyCount : int.MaxValue;

        // Fetch studies from CT.gov API using batched processing
        var studyCount = 0;
        await ctClient.GetTrialRecordsBatchedAsync(
            count: studyLimit,
            onBatch: async batch =>
            {
                // Enqueue event for each discovered study
                foreach (var study in batch)
                {
                    var eventData = System.Text.Json.JsonSerializer.Serialize(new { nctId = study.NctId });
                    await _eventQueueService.EnqueueAsync("studies.discovered", eventData, ct).ConfigureAwait(false);
                    studyCount++;
                }
            },
            cancellationToken: ct).ConfigureAwait(false);

        // Update data source state
        await _dataSourceStateService.UpdateLastSyncAsync(
            SourceName,
            DateTime.UtcNow,
            null,
            ct).ConfigureAwait(false);

        await _dataSourceStateService.SetStatusAsync(SourceName, "idle", ct: ct).ConfigureAwait(false);
    }
}

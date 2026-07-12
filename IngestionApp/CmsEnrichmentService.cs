using System.Net.Http.Headers;
using Microsoft.Extensions.Hosting;
using Scrapers.Persistence;
using Scrapers.Services.Cms;

namespace IngestionApp;

internal sealed class CmsEnrichmentService : BackgroundService
{
    private readonly string _connectionString;
    private readonly TimeSpan _interval;
    private static readonly HttpClient _httpClient = new();

    public CmsEnrichmentService(string connectionString, double intervalDays = 7)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _interval = TimeSpan.FromDays(intervalDays);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunEnrichmentCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CMS enrichment error: {ex.Message}");
            }

            await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
        }
    }

    public async Task RunEnrichmentCycleAsync(CancellationToken ct = default)
    {
        var repo = new StudyRepository(_connectionString);
        var orchestrator = new CmsEnrichmentOrchestrator(_connectionString);

        await repo.AddScrapeEventAsync(new Scrapers.Persistence.Entities.ScrapeEventEntity
        {
            Timestamp = DateTime.UtcNow,
            Source = "CmsMedicare",
            EventType = "enrichment.start",
            Level = "Info",
            Message = "Starting CMS Medicare enrichment cycle"
        }, ct).ConfigureAwait(false);

        var providers = await DownloadAndParseCmsCsvAsync(ct).ConfigureAwait(false);

        if (providers.Count == 0)
        {
            await repo.AddScrapeEventAsync(new Scrapers.Persistence.Entities.ScrapeEventEntity
            {
                Timestamp = DateTime.UtcNow,
                Source = "CmsMedicare",
                EventType = "enrichment.empty",
                Level = "Warning",
                Message = "No CMS provider data downloaded"
            }, ct).ConfigureAwait(false);
            return;
        }

        var imported = await orchestrator.ImportAndMatchProvidersAsync(providers, ct).ConfigureAwait(false);

        await repo.AddScrapeEventAsync(new Scrapers.Persistence.Entities.ScrapeEventEntity
        {
            Timestamp = DateTime.UtcNow,
            Source = "CmsMedicare",
            EventType = "enrichment.complete",
            Level = "Info",
            Message = $"Imported {imported} CMS providers, matched against investigators by NPI",
            RecordsAffected = imported
        }, ct).ConfigureAwait(false);
    }

    internal static async Task<IReadOnlyList<Scrapers.Persistence.Entities.CmsProviderEntity>> DownloadAndParseCmsCsvAsync(CancellationToken ct = default)
    {
        var csvUrl = Environment.GetEnvironmentVariable("CMS_CSV_URL")
            ?? "https://data.cms.gov/resource/s2s5-8z7w.csv";

        using var request = new HttpRequestMessage(HttpMethod.Get, csvUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/csv"));
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await CmsMedicareScraper.ParseProviderCsvAsync(stream, ct).ConfigureAwait(false);
    }
}

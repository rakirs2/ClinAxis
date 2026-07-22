using System.Reflection;
using IngestionApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Services;
using Scrapers.Services.CrawlServices;
using Scrapers.Services.Enrichment;
using Scrapers.Services.EventQueue;

if (args.Contains("--version") || args.Contains("-v"))
{
    var assembly = System.Reflection.Assembly.GetEntryAssembly()!;
    var version = assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    var infoVersion = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;
    await Console.Out.WriteLineAsync($"{assembly.GetName().Name} {version} ({infoVersion})").ConfigureAwait(false);
    return 0;
}

var rawCs = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(rawCs))
{
    await Console.Error.WriteLineAsync("POSTGRES_CONNECTION_STRING not set.").ConfigureAwait(false);
    return 1;
}
var cs = ConnectionStringProvider.WithPoolLimits(rawCs, maxPoolSize: 10);

// Optionally reset database (set INGESTION_RESET_DB=true for fresh state on dev redeploy)
if (Environment.GetEnvironmentVariable("INGESTION_RESET_DB") == "true")
{
    var repo = new StudyRepository(cs);
    await repo.ResetDatabaseAsync().ConfigureAwait(false);
}

// Build the host for long-running background services
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Event queue infrastructure
        services.AddSingleton<IEventQueueService>(new EventQueueService(cs));
        services.AddSingleton<IDataSourceStateService>(new DataSourceStateService(cs));
        services.AddSingleton<ISourceFetchHistoryService>(new SourceFetchHistoryService(cs));

        // Persistence
        services.AddSingleton<StudyRepository>(new StudyRepository(cs));

        // ClinicalTrials.gov ingestion pipeline
        services.AddSingleton<ClinicalTrialsGov>();
        services.AddSingleton<ClinicalTrialsIngestionService>();

        // Pivot services
        services.AddSingleton<PivotServiceRegistry>();
        services.AddSingleton<PivotConfigurationService>(new PivotConfigurationService(cs));

        // Background services for scraping and event processing
        services.AddHostedService(sp => new ClinicalTrialsScrapeService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<IDataSourceStateService>(),
            scrapeIntervalMinutes: 60));

        services.AddHostedService(sp => new EventProcessingService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<ClinicalTrialsIngestionService>(),
            pollIntervalSeconds: 10,
            claimedEventTimeoutMinutes: 30));

        services.AddHostedService(sp => new DeadLetterProcessingService(
            sp.GetRequiredService<IEventQueueService>(),
            checkIntervalMinutes: 5));

        services.AddHostedService(sp => new InvestigatorPublicationScrubService(
            cs,
            sp.GetRequiredService<ILogger<InvestigatorPublicationScrubService>>(),
            sp.GetRequiredService<IEventQueueService>()));

        // Enrichment services (NPI lookup via NPPES NPI Registry, ORCID API for disambiguation)
        services.AddSingleton<NppesNpiRegistryClient>(_ => new NppesNpiRegistryClient(new HttpClient()));
        services.AddSingleton<OrcidApiClient>(_ => new OrcidApiClient(new HttpClient()));
        services.AddHostedService(sp => new InvestigatorEnrichmentService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<NppesNpiRegistryClient>(),
            sp.GetRequiredService<OrcidApiClient>(),
            cs,
            pollIntervalSeconds: 30));

        // Medicare Utilization enrichment
        var cmsBaseUrl = Environment.GetEnvironmentVariable("CMS_MEDICARE_BASE_URL") ?? "https://data.cms.gov/data-api/v1/dataset/";
        var cmsDatasetUuid = Environment.GetEnvironmentVariable("CMS_MEDICARE_DATASET_UUID") ?? "8889d81e-2ee7-448f-8713-f071038289b5";
        var medicareDataYear = int.TryParse(Environment.GetEnvironmentVariable("MEDICARE_DATA_YEAR"), out var my) ? my : 0;
        services.AddSingleton<CmsMedicareClient>(_ => new CmsMedicareClient(
            new HttpClient { BaseAddress = new Uri(cmsBaseUrl) },
            datasetUuid: cmsDatasetUuid));
        services.AddHostedService(sp => new MedicareUtilizationService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<CmsMedicareClient>(),
            cs,
            pollIntervalSeconds: 30,
            dataYear: medicareDataYear));

        // CMS Open Payments (Sunshine Act) enrichment
        services.AddSingleton<CmsOpenPaymentsClient>(_ => new CmsOpenPaymentsClient(new HttpClient()));
        services.AddHostedService(sp => new OpenPaymentsService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<CmsOpenPaymentsClient>(),
            cs,
            pollIntervalSeconds: 30));

        // Investigator Metrics enrichment (Semantic Scholar h-index and citations)
        services.AddSingleton<SemanticScholarClient>(_ => new SemanticScholarClient(new HttpClient()));
        services.AddHostedService(sp => new InvestigatorMetricsService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<SemanticScholarClient>(),
            cs,
            pollIntervalSeconds: 30));
    })
    .Build();

// Run the host (blocking call, runs until cancelled)
await host.RunAsync().ConfigureAwait(false);

return 0;

using System.Reflection;
using IngestionApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Services;
using Scrapers.Services.Enrichment;
using Scrapers.Services.EventQueue;
using Scrapers.Utilities;

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
    .ConfigureLogging(logging =>
    {
        // Structured JSON console logs (machine-parseable in docker logs).
        // Levels are configurable at runtime via Logging__LogLevel__* env vars,
        // e.g. Logging__LogLevel__IngestionApp=Debug (see docs/README.md).
        logging.AddJsonConsole();
    })
    .ConfigureServices(services =>
    {
        // Event queue infrastructure
        var backfillClaimTimeoutHours = int.TryParse(Environment.GetEnvironmentVariable("BACKFILL_CLAIM_TIMEOUT_HOURS"), out var backfillClaimTimeout)
            ? backfillClaimTimeout
            : 12;
        services.AddSingleton<IEventQueueService>(new EventQueueService(cs, backfillClaimTimeoutHours));
        services.AddSingleton<IDataSourceStateService>(new DataSourceStateService(cs));
        services.AddSingleton<INgestionProgressReporter>(new ScrapeEventProgressReporter(cs, "ClinicalTrials.gov"));

        // MeSH Matcher for A/B testing
        MeSHMatcher? meshMatcher = null;
        var meshResourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources", "mesh");
        if (Directory.Exists(meshResourcesPath) && File.Exists(Path.Combine(meshResourcesPath, "model.onnx")))
        {
            Console.WriteLine($"  [MeSH] Loading matcher from {meshResourcesPath}");
            try
            {
                meshMatcher = new MeSHMatcher(meshResourcesPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [MeSH] Failed to load matcher: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("  [MeSH] Resources not found, A/B test disabled");
        }

        // Persistence
        services.AddSingleton<StudyRepository>(meshMatcher != null
            ? new StudyRepository(cs, meshMatcher)
            : new StudyRepository(cs));

        // ClinicalTrials.gov ingestion pipeline
        services.AddSingleton<ClinicalTrialsGov>();
        services.AddSingleton<ClinicalTrialsIngestionService>();

        // Background services for scraping and event processing
        var ingestTimeoutMinutes = int.TryParse(Environment.GetEnvironmentVariable("INGEST_TIMEOUT_MINUTES"), out var ingestTimeout)
            ? ingestTimeout
            : 60;
        var backfillIngestTimeoutHours = int.TryParse(Environment.GetEnvironmentVariable("BACKFILL_INGEST_TIMEOUT_HOURS"), out var backfillIngestTimeout)
            ? backfillIngestTimeout
            : 10;

        services.AddHostedService(sp => new ClinicalTrialsScrapeService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<IDataSourceStateService>(),
            sp.GetRequiredService<INgestionProgressReporter>(),
            scrapeIntervalMinutes: 60));

        services.AddHostedService(sp => new EventProcessingService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<ClinicalTrialsIngestionService>(),
            pollIntervalSeconds: 10,
            claimedEventTimeoutMinutes: 30,
            ingestTimeoutMinutes: ingestTimeoutMinutes,
            backfillIngestTimeoutHours: backfillIngestTimeoutHours));

        services.AddHostedService(sp => new DeadLetterProcessingService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<ILogger<DeadLetterProcessingService>>(),
            checkIntervalMinutes: 5));

        services.AddHostedService(sp => new InvestigatorPublicationScrubService(
            cs,
            sp.GetRequiredService<ILogger<InvestigatorPublicationScrubService>>(),
            sp.GetRequiredService<IEventQueueService>()));

        // Enrichment services (NPI lookup via NPPES NPI Registry, ORCID API for disambiguation)
        services.AddSingleton<NppesNpiRegistryClient>(_ => new NppesNpiRegistryClient(new HttpClient()));
        services.AddSingleton<OrcidApiClient>(_ => new OrcidApiClient(new HttpClient()));

        // NPI disambiguation ML model (A/B recording — the rule scorer stays authoritative)
        var npiModelPath = Path.Combine(AppContext.BaseDirectory, "Resources", "npi-model");
        var npiModelService = NpiModelService.TryLoad(npiModelPath);
        if (npiModelService != null)
        {
            Console.WriteLine($"  [NPI Model] Loaded from {npiModelPath}, ModelScore recording enabled");
        }
        else
        {
            Console.WriteLine("  [NPI Model] Resources not found or invalid, ModelScore recording disabled");
        }

        services.AddHostedService(sp => new InvestigatorEnrichmentService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<NppesNpiRegistryClient>(),
            sp.GetRequiredService<OrcidApiClient>(),
            npiModelService,
            cs,
            sp.GetRequiredService<ILogger<InvestigatorEnrichmentService>>(),
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
            sp.GetRequiredService<ILogger<MedicareUtilizationService>>(),
            pollIntervalSeconds: 30,
            dataYear: medicareDataYear));

        // CMS Open Payments (Sunshine Act) enrichment
        services.AddSingleton<CmsOpenPaymentsClient>(_ => new CmsOpenPaymentsClient(new HttpClient()));
        services.AddHostedService(sp => new OpenPaymentsService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<CmsOpenPaymentsClient>(),
            cs,
            sp.GetRequiredService<ILogger<OpenPaymentsService>>(),
            pollIntervalSeconds: 30));

        // Investigator Metrics enrichment (Semantic Scholar h-index and citations)
        services.AddSingleton<SemanticScholarClient>(_ => new SemanticScholarClient(new HttpClient()));
        services.AddHostedService(sp => new InvestigatorMetricsService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<SemanticScholarClient>(),
            cs,
            sp.GetRequiredService<ILogger<InvestigatorMetricsService>>(),
            pollIntervalSeconds: 30));
    })
    .Build();

// Run the host (blocking call, runs until cancelled)
await host.RunAsync().ConfigureAwait(false);

return 0;

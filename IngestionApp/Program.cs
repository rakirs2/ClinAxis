using IngestionApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Services;
using Scrapers.Services.CrawlServices;
using Scrapers.Services.EventQueue;

var cs = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(cs))
{
    await Console.Error.WriteLineAsync("POSTGRES_CONNECTION_STRING not set.").ConfigureAwait(false);
    return 1;
}

var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

// Ensure database schema is created
var repo = new StudyRepository(cs);
await repo.EnsureSchemaAsync().ConfigureAwait(false);

// Build the host for long-running background services
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Event queue infrastructure
        services.AddSingleton<IEventQueueService>(new EventQueueService(cs));
        services.AddSingleton<IDataSourceStateService>(new DataSourceStateService(cs));
        services.AddSingleton<ISourceFetchHistoryService>(new SourceFetchHistoryService(cs));

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
            scrapeIntervalMinutes: isDevelopment ? 60 : 60,
            localDevelopmentStudyCount: 1000,
            isDevelopment: isDevelopment));

        services.AddHostedService(sp => new EventProcessingService(
            sp.GetRequiredService<IEventQueueService>(),
            sp.GetRequiredService<ClinicalTrialsIngestionService>(),
            pollIntervalSeconds: 10,
            claimedEventTimeoutMinutes: 30));

        services.AddHostedService(sp => new DeadLetterProcessingService(
            sp.GetRequiredService<IEventQueueService>(),
            checkIntervalMinutes: 5));
    })
    .Build();

// Run the host (blocking call, runs until cancelled)
await host.RunAsync().ConfigureAwait(false);

return 0;

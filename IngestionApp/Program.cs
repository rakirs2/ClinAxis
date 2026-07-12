using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers;
using Scrapers.CrawlServices;
using Scrapers.Persistence;
using Scrapers.Services;

var cs = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(cs))
{
    await Console.Error.WriteLineAsync("POSTGRES_CONNECTION_STRING not set.").ConfigureAwait(false);
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);

var logDebugMessage = LoggerMessage.Define<string>(LogLevel.Debug, 0, "{Message}");

builder.Services.AddSingleton(new StudyRepository(cs));
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ClinicalTrialsGov>>();
    return new ClinicalTrialsGov(
        httpClient: new HttpClient(),
        log: msg => logDebugMessage(logger, msg, null));
});
builder.Services.AddSingleton<ClinicalTrialsIngestionService>();
builder.Services.AddSingleton(new PubMedScraperService(cs));
builder.Services.AddSingleton<AggregationService>();

builder.Services.AddHostedService<ClinicalTrialsCrawlService>();
builder.Services.AddHostedService<PubMedCrawlService>();
builder.Services.AddHostedService<AggregationCrawlService>();

await builder.Build().RunAsync().ConfigureAwait(false);
return 0;

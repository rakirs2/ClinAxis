using System.Diagnostics;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services;

var cs = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(cs))
{
    await Console.Error.WriteLineAsync("POSTGRES_CONNECTION_STRING not set.");
    return 1;
}

var count = args.Length > 0 && int.TryParse(args[0], out var n) ? n : int.MaxValue;

var repo = new StudyRepository(cs);
await repo.EnsureSchemaAsync();

var run = new PipelineRunEntity
{
    StartedAt = DateTime.UtcNow,
    Status = "Running"
};
var runId = await repo.AddPipelineRunAsync(run);
var sw = Stopwatch.StartNew();

async Task RecordEvent(string source, string eventType, string level, int? records = null, long? durationMs = null, string? message = null, int? httpStatus = null)
{
    await repo.AddScrapeEventAsync(new ScrapeEventEntity
    {
        PipelineRunId = runId,
        Timestamp = DateTime.UtcNow,
        Source = source,
        EventType = eventType,
        Level = level,
        DurationMs = durationMs,
        RecordsAffected = records,
        Message = message,
        HttpStatusCode = httpStatus
    });
}

await RecordEvent("System", "PipelineStart", "Info", message: $"Starting ingestion with count={count}");

try
{
    using var httpClient = new HttpClient();
    var clinicalTrialsClient = new ClinicalTrialsGov(httpClient, log: msg => Console.WriteLine($"  [CT] {msg}"));
    var ingestion = new ClinicalTrialsIngestionService(clinicalTrialsClient, repo);
    var ingested = await ingestion.IngestAsync(count, CancellationToken.None);
    await RecordEvent("ClinicalTrials", "RecordsIngested", "Info", records: ingested, message: $"Ingested {ingested} studies");
    await Console.Out.WriteLineAsync($"Ingested {ingested} studies.");

    var pubmed = new PubMedScraperService(cs);
    var pubmedCount = await pubmed.IngestPubMedPapersAsync(CancellationToken.None);
    await RecordEvent("PubMed", "RecordsIngested", "Info", records: pubmedCount, message: $"Stored {pubmedCount} PubMed papers");
    await Console.Out.WriteLineAsync($"Stored {pubmedCount} PubMed papers.");

    var agg = new AggregationService(repo);
    await agg.AggregateAsync(CancellationToken.None);
    await RecordEvent("Aggregation", "Complete", "Info", message: "Aggregations complete");
    await Console.Out.WriteLineAsync("Aggregations complete.");

    var studies = await repo.CountStudiesAsync();
    var investigators = await repo.CountInvestigatorsAsync();
    var pubmedPapers = await repo.CountPubmedStudiesAsync();
    var keywords = await repo.CountKeywordsAsync();
    var authors = await repo.CountAuthorsAsync();
    var piAggs = await repo.CountPiAggregationsAsync();

    sw.Stop();
    await repo.CompletePipelineRunAsync(runId, "Completed", studies, investigators, pubmedPapers, keywords, authors);
    await RecordEvent("System", "PipelineComplete", "Info", message: $"Finished in {sw.Elapsed.TotalMinutes:F1}min");

    await Console.Out.WriteLineAsync($"DB: {studies} studies, {investigators} investigators, {pubmedPapers} PubMed papers, {keywords} keywords, {authors} authors, {piAggs} PI aggregations.");
    await Console.Out.WriteLineAsync($"Duration: {sw.Elapsed.TotalMinutes:F1} minutes.");
    await Console.Out.WriteLineAsync("Done.");
    return 0;
}
catch (Exception ex)
{
    sw.Stop();
    await repo.CompletePipelineRunAsync(runId, "Failed", errorMessage: ex.ToString());
    await RecordEvent("System", "PipelineFailed", "Error", message: ex.Message);
    await Console.Error.WriteLineAsync($"FAILED: {ex}");
    return 1;
}

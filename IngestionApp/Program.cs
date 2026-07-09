using Scrapers;
using Scrapers.Persistence;
using Scrapers.Services;

var cs = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(cs))
{
    await Console.Error.WriteLineAsync("POSTGRES_CONNECTION_STRING not set.");
    return 1;
}

var count = args.Length > 0 && int.TryParse(args[0], out var n) ? n : 50;

var repo = new StudyRepository(cs);
await repo.EnsureSchemaAsync();
await repo.ClearAsync();

using var httpClient = new HttpClient();
var client = new ClinicalTrialsGov(httpClient);
var ingestion = new ClinicalTrialsIngestionService(client, repo);
var ingested = await ingestion.IngestAsync(count, CancellationToken.None);
await Console.Out.WriteLineAsync($"Ingested {ingested} studies.");

var pubmed = new PubMedScraperService(cs);
var pubmedCount = await pubmed.IngestPubMedPapersAsync(CancellationToken.None);
await Console.Out.WriteLineAsync($"Stored {pubmedCount} PubMed papers.");

var agg = new AggregationService(repo);
await agg.AggregateAsync(CancellationToken.None);
await Console.Out.WriteLineAsync("Aggregations complete.");

var studies = await repo.CountStudiesAsync();
var investigators = await repo.CountInvestigatorsAsync();
var pubmedPapers = await repo.CountPubmedStudiesAsync();
var piAggs = await repo.CountPiAggregationsAsync();
await Console.Out.WriteLineAsync($"DB: {studies} studies, {investigators} investigators, {pubmedPapers} PubMed papers, {piAggs} PI aggregations.");
await Console.Out.WriteLineAsync("Done.");
return 0;

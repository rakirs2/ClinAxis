using DataApi;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5003");

var connectionString = ConnectionStringProvider.Default;

var startupRepo = new StudyRepository(connectionString);
await startupRepo.EnsureSchemaAsync();

builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

app.MapHealthChecks("/health");

// New endpoints for autocomplete dropdowns
app.MapGet("/api/distinct-conditions", async () =>
{
    var repo = new StudyRepository(connectionString);
    var conditions = await repo.GetDistinctConditionsAsync();
    return Results.Ok(new { data = conditions });
});

app.MapGet("/api/distinct-locations", async () =>
{
    var repo = new StudyRepository(connectionString);
    var (countries, states, cities, facilities) = await repo.GetDistinctLocationsAsync();
    return Results.Ok(new
    {
        countries,
        states,
        cities,
        facilities
    });
});

// Enhanced search endpoint - accepts all StudySearchCriteria parameters
app.MapGet("/api/studies", async (
    int? page, int? pageSize,
    string? search, string? status, string? phase,  // Legacy params
    string? condition, string? country, string? state, string? city, string? facility,
    int? enrollmentMin, int? enrollmentMax,
    DateTime? startDateFrom, DateTime? startDateTo) =>
{
    var repo = new StudyRepository(connectionString);
    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 100);

    // Build StudySearchCriteria from query parameters
    var criteria = new StudySearchCriteria
    {
        Keyword = search,
        Statuses = string.IsNullOrEmpty(status) ? null : status.Split(',').Select(s => s.Trim()).ToList(),
        Phases = string.IsNullOrEmpty(phase) ? null : phase.Split(',').Select(p => p.Trim()).ToList(),
        Conditions = string.IsNullOrEmpty(condition) ? null : condition.Split(',').Select(c => c.Trim()).ToList(),
        Countries = string.IsNullOrEmpty(country) ? null : country.Split(',').Select(c => c.Trim()).ToList(),
        States = string.IsNullOrEmpty(state) ? null : state.Split(',').Select(s => s.Trim()).ToList(),
        Cities = string.IsNullOrEmpty(city) ? null : city.Split(',').Select(c => c.Trim()).ToList(),
        Facilities = string.IsNullOrEmpty(facility) ? null : facility.Split(',').Select(f => f.Trim()).ToList(),
        EnrollmentMin = enrollmentMin,
        EnrollmentMax = enrollmentMax,
        StartDateFrom = startDateFrom,
        StartDateTo = startDateTo,
        Page = p,
        PageSize = ps
    };

    IReadOnlyList<StudyEntity> studies = await repo.SearchStudiesAsync(criteria);
    var total = await repo.CountStudiesFilteredAsync(criteria);

    return Results.Ok(new
    {
        data = studies.Select(s => StudyMapper.ToSummary(s)),
        total,
        page = p,
        pageSize = ps,
        totalPages = (int)Math.Ceiling((double)total / ps)
    });
});

app.MapGet("/api/studies/{nctId}", async (string nctId) =>
{
    var repo = new StudyRepository(connectionString);
    StudyEntity? study = await repo.GetStudyByNctIdAsync(nctId);
    return study is null ? Results.NotFound(new { error = "Study not found" }) : Results.Ok(StudyMapper.ToDetail(study));
});

app.MapGet("/api/investigators", async (int? page, int? pageSize, string? search) =>
{
    var repo = new StudyRepository(connectionString);
    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 100);

    IReadOnlyList<InvestigatorSummary> investigators = await repo.GetInvestigatorsPagedAsync(p, ps, search);
    var total = await repo.CountInvestigatorsFilteredAsync(search);

    return Results.Ok(new
    {
        data = investigators,
        total,
        page = p,
        pageSize = ps,
        totalPages = (int)Math.Ceiling((double)total / ps)
    });
});

app.MapGet("/api/pipeline-runs", async (int? page, int? pageSize) =>
{
    var repo = new StudyRepository(connectionString);
    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 100);
    List<PipelineRunEntity> runs = await repo.GetPipelineRunsAsync(p, ps);
    return Results.Ok(new { data = runs.Select(r => StudyMapper.ToPipelineRun(r)) });
});

app.MapGet("/api/stats", async () =>
{
    var repo = new StudyRepository(connectionString);
    var studies = await repo.CountStudiesAsync();
    var investigators = await repo.CountInvestigatorsAsync();
    var pubmedPapers = await repo.CountPubmedStudiesAsync();
    var keywords = await repo.CountKeywordsAsync();
    var authors = await repo.CountAuthorsAsync();
    return Results.Ok(new
    {
        totalStudies = studies,
        totalInvestigators = investigators,
        totalPubmedPapers = pubmedPapers,
        totalKeywords = keywords,
        totalAuthors = authors
    });
});

app.MapGet("/api/telemetry", async () =>
{
    var repo = new StudyRepository(connectionString);

    var studies = await repo.CountStudiesAsync();
    var investigators = await repo.CountInvestigatorsAsync();
    var pubmedPapers = await repo.CountPubmedStudiesAsync();
    var keywords = await repo.CountKeywordsAsync();
    var authors = await repo.CountAuthorsAsync();

    List<PipelineRunEntity> recentRuns = await repo.GetPipelineRunsAsync(1, 5);
    IReadOnlyList<ScrapeEventEntity> recentEvents = await repo.GetRecentScrapeEventsAsync(20);

    var piCount = await repo.CountPiAggregationsAsync();
    IReadOnlyList<CategoryTypeCount> categoryByType = await repo.CountCategoryAggregationsByTypeAsync();

    return Results.Ok(new
    {
        db = new
        {
            totalStudies = studies,
            totalInvestigators = investigators,
            totalPubmedPapers = pubmedPapers,
            totalKeywords = keywords,
            totalAuthors = authors
        },
        pipelineRuns = recentRuns.Select(r => StudyMapper.ToPipelineRun(r)),
        recentEvents = recentEvents.Select(e => new
        {
            id = e.Id,
            pipelineRunId = e.PipelineRunId,
            timestamp = e.Timestamp,
            source = e.Source,
            eventType = e.EventType,
            level = e.Level,
            durationMs = e.DurationMs,
            recordsAffected = e.RecordsAffected,
            message = e.Message,
            httpStatusCode = e.HttpStatusCode
        }),
        aggregations = new
        {
            piAggregationCount = piCount,
            categoryAggregationCount = categoryByType.Sum(c => c.Count)
        }
    });
});

app.MapGet("/api/aggregations", async () =>
{
    var repo = new StudyRepository(connectionString);
    var piCount = await repo.CountPiAggregationsAsync();
    IReadOnlyList<CategoryTypeCount> categoryByType = await repo.CountCategoryAggregationsByTypeAsync();
    Dictionary<string, int> categoryDict = new();
    foreach (CategoryTypeCount c in categoryByType)
    {
        categoryDict[c.CategoryType] = c.Count;
    }
    return Results.Ok(new
    {
        piAggregationCount = piCount,
        categoryAggregationCount = categoryByType.Sum(c => c.Count),
        categoryAggregationsByType = categoryDict
    });
});

await app.RunAsync();

namespace DataApi
{
    partial class Program { }
}
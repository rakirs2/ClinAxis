using DataApi;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5000");

var connectionString = ConnectionStringProvider.Default;

WebApplication app = builder.Build();

app.MapGet("/api/studies", async (int? page, int? pageSize, string? search, string? status, string? phase) =>
{
    var repo = new StudyRepository(connectionString);
    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 100);

    IReadOnlyList<StudyEntity> studies = await repo.GetStudiesPagedAsync(p, ps, search, status, phase);
    var total = await repo.CountStudiesFilteredAsync(search, status, phase);

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

app.Run();
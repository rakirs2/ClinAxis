using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Scrapers;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services;

var cs = ConnectionStringProvider.WithPoolLimits(ConnectionStringProvider.Default);

Console.WriteLine("Initializing database schema ...");
var repo = new StudyRepository(cs);
await repo.MigrateSchemaAsync();

var resourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources", "mesh");
if (!Directory.Exists(resourcesPath))
{
    resourcesPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "..", "..", "..", "..", "..",
        "Scrapers", "Resources", "mesh");
}

if (!File.Exists(Path.Combine(resourcesPath, "model.onnx")))
{
    Console.Error.WriteLine("ERROR: model.onnx not found. Run export_sbert_onnx.py first.");
    return 1;
}

Console.WriteLine("Loading MeSH matcher ...");
using var matcher = new MeSHMatcher(resourcesPath);

var apiBase = new Uri("https://clinicaltrials.gov/api/v2/studies?format=json&pageSize=100");
var totalDesired = 100;
var pageToken = "";
var allRecords = new List<ClinicalTrialRecord>();
using var http = new HttpClient();
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

Console.WriteLine($"Fetching up to {totalDesired} studies from CT.gov ...");
while (allRecords.Count < totalDesired)
{
    var url = apiBase.ToString();
    if (!string.IsNullOrEmpty(pageToken))
        url += $"&pageToken={pageToken}";

    var response = await http.GetStringAsync(new Uri(url));
    var doc = JsonDocument.Parse(response);
    var root = doc.RootElement;

    var studies = root.GetProperty("studies").EnumerateArray();

    foreach (var studyEl in studies)
    {
        var payload = JsonSerializer.Deserialize<StudyListResponse.StudyPayload>(
            studyEl.GetRawText(), jsonOptions);

        if (payload != null)
        {
            var record = payload.ToRecord();
            allRecords.Add(record);
            if (allRecords.Count >= totalDesired) break;
        }
    }

    if (root.TryGetProperty("nextPageToken", out var next))
    {
        pageToken = next.GetString() ?? "";
        if (string.IsNullOrEmpty(pageToken)) break;
    }
    else
    {
        break;
    }

    Console.WriteLine($"  Fetched {allRecords.Count}/{totalDesired} ...");
    await Task.Delay(350);
}

Console.WriteLine($"\nProcessing {allRecords.Count} studies through A/B test ...");
var meshResults = new List<Scrapers.Models.MeSHMatchResult>();

foreach (var record in allRecords)
{
    if (record.Conditions != null)
    {
        foreach (var cond in record.Conditions)
        {
            if (!string.IsNullOrWhiteSpace(cond))
            {
                var match = matcher.Match(cond.Trim(), "condition", record.NctId!);
                meshResults.Add(match);
            }
        }
    }
    if (record.Keywords != null)
    {
        foreach (var kw in record.Keywords)
        {
            if (!string.IsNullOrWhiteSpace(kw))
            {
                var match = matcher.Match(kw.Trim(), "keyword", record.NctId!);
                meshResults.Add(match);
            }
        }
    }
}

Console.WriteLine($"\nStoring {meshResults.Count} results in database ...");
using var ctx = new ClinicalTrialsContext(
    new DbContextOptionsBuilder<ClinicalTrialsContext>()
        .ConfigureNpgsql(cs).Options);

foreach (var m in meshResults)
{
    ctx.RejectedTerms.Add(new RejectedTermEntity
    {
        StudyNctId = m.StudyNctId,
        Value = m.Value,
        Source = m.Source,
        SideBMatched = m.SideBMatched,
        SideBMeshTerm = m.MeshTerm,
        SideBMeshCui = m.MeshCui,
        SideBCategory = m.Category,
        SideBSimilarity = m.Similarity,
        Accepted = m.Accepted,
        RejectionReason = m.RejectionReason,
        CreatedAt = DateTime.UtcNow,
    });
}

await ctx.SaveChangesAsync();

Console.WriteLine("\n--- A/B Test Results ---");
var accepted = meshResults.Count(m => m.Accepted);
var sideB = meshResults.Count(m => m.SideBMatched);
var disease = meshResults.Count(m => m.Category == "disease" && m.SideBMatched);
var nonDisease = meshResults.Count(m => m.Category != "disease" && m.Category != "unmapped" && m.SideBMatched);
var unmatched = meshResults.Count(m => !m.SideBMatched);

Console.WriteLine($"Total terms:          {meshResults.Count}");
Console.WriteLine($"Side B (MeSH match): {sideB}");
Console.WriteLine($"Accepted (disease):  {accepted}");
Console.WriteLine($"  Disease matched:   {disease}");
Console.WriteLine($"  Non-disease:       {nonDisease}");
Console.WriteLine($"  Unmatched (<0.8):  {unmatched}");

Console.WriteLine($"\nCategory breakdown:");
foreach (var g in meshResults.Where(m => m.SideBMatched).GroupBy(m => m.Category).OrderByDescending(g => g.Count()))
{
    Console.WriteLine($"  {g.Key}: {g.Count()}");
}

Console.WriteLine($"\nDone. View results at http://localhost:5003/api/ab-test/stats or frontend /ab-test");
return 0;

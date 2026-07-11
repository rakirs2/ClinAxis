using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class DataApiSearchLiveTests
{
    private StudyRepository _repo = null!;
    private static readonly HttpClient Client = new();

    // Need to determine HttpLive status — these tests require DataApi running on :5003.
    // Planned fix (PR 7, #34): use WebApplicationFactory<Program> + Testcontainers.PostgreSql
    // so they self-host in-process and work with a single Run click.
    [TestInitialize]
    public async Task InitializeAsync()
    {
        _repo = new StudyRepository(ConnectionStringProvider.Default);
        await _repo.EnsureSchemaAsync();
    }

    [TestMethod]
    [TestCategory("HttpLive")]
    [Ignore("Need to determine HttpLive status — see issue #34")]
    public async Task SearchStudiesViaApi_ReturnsMatchingResults()
    {
        var record = new ClinicalTrialRecord
        {
            NctId = "NCT09999001",
            BriefTitle = "Liver Cancer Immunotherapy Trial Phase III",
            OverallStatus = "RECRUITING",
            OverallOfficials = new List<Investigator>
            {
                new() { Name = "Dr. Smith", Role = "PRINCIPAL_INVESTIGATOR" }
            }
        };
        await _repo.UpdateStudiesWithClinicalTrialsAsync(new[] { record });

        using HttpResponseMessage response = await Client.GetAsync(
            new Uri("http://localhost:5003/api/studies?search=liver+cancer&page=1&pageSize=10"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");
        Assert.IsTrue(data.GetArrayLength() > 0, "Search should return at least one result");

        var found = data.EnumerateArray().Any(s =>
            s.GetProperty("nctId").GetString() == "NCT09999001");
        Assert.IsTrue(found, "Inserted study should appear in search results");
    }

    [TestMethod]
    [TestCategory("HttpLive")]
    [Ignore("Need to determine HttpLive status — see issue #34")]
    public async Task SearchStudiesViaApi_EmptySearchReturnsStudies()
    {
        using var response = await Client.GetAsync(
            new Uri("http://localhost:5003/api/studies?page=1&pageSize=5"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.IsTrue(doc.RootElement.TryGetProperty("data", out JsonElement data));
        Assert.IsTrue(data.GetArrayLength() > 0, "Empty search should return studies");
        Assert.IsTrue(doc.RootElement.TryGetProperty("total", out JsonElement total));
        Assert.IsTrue(total.GetInt32() > 0, "Total should be > 0");
    }

    [TestMethod]
    [TestCategory("HttpLive")]
    [Ignore("Need to determine HttpLive status — see issue #34")]
    public async Task SearchStudiesViaApi_ResponseShapeMatchesSchema()
    {
        using var response = await Client.GetAsync(
            new Uri("http://localhost:5003/api/studies?search=cancer&page=1&pageSize=1"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var root = doc.RootElement;
        Assert.IsTrue(root.TryGetProperty("data", out JsonElement data));
        Assert.IsTrue(root.TryGetProperty("total", out _));
        Assert.IsTrue(root.TryGetProperty("page", out _));
        Assert.IsTrue(root.TryGetProperty("pageSize", out _));
        Assert.IsTrue(root.TryGetProperty("totalPages", out _));

        if (data.GetArrayLength() > 0)
        {
            var study = data[0];
            Assert.IsTrue(study.TryGetProperty("nctId", out _));
            Assert.IsTrue(study.TryGetProperty("briefTitle", out _));
            Assert.IsTrue(study.TryGetProperty("overallStatus", out _));
            Assert.IsTrue(study.TryGetProperty("investigatorCount", out _));
            Assert.IsTrue(study.TryGetProperty("pubmedPaperCount", out _));
        }
    }
}

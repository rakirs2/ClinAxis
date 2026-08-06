using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class DataApiSmokeTests
{
    private SnapshotDb _snapshot = null!;
    private WebApplicationFactory<DataApi.Program> _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Init()
    {
        _snapshot = new SnapshotDb();
        Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", _snapshot.ConnectionString);
        _factory = new WebApplicationFactory<DataApi.Program>();
        _client = _factory.CreateClient();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _snapshot.DisposeAsync();
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task HealthEndpoint_Returns200WithStatusAndVersion()
    {
        using HttpResponseMessage response = await _client.GetAsync("/health");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("DataApi", doc.RootElement.GetProperty("application").GetString());
        Assert.IsTrue(doc.RootElement.TryGetProperty("version", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("informationalVersion", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("framework", out _));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchByTitle_WithSeededData_ReturnsMatchingResult()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/studies?keyword=Pregabalin&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");
        Assert.IsTrue(data.GetArrayLength() > 0, "Search should return at least one result");

        var found = data.EnumerateArray().Any(s => s.GetProperty("nctId").GetString() == "NCT00000002");
        Assert.IsTrue(found, "Seeded Pregabalin study should appear in search results");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task InvestigatorFinder_ReturnsScoredResults()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/investigator-finder")
        {
            Content = new StringContent(
                """{"treePrefixes": ["C14.907"]}""",
                Encoding.UTF8,
                "application/json")
        };

        using HttpResponseMessage response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.IsTrue(doc.RootElement.TryGetProperty("totalCandidates", out JsonElement totalCandidates));
        Assert.IsTrue(totalCandidates.GetInt32() > 0, "Hypertension prefix should match seeded studies");

        JsonElement investigators = doc.RootElement.GetProperty("investigators");
        Assert.IsTrue(investigators.GetArrayLength() > 0, "Should return ranked investigators");

        var found = investigators.EnumerateArray().Any(i =>
            i.GetProperty("name").GetString() == "Dr. David Williams, MD" &&
            i.GetProperty("uuid").GetString() == SeedData.Person9.Id.ToString());
        Assert.IsTrue(found, "PI of the seeded hypertension study should be ranked");
    }
}

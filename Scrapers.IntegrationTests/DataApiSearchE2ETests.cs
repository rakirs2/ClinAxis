using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class DataApiSearchE2ETests
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
    public async Task SearchByTitle_WithSeededData_ReturnsMatchingResult()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/studies?search=Pregabalin&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");
        Assert.IsTrue(data.GetArrayLength() > 0, "Search should return at least one result");

        var found = data.EnumerateArray().Any(s => s.GetProperty("nctId").GetString() == "NCT00000002");
        Assert.IsTrue(found, "Inserted study should appear in search results");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task EmptySearch_ReturnsAllSeededStudies()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/studies?page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.IsTrue(doc.RootElement.TryGetProperty("data", out JsonElement data));
        Assert.IsTrue(data.GetArrayLength() > 0, "Empty search should return studies");
        Assert.IsTrue(doc.RootElement.TryGetProperty("total", out JsonElement total));
        Assert.IsTrue(total.GetInt32() > 0, "Total should be > 0");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ResponseShape_MatchesSchema()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/studies?search=Pregabalin&page=1&pageSize=1");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        JsonElement root = doc.RootElement;
        Assert.IsTrue(root.TryGetProperty("data", out JsonElement data));
        Assert.IsTrue(root.TryGetProperty("total", out _));
        Assert.IsTrue(root.TryGetProperty("page", out _));
        Assert.IsTrue(root.TryGetProperty("pageSize", out _));
        Assert.IsTrue(root.TryGetProperty("totalPages", out _));

        if (data.GetArrayLength() > 0)
        {
            JsonElement study = data[0];
            Assert.IsTrue(study.TryGetProperty("nctId", out _));
            Assert.IsTrue(study.TryGetProperty("briefTitle", out _));
            Assert.IsTrue(study.TryGetProperty("overallStatus", out _));
            Assert.IsTrue(study.TryGetProperty("investigatorCount", out _));
            Assert.IsTrue(study.TryGetProperty("pubmedPaperCount", out _));
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchByStatus_ReturnsFilteredResults()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/studies?status=RECRUITING&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");
        Assert.IsTrue(data.GetArrayLength() > 0, "Should find RECRUITING studies");
        var allRecruiting = data.EnumerateArray().All(s => s.GetProperty("overallStatus").GetString() == "RECRUITING");
        Assert.IsTrue(allRecruiting, "All returned studies should have RECRUITING status");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Pagination_ReturnsCorrectCount()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/studies?page=1&pageSize=2");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");
        Assert.AreEqual(2, data.GetArrayLength(), "Should return exactly 2 studies on page 1");
    }
}

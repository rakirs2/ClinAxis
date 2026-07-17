using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Testing;
using Scrapers.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

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
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
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
    public async Task RejectedEntitiesEndpoint_Returns200WithPagination()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/rejected-entities?type=keyword&page=1&pageSize=10");
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.IsTrue(doc.RootElement.TryGetProperty("data", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("total", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("page", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("pageSize", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("totalPages", out _));
        Assert.AreEqual(1, doc.RootElement.GetProperty("page").GetInt32());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task EnrichmentBreakdownEndpoint_Returns200WithCounts()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/enrichment/breakdown");
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var dictionary = doc.RootElement;
        Assert.IsTrue(dictionary.ValueKind == System.Text.Json.JsonValueKind.Object);
    }
}

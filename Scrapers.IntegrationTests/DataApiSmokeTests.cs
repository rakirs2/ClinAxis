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
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("DataApi", doc.RootElement.GetProperty("application").GetString());
        Assert.IsTrue(doc.RootElement.TryGetProperty("version", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("informationalVersion", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("framework", out _));
    }
}

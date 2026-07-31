using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class InvestigatorFinderModelScoreTests
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
    public async Task FinderEndpoint_ReturnsModelScoreInRange_WhenModelPresent()
    {
        var request = new { treePrefixes = new[] { "C10.500" }, topN = 10 };
        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/investigator-finder", request);

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.IsTrue(doc.RootElement.TryGetProperty("investigators", out var investigators));
        Assert.IsTrue(investigators.GetArrayLength() > 0, "expected at least one investigator for C10.500");

        foreach (var inv in investigators.EnumerateArray())
        {
            Assert.IsTrue(inv.TryGetProperty("score", out _), "missing rule-based score");
            Assert.IsTrue(inv.TryGetProperty("modelScore", out var modelScore),
                "missing modelScore — pi-model artifacts not found by DataApi");
            Assert.AreEqual(JsonValueKind.Number, modelScore.ValueKind);
            var value = modelScore.GetDouble();
            Assert.IsTrue(value >= 0.0 && value <= 1.0, $"modelScore {value} out of [0,1]");
        }
    }
}

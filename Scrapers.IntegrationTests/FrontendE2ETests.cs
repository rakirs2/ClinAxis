using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class FrontendE2ETests
{
    private WebApplicationFactory<Frontend.Program> _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Init()
    {
        _factory = new WebApplicationFactory<Frontend.Program>();
        _client = _factory.CreateClient();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Homepage_RendersSearchForm()
    {
        using HttpResponseMessage response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.IsTrue(html.Contains("Study Search", StringComparison.Ordinal), "Page should contain title");
        Assert.IsTrue(html.Contains("class=\"form-control\"", StringComparison.Ordinal), "Page should contain search input");
        Assert.IsTrue(html.Contains("blazor.web.js", StringComparison.Ordinal), "Page should reference Blazor script");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task BlazorScript_ServesSuccessfully()
    {
        using HttpResponseMessage response = await _client.GetAsync("/_framework/blazor.web.js");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Length > 1000, "blazor.web.js should be a substantial JS file");
        Assert.IsTrue(content.Contains("Blazor", StringComparison.OrdinalIgnoreCase), "blazor.web.js should contain Blazor runtime code");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task BlazorNegotiate_ReturnsAvailableTransports()
    {
        using HttpResponseMessage response = await _client.PostAsync("/_blazor/negotiate", null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.IsTrue(doc.RootElement.TryGetProperty("connectionId", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("availableTransports", out JsonElement transports));
        Assert.IsTrue(transports.GetArrayLength() > 0, "Should have at least one transport");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Homepage_IncludesNavLinks()
    {
        using HttpResponseMessage response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.IsTrue(html.Contains("/pipeline-runs", StringComparison.Ordinal), "Should link to pipeline history");
        Assert.IsTrue(html.Contains("/status", StringComparison.Ordinal), "Should link to status page");
    }
}

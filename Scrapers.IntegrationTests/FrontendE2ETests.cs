using System.Net;
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
    public async Task HealthEndpoint_Returns200WithStatusAndVersion()
    {
        using HttpResponseMessage response = await _client.GetAsync("/health");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("Frontend", doc.RootElement.GetProperty("application").GetString());
        Assert.IsTrue(doc.RootElement.TryGetProperty("version", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("informationalVersion", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("framework", out _));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Homepage_RendersSearchFormAndNavLinks()
    {
        using HttpResponseMessage response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.IsTrue(html.Contains("Study Search", StringComparison.Ordinal), "Page should contain title");
        Assert.IsTrue(html.Contains("class=\"form-control\"", StringComparison.Ordinal), "Page should contain search input");
        Assert.IsTrue(html.Contains("blazor.web.js", StringComparison.Ordinal), "Page should reference Blazor script");
        Assert.IsTrue(html.Contains("/status", StringComparison.Ordinal), "Should link to status page");
    }
}

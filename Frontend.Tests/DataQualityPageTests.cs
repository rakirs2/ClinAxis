using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class DataQualityPageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [TestMethod]
    public void DataQualityPageRendersTitle()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockTab0(mockHttp);
        MockTab1(mockHttp);
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.DataQuality> cut = ctx.Render<Frontend.Pages.DataQuality>(
            parameters => parameters.Add(p => p.Tab, 0));
        Assert.IsNotNull(cut.Find("h1"));
        Assert.AreEqual("Data Quality", cut.Find("h1").TextContent);
    }

    [TestMethod]
    public void Tab0IsActiveByDefault()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockTab0(mockHttp);
        MockTab1(mockHttp);
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.DataQuality> cut = ctx.Render<Frontend.Pages.DataQuality>(
            parameters => parameters.Add(p => p.Tab, 0));

        var links = cut.FindAll(".nav-tabs .nav-link");
        Assert.AreEqual(5, links.Count);
        Assert.IsTrue(links[0].ClassList.Contains("active"));
        Assert.IsFalse(links[1].ClassList.Contains("active"));
        Assert.IsFalse(links[2].ClassList.Contains("active"));
        Assert.IsFalse(links[3].ClassList.Contains("active"));
    }

    [TestMethod]
    public void Tab0ShowsDeadLetterContent()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockTab0(mockHttp);
        MockTab1(mockHttp);
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.DataQuality> cut = ctx.Render<Frontend.Pages.DataQuality>(
            parameters => parameters.Add(p => p.Tab, 0));

        cut.WaitForState(() => cut.Markup.Contains("dead-letter", StringComparison.OrdinalIgnoreCase), timeout: TimeSpan.FromSeconds(5));
        StringAssert.Contains(cut.Markup, "Failed Events", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Tab1ShowsRejectedKeywords()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockTab0(mockHttp);
        MockTab1(mockHttp);
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.DataQuality> cut = ctx.Render<Frontend.Pages.DataQuality>(
            parameters => parameters.Add(p => p.Tab, 1));

        cut.WaitForState(() => cut.Markup.Contains("BREAST CANCER", StringComparison.OrdinalIgnoreCase), timeout: TimeSpan.FromSeconds(5));
        StringAssert.Contains(cut.Markup, "Rejected Keywords", StringComparison.Ordinal);
        var links = cut.FindAll(".nav-tabs .nav-link");
        Assert.IsFalse(links[0].ClassList.Contains("active"));
        Assert.IsTrue(links[1].ClassList.Contains("active"));
    }

    [TestMethod]
    public void HrefsPointToCorrectRoutes()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockTab0(mockHttp);
        MockTab1(mockHttp);
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.DataQuality> cut = ctx.Render<Frontend.Pages.DataQuality>(
            parameters => parameters.Add(p => p.Tab, 0));

        var links = cut.FindAll(".nav-tabs a");
        Assert.AreEqual("/data-quality/0", links[0].GetAttribute("href"));
        Assert.AreEqual("/data-quality/1", links[1].GetAttribute("href"));
        Assert.AreEqual("/data-quality/2", links[2].GetAttribute("href"));
        Assert.AreEqual("/data-quality/3", links[3].GetAttribute("href"));
        Assert.AreEqual("/data-quality/4", links[4].GetAttribute("href"));
    }

    [TestMethod]
    public void Tab4ShowsKeywordGateSummaryAndSamples()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockTab0(mockHttp);
        mockHttp.When("http://localhost:5003/api/rejected-terms/summary")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                total = 1000,
                accepted = 300,
                rejected = 700,
                similarityBands = new Dictionary<string, int>
                {
                    ["0.00-0.50"] = 10,
                    ["0.50-0.65"] = 20,
                    ["0.65-0.80"] = 25,
                    ["0.80-1.00"] = 25
                }
            }, JsonOptions));
        mockHttp.When("http://localhost:5003/api/rejected-terms*")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                data = new[]
                {
                    new
                    {
                        id = 1,
                        studyNctId = "NCT001",
                        value = "Zebrafish",
                        source = "condition",
                        sideBMatched = false,
                        sideBMeshTerm = "unmapped",
                        sideBSimilarity = 0.6,
                        accepted = false
                    }
                },
                total = 80,
                page = 1,
                pageSize = 50,
                totalPages = 2
            }, JsonOptions));
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.DataQuality> cut = ctx.Render<Frontend.Pages.DataQuality>(
            parameters => parameters.Add(p => p.Tab, 4));

        cut.WaitForState(() => cut.Markup.Contains("Zebrafish", StringComparison.Ordinal), timeout: TimeSpan.FromSeconds(5));
        Assert.IsTrue(cut.Markup.Contains("Keyword acceptance gate (MiniLM)", StringComparison.Ordinal), "Tab heading should render");
        Assert.IsTrue(cut.Markup.Contains("700", StringComparison.Ordinal), "Rejected count should render");
        Assert.IsTrue(cut.Markup.Contains("Zebrafish", StringComparison.Ordinal), "Sample should render");
        Assert.IsTrue(cut.Markup.Contains("0.600", StringComparison.Ordinal), "Similarity should render with three decimals");
    }

    private static HttpClient BuildClient(MockHttpMessageHandler mockHttp)
    {
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        return client;
    }

    private static void MockTab0(MockHttpMessageHandler mockHttp)
    {
        mockHttp.When("http://localhost:5003/api/event-queue/dead-letter*")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                data = new[]
                {
                    new
                    {
                        id = 1,
                        eventType = "medicare.utilization",
                        data = "test",
                        status = "dead-letter",
                        errorMessage = "Test error",
                        retryCount = 3,
                        createdAt = DateTime.UtcNow
                    }
                },
                total = 1,
                page = 1,
                pageSize = 20,
                totalPages = 1
            }, JsonOptions));
    }

    private static void MockTab1(MockHttpMessageHandler mockHttp)
    {
        mockHttp.When("http://localhost:5003/api/rejected-entities")
            .WithQueryString("type=keyword")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                data = new[]
                {
                    new { id = 1, entityType = "keyword", value = "BREAST CANCER", studyNctId = "NCT00000001", rejectedAt = DateTime.UtcNow },
                    new { id = 2, entityType = "keyword", value = "HIV/AIDS", studyNctId = "NCT00000002", rejectedAt = DateTime.UtcNow }
                },
                total = 2,
                page = 1,
                pageSize = 50,
                totalPages = 1
            }, JsonOptions));
    }
}

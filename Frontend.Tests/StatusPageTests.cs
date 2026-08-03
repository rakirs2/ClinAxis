using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class StatusPageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [TestMethod]
    public void StatusPageRendersTitle()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockDefaults(mockHttp);
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.Status> cut = ctx.Render<Frontend.Pages.Status>();
        Assert.IsNotNull(cut.Find("h1"));
        Assert.AreEqual("System Status", cut.Find("h1").TextContent);
    }

    [TestMethod]
    public void StatusPageRendersEventQueueHealthSection()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockDefaults(mockHttp);
        mockHttp.When("http://localhost:5003/api/event-queue/stats")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                pendingCount = 5,
                processingCount = 1,
                completedCount = 100,
                deadLetterCount = 0,
                failedCount = 0,
                averageProcessingTimeMs = 2000.0,
                failureRate = 0.0,
                estimatedTimeRemainingMs = 10000.0
            }, JsonOptions));
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.Status> cut = ctx.Render<Frontend.Pages.Status>();

        cut.WaitForState(() => cut.Markup.Contains("Pending Events", StringComparison.Ordinal), timeout: TimeSpan.FromSeconds(5));
        var markup = cut.Markup;
        Assert.IsTrue(markup.Contains("Estimated Time Remaining", StringComparison.Ordinal));
        Assert.IsTrue(markup.Contains("Processing", StringComparison.Ordinal));
        Assert.IsTrue(markup.Contains("Completed", StringComparison.Ordinal));
        Assert.IsTrue(markup.Contains("Dead Letter", StringComparison.Ordinal));
        Assert.IsTrue(markup.Contains("Avg Processing Time", StringComparison.Ordinal));
        Assert.IsTrue(markup.Contains("Failure Rate", StringComparison.Ordinal));
    }

    [TestMethod]
    public void StatusPageRendersVisitorCounts()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockDefaultsWithStats(mockHttp);
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.Status> cut = ctx.Render<Frontend.Pages.Status>();

        cut.WaitForState(() => cut.Markup.Contains("Visitors", StringComparison.Ordinal), timeout: TimeSpan.FromSeconds(5));
        var markup = cut.Markup;
        Assert.IsTrue(markup.Contains("10 views", StringComparison.Ordinal), "Should show total views");
        Assert.IsTrue(markup.Contains("4 unique", StringComparison.Ordinal), "Should show unique visitors");
        Assert.IsTrue(markup.Contains("This week", StringComparison.Ordinal));
        Assert.IsTrue(markup.Contains("This month", StringComparison.Ordinal));
    }

    [TestMethod]
    public void StatusPageMarksSelectedNameAsHuman()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockDefaultsWithStats(mockHttp, includeRejectedNames: false);
        var id = Guid.NewGuid();
        var overridden = false;
        mockHttp.When("http://localhost:5003/api/rejected-names")
            .Respond("application/json", JsonSerializer.Serialize(new[]
            {
                new { id, name = "Pfizer", occurrenceCount = 3, studyCount = 2, rejectionReason = "PharmaBlocklist:PFIZER",
                      isHumanOverride = (bool?)(overridden ? true : null), note = (string?)(overridden ? "verified via NPPES" : null) }
            }, JsonOptions));
        mockHttp.When(HttpMethod.Post, "http://localhost:5003/api/rejected-names/*/override")
            .Respond(async () =>
            {
                overridden = true;
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        id,
                        name = "Pfizer",
                        occurrenceCount = 3,
                        studyCount = 2,
                        rejectionReason = "PharmaBlocklist:PFIZER",
                        isHumanOverride = true,
                        note = "verified via NPPES"
                    }, JsonOptions), System.Text.Encoding.UTF8, "application/json")
                };
            });
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.Status> cut = ctx.Render<Frontend.Pages.Status>();

        cut.WaitForState(() => cut.Markup.Contains("Pfizer", StringComparison.Ordinal), timeout: TimeSpan.FromSeconds(5));

        var noteInput = cut.FindAll("input")[0];
        noteInput.Change("verified via NPPES");
        cut.FindAll("input[type=checkbox]").Single().Change(true);
        cut.Find("button.btn-success").Click();

        cut.WaitForState(() => cut.Markup.Contains("Human", StringComparison.Ordinal), timeout: TimeSpan.FromSeconds(5));
        Assert.IsTrue(cut.Markup.Contains("verified via NPPES", StringComparison.Ordinal));
    }

    private static HttpClient BuildClient(MockHttpMessageHandler mockHttp)
    {
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        return client;
    }

    private static void MockDefaults(MockHttpMessageHandler mockHttp)
    {
        mockHttp.When("http://localhost:5003/api/telemetry")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                db = new { totalStudies = 100, totalInvestigators = 50, totalPubmedPapers = 20, totalKeywords = 200, totalAuthors = 0 },
                recentEvents = Array.Empty<object>()
            }, JsonOptions));

        mockHttp.When("http://localhost:5003/api/data-source-state")
            .Respond("application/json", JsonSerializer.Serialize(Array.Empty<object>(), JsonOptions));

        mockHttp.When("http://localhost:5003/api/event-queue/dead-letter")
            .Respond("application/json", JsonSerializer.Serialize(Array.Empty<object>(), JsonOptions));
    }

    private static void MockDefaultsWithStats(MockHttpMessageHandler mockHttp, bool includeRejectedNames = true)
    {
        MockDefaults(mockHttp);

        mockHttp.When("http://localhost:5003/api/event-queue/stats")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                pendingCount = 0,
                processingCount = 0,
                completedCount = 50,
                deadLetterCount = 0,
                failedCount = 0,
                averageProcessingTimeMs = 0.0,
                failureRate = 0.0,
                estimatedTimeRemainingMs = (double?)null
            }, JsonOptions));

        if (includeRejectedNames)
        {
            mockHttp.When("http://localhost:5003/api/rejected-names")
                .Respond("application/json", JsonSerializer.Serialize(Array.Empty<object>(), JsonOptions));
        }

        mockHttp.When("http://localhost:5003/api/scraper-progress")
            .Respond("application/json", JsonSerializer.Serialize(new { }, JsonOptions));

        mockHttp.When("http://localhost:5003/api/page-views/stats?period=day")
            .Respond("application/json", JsonSerializer.Serialize(new { totalViews = 10, uniqueVisitors = 4 }, JsonOptions));

        mockHttp.When("http://localhost:5003/api/page-views/stats?period=week")
            .Respond("application/json", JsonSerializer.Serialize(new { totalViews = 25, uniqueVisitors = 9 }, JsonOptions));

        mockHttp.When("http://localhost:5003/api/page-views/stats?period=month")
            .Respond("application/json", JsonSerializer.Serialize(new { totalViews = 100, uniqueVisitors = 20 }, JsonOptions));
    }
}

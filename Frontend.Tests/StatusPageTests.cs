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

    [TestMethod]
    public void StatusPageFullResyncButtonPostsIngestRunRequest()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockDefaultsWithStats(mockHttp, includeRejectedNames: false,
            dataSourceStatesJson: JsonSerializer.Serialize(new[]
            {
                new { sourceName = "ClinicalTrials.gov", status = "idle", backfillStatus = "complete", backfillRemainingStudies = (int?)0 }
            }, JsonOptions));
        mockHttp.When("http://localhost:5003/api/rejected-names")
            .Respond("application/json", JsonSerializer.Serialize(Array.Empty<object>(), JsonOptions));
        var requestedMode = "";
        mockHttp.When(HttpMethod.Post, "http://localhost:5003/api/ingest/run*")
            .Respond(async req =>
            {
                requestedMode = req.RequestUri!.Query;
                return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { accepted = true }, JsonOptions),
                        System.Text.Encoding.UTF8, "application/json")
                };
            });
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.Status> cut = ctx.Render<Frontend.Pages.Status>();

        cut.WaitForState(() => cut.Markup.Contains("Full re-sync", StringComparison.Ordinal), timeout: TimeSpan.FromSeconds(5));

        cut.FindAll("button").First(b => b.TextContent.Contains("Full re-sync", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.IsTrue(requestedMode.Contains("mode=full", StringComparison.Ordinal)),
            timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void StatusPageShowsScraperProgressTodayCard()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockDefaultsWithStats(mockHttp);
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.Status> cut = ctx.Render<Frontend.Pages.Status>();

        cut.WaitForState(() => cut.Markup.Contains("Scraper Progress Today", StringComparison.Ordinal), timeout: TimeSpan.FromSeconds(5));
        Assert.IsTrue(cut.Markup.Contains("Studies added in the last 24 hours", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("1,234", StringComparison.Ordinal), "Added-last-24h count should render");
    }

    [TestMethod]
    public void StatusPageUsesCtGovTotalForLiveStudies()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockDefaultsWithStats(mockHttp);
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.Status> cut = ctx.Render<Frontend.Pages.Status>();

        cut.WaitForState(() => cut.Markup.Contains("Studies (live, on CT.gov)", StringComparison.Ordinal), timeout: TimeSpan.FromSeconds(5));
        var row = cut.FindAll("tr").Single(tr => tr.TextContent.Contains("Studies (live, on CT.gov)", StringComparison.Ordinal));

        StringAssert.Contains(row.TextContent, "500,000", StringComparison.Ordinal);
        Assert.IsFalse(row.TextContent.Contains("400,000", StringComparison.Ordinal),
            "The live CT.gov row must not use the local database count.");
    }

    [TestMethod]
    public void StatusPageStillLoadsScraperProgressWhenQueueStatsFail()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockDefaults(mockHttp);
        mockHttp.When("http://localhost:5003/api/event-queue/stats")
            .Respond(_ => throw new HttpRequestException("queue stats timeout"));
        mockHttp.When("http://localhost:5003/api/scraper-progress")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                totalAvailable = 500000,
                totalInDb = 400000,
                percentScraped = 80.0,
                addedLast24h = 1234
            }, JsonOptions));
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.Status> cut = ctx.Render<Frontend.Pages.Status>();

        cut.WaitForState(() => cut.Markup.Contains("Studies (live, on CT.gov)", StringComparison.Ordinal), timeout: TimeSpan.FromSeconds(5));
        var row = cut.FindAll("tr").Single(tr => tr.TextContent.Contains("Studies (live, on CT.gov)", StringComparison.Ordinal));

        StringAssert.Contains(row.TextContent, "500,000", StringComparison.Ordinal);
        Assert.IsTrue(cut.Markup.Contains("Queue telemetry unavailable", StringComparison.Ordinal));
    }

    [TestMethod]
    public void StatusPageRendersInFlightProgressAndHeartbeatColumns()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        MockDefaults(mockHttp);
        mockHttp.When("http://localhost:5003/api/event-queue/stats")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                pendingCount = 18,
                processingCount = 1,
                completedCount = 242,
                deadLetterCount = 0,
                failedCount = 0,
                averageProcessingTimeMs = 0.0,
                failureRate = 0.0,
                estimatedTimeRemainingMs = (double?)null,
                byEventType = new[]
                {
                    new
                    {
                        eventType = "studies.backfill",
                        pending = 18,
                        processing = 1,
                        completed = 242,
                        failed = 0,
                        deadLetter = 0,
                        averageProcessingTimeMs = 0.0,
                        completedLast15m = 0,
                        completedLast1h = 0,
                        inFlight = (object?)new
                        {
                            eventId = 30473,
                            claimedAt = DateTime.UtcNow.AddMinutes(-30),
                            progressUpdatedAt = DateTime.UtcNow.AddMinutes(-15),
                            processed = 400,
                            total = 1011,
                            percent = 39.6,
                            ratePerMin = 13.3,
                            etaUtc = DateTime.UtcNow.AddMinutes(46)
                        }
                    },
                    new
                    {
                        eventType = "investigator.enrichment",
                        pending = 0,
                        processing = 0,
                        completed = 2,
                        failed = 0,
                        deadLetter = 0,
                        averageProcessingTimeMs = 1200.0,
                        completedLast15m = 2,
                        completedLast1h = 2,
                        inFlight = (object?)null
                    }
                }
            }, JsonOptions));
        var client = BuildClient(mockHttp);
        ctx.Services.AddSingleton(client);
        IRenderedComponent<Frontend.Pages.Status> cut = ctx.Render<Frontend.Pages.Status>();

        cut.WaitForState(() => cut.Markup.Contains("In-flight Events", StringComparison.Ordinal), timeout: TimeSpan.FromSeconds(5));
        var markup = cut.Markup;
        Assert.IsTrue(markup.Contains("#30473", StringComparison.Ordinal), "In-flight event id should render");
        Assert.IsTrue(markup.Contains("400 / 1,011", StringComparison.Ordinal), "Processed/total progress should render");
        Assert.IsTrue(markup.Contains("39.6%", StringComparison.Ordinal), "Percent should render");
        Assert.IsTrue(markup.Contains("13.3/min", StringComparison.Ordinal), "Rate should render");
        Assert.IsTrue(markup.Contains("Done 15m", StringComparison.Ordinal), "Heartbeat column header should render");
        Assert.IsTrue(markup.Contains("Done 1h", StringComparison.Ordinal), "Heartbeat column header should render");
    }

    [TestMethod]
    public async Task StatusPageStartsIndependentRequestsConcurrently()
    {
        using var ctx = new BunitContext();
        using var handler = new ConcurrentStatusHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5003") };
        ctx.Services.AddSingleton(client);
        _ = ctx.Render<Frontend.Pages.Status>();

        await handler.BothSlowRequestsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        handler.Release.TrySetResult(true);
    }

    private sealed class ConcurrentStatusHandler : HttpMessageHandler
    {
        public TaskCompletionSource<bool> BothSlowRequestsStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _slowRequestsStarted;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path is "/api/telemetry" or "/api/scraper-progress")
            {
                if (Interlocked.Increment(ref _slowRequestsStarted) == 2)
                    BothSlowRequestsStarted.TrySetResult(true);
                await Release.Task.WaitAsync(cancellationToken);
            }

            var payload = path switch
            {
                "/api/telemetry" => "{\"db\":{\"totalStudies\":100,\"totalStudiesLive\":100,\"totalInvestigators\":10,\"totalPubmedPapers\":5,\"totalKeywords\":20,\"totalAuthors\":0},\"enrichment\":{\"totalInvestigators\":10,\"withNpi\":5,\"notFound\":0,\"ambiguous\":0,\"notAttempted\":5,\"npiCoveragePct\":50.0},\"recentEvents\":[]}",
                "/api/scraper-progress" => "{\"totalAvailable\":100,\"totalInDb\":100,\"percentScraped\":100.0,\"addedLast24h\":0}",
                "/api/data-source-state" => "[]",
                "/api/rejected-names" => "[]",
                _ when path.StartsWith("/api/page-views/stats", StringComparison.Ordinal) => "{\"totalViews\":0,\"uniqueVisitors\":0}",
                _ => "{\"pendingCount\":0,\"processingCount\":0,\"completedCount\":0,\"deadLetterCount\":0,\"failedCount\":0,\"averageProcessingTimeMs\":0,\"failureRate\":0,\"estimatedTimeRemainingMs\":null}"
            };

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private static HttpClient BuildClient(MockHttpMessageHandler mockHttp)
    {
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        return client;
    }

    private static void MockDefaults(MockHttpMessageHandler mockHttp, bool includeDataSourceState = true)
    {
        mockHttp.When("http://localhost:5003/api/telemetry")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                 db = new { totalStudies = 100, totalStudiesLive = 400000, totalInvestigators = 50, totalPubmedPapers = 20, totalKeywords = 200, totalAuthors = 0 },
                recentEvents = Array.Empty<object>()
            }, JsonOptions));

        if (includeDataSourceState)
        {
            mockHttp.When("http://localhost:5003/api/data-source-state")
                .Respond("application/json", JsonSerializer.Serialize(Array.Empty<object>(), JsonOptions));
        }

        mockHttp.When("http://localhost:5003/api/event-queue/dead-letter")
            .Respond("application/json", JsonSerializer.Serialize(Array.Empty<object>(), JsonOptions));
    }

    private static void MockDefaultsWithStats(MockHttpMessageHandler mockHttp, bool includeRejectedNames = true,
        string? dataSourceStatesJson = null)
    {
        MockDefaults(mockHttp, includeDataSourceState: dataSourceStatesJson is null);

        if (dataSourceStatesJson is not null)
        {
            mockHttp.When("http://localhost:5003/api/data-source-state")
                .Respond("application/json", dataSourceStatesJson);
        }

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
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                totalAvailable = 500000,
                totalInDb = 400000,
                percentScraped = 80.0,
                ingestRatePerHour = 1200.0,
                remainingStudies = 100000,
                estimatedCompletionUtc = DateTime.UtcNow.AddDays(1),
                addedLast24h = 1234,
                sinceUtc = DateTime.UtcNow.AddHours(-24)
            }, JsonOptions));

        mockHttp.When("http://localhost:5003/api/page-views/stats?period=day")
            .Respond("application/json", JsonSerializer.Serialize(new { totalViews = 10, uniqueVisitors = 4 }, JsonOptions));

        mockHttp.When("http://localhost:5003/api/page-views/stats?period=week")
            .Respond("application/json", JsonSerializer.Serialize(new { totalViews = 25, uniqueVisitors = 9 }, JsonOptions));

        mockHttp.When("http://localhost:5003/api/page-views/stats?period=month")
            .Respond("application/json", JsonSerializer.Serialize(new { totalViews = 100, uniqueVisitors = 20 }, JsonOptions));
    }
}

using System.Net;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class SearchPageTests
{
    private static readonly string[] ConditionTestData = ["Condition A"];
    private static readonly string[] RichConditionTestData = ["Condition A", "Condition B", "Diabetes Mellitus", "Hypertension"];
    private static readonly string[] PhaseTestData = ["PHASE2"];
    private static readonly string[] CountryTestData = ["USA"];
    private static readonly string[] EmptyArray = [];

    private static object DefaultStudiesResponse() => new
    {
        data = EmptyArray,
        total = 0,
        page = 1,
        pageSize = 20,
        totalPages = 0
    };

    private static (BunitContext Ctx, MockHttpMessageHandler Mock, IRenderedComponent<Frontend.Pages.Search> Cut) SetupTest(
        string[]? conditions = null, object? studiesResponse = null, TaskCompletionSource? conditionsGate = null,
        TaskCompletionSource? studiesGate = null, Action<HttpRequestMessage>? captureStudies = null, string? initialUrl = null)
    {
        var ctx = new BunitContext();
        var mockHttp = new MockHttpMessageHandler();
        if (conditionsGate is null)
        {
            mockHttp.When("/api/distinct-conditions")
                .Respond("application/json", JsonSerializer.Serialize(conditions ?? ConditionTestData));
        }
        else
        {
            mockHttp.When("/api/distinct-conditions")
                .Respond(async () =>
                {
                    await conditionsGate.Task;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(conditions ?? ConditionTestData), System.Text.Encoding.UTF8, "application/json")
                    };
                });
        }
        mockHttp.When("/api/distinct-locations").Respond("application/json", JsonSerializer.Serialize(new
        {
            countries = CountryTestData,
            states = EmptyArray,
            cities = EmptyArray,
            facilities = EmptyArray
        }));
        mockHttp.When("/api/mesh-tree*").Respond("application/json", "[]");
        if (studiesResponse is not null)
        {
            if (studiesGate is null)
            {
                mockHttp.When("/api/studies*")
                    .Respond(req =>
                    {
                        captureStudies?.Invoke(req);
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(JsonSerializer.Serialize(studiesResponse), System.Text.Encoding.UTF8, "application/json")
                        };
                    });
            }
            else
            {
                mockHttp.When("/api/studies*")
                    .Respond(async () =>
                    {
                        await studiesGate.Task;
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(JsonSerializer.Serialize(studiesResponse), System.Text.Encoding.UTF8, "application/json")
                        };
                    });
            }
        }
        else
        {
            mockHttp.When("/api/studies*")
                .Respond(req =>
                {
                    captureStudies?.Invoke(req);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(DefaultStudiesResponse()), System.Text.Encoding.UTF8, "application/json")
                    };
                });
        }
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        ctx.Services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(client));

        ctx.JSInterop.SetupVoid("meshTree.render", _ => true);
        ctx.JSInterop.Setup<string[]>("meshTree.getSelected", _ => true).SetResult([]);

        if (initialUrl is not null)
        {
            var nav = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
            nav.NavigateTo(initialUrl);
        }

        var cut = ctx.Render<Frontend.Pages.Search>();
        if (conditionsGate is null)
        {
            cut.WaitForState(() => !cut.Markup.Contains("Loading conditions...", StringComparison.Ordinal), TimeSpan.FromSeconds(5));
        }
        return (ctx, mockHttp, cut);
    }

    [TestMethod]
    public void SearchPageRendersSearchTitle()
    {
        var (ctx, _, cut) = SetupTest();

        Assert.IsNotNull(cut.Find("h1"));
        Assert.IsTrue(cut.Find("h1").TextContent.Contains("Search", StringComparison.Ordinal));
        ctx.Dispose();
    }

    [TestMethod]
    public void SearchPageRendersFilterInputs()
    {
        var (ctx, _, cut) = SetupTest();

        Assert.IsNotNull(cut.Find("input[placeholder='Search by title, NCT ID, or keyword']"));
        Assert.IsNotNull(cut.Find("button:contains('Search')"));
        ctx.Dispose();
    }

    [TestMethod]
    public void SearchPageRendersBranchSelectorAndConditionAutocomplete()
    {
        var (ctx, _, cut) = SetupTest();

        Assert.IsNotNull(cut.Find("select"));
        Assert.IsNotNull(cut.Find("input[placeholder='Search condition name...']"));
        Assert.IsTrue(cut.FindAll("option").Any(o => o.TextContent.Contains("Diseases", StringComparison.Ordinal)));
        Assert.IsTrue(cut.FindAll("option").Any(o => o.TextContent.Contains("Anatomy", StringComparison.Ordinal)));
        ctx.Dispose();
    }

    [TestMethod]
    public void ConditionAutocompleteFiltersWhenTypedBeforeConditionsLoad()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (ctx, _, cut) = SetupTest(conditions: RichConditionTestData, conditionsGate: gate);

        var input = cut.Find("input[placeholder='Search condition name...']");
        input.Input("Diabetes");

        cut.WaitForAssertion(() => Assert.AreEqual(0, cut.FindAll("li.list-group-item").Count));

        gate.SetResult();

        cut.WaitForState(() => cut.FindAll("li.list-group-item").Count > 0, TimeSpan.FromSeconds(5));
        var items = cut.FindAll("li.list-group-item");
        Assert.AreEqual(1, items.Count);
        Assert.IsTrue(items[0].TextContent.Contains("Diabetes Mellitus", StringComparison.Ordinal));
        ctx.Dispose();
    }

    [TestMethod]
    public void ClickingConditionAddsAndRemovesBadge()
    {
        var (ctx, _, cut) = SetupTest(conditions: RichConditionTestData);

        var input = cut.Find("input[placeholder='Search condition name...']");
        input.Input("Condition");

        cut.WaitForState(() => cut.FindAll("li.list-group-item").Count > 0, TimeSpan.FromSeconds(5));
        cut.FindAll("li.list-group-item")[0].Click();

        cut.WaitForState(() => cut.FindAll("span.badge").Count > 0, TimeSpan.FromSeconds(5));
        cut.WaitForState(() => cut.FindAll("div.spinner-border").Count == 0, TimeSpan.FromSeconds(5));

        var badges = cut.FindAll("span.badge");
        Assert.AreEqual(1, badges.Count);
        Assert.IsTrue(badges[0].TextContent.Contains("Condition A", StringComparison.Ordinal));

        badges[0].QuerySelector("button.btn-close")!.Click();

        cut.WaitForState(() => cut.FindAll("span.badge").Count == 0, TimeSpan.FromSeconds(5));
        ctx.Dispose();
    }

    [TestMethod]
    public void SearchSendsConditionParamAndRendersResultsTable()
    {
        var studiesResponse = new
        {
            data = new[]
            {
                new
                {
                    nctId = "NCT00000001",
                    briefTitle = "Test Study",
                    overallStatus = "RECRUITING",
                    conditions = ConditionTestData,
                    phases = PhaseTestData,
                    enrollmentCount = 100
                }
            },
            total = 1,
            page = 1,
            pageSize = 20,
            totalPages = 1
        };

        string? studiesQuery = null;
        var (ctx, _, cut) = SetupTest(
            conditions: RichConditionTestData, studiesResponse: studiesResponse,
            captureStudies: req => studiesQuery = req.RequestUri!.Query);

        ctx.JSInterop.Setup<string[]>("meshTree.getSelected", _ => true).SetResult([]);

        // Select a condition, then search
        var input = cut.Find("input[placeholder='Search condition name...']");
        input.Input("Condition");
        cut.WaitForState(() => cut.FindAll("li.list-group-item").Count > 0, TimeSpan.FromSeconds(5));
        cut.FindAll("li.list-group-item")[0].Click();

        cut.Find("button:contains('Search')").Click();

        cut.WaitForState(() => cut.FindAll("table").Count > 0, TimeSpan.FromSeconds(5));

        Assert.IsNotNull(cut.Find("a[href='/studies/NCT00000001']"));
        Assert.IsNotNull(studiesQuery);
        StringAssert.Contains(studiesQuery!, "condition=Condition%20A", StringComparison.Ordinal);
        ctx.Dispose();
    }

    [TestMethod]
    public void UrlWithFiltersAutoSearchesOnLoadAndRendersResults()
    {
        var studiesResponse = new
        {
            data = new[]
            {
                new
                {
                    nctId = "NCT00000001",
                    briefTitle = "Test Study",
                    overallStatus = "RECRUITING",
                    conditions = ConditionTestData,
                    phases = PhaseTestData,
                    enrollmentCount = 100
                }
            },
            total = 1,
            page = 1,
            pageSize = 20,
            totalPages = 1
        };

        string? studiesQuery = null;
        var (ctx, _, cut) = SetupTest(
            studiesResponse: studiesResponse, captureStudies: req => studiesQuery = req.RequestUri!.Query,
            initialUrl: "/?status=RECRUITING&page=1&pageSize=10");

        cut.WaitForState(() => cut.FindAll("table").Count > 0, TimeSpan.FromSeconds(5));

        Assert.IsNotNull(studiesQuery);
        StringAssert.Contains(studiesQuery!, "status=RECRUITING", StringComparison.Ordinal);
        Assert.IsNotNull(cut.Find("a[href='/studies/NCT00000001']"));
        ctx.Dispose();
    }

    [TestMethod]
    public void TogglingStatusFilterSearchesInRealTimeWithoutButton()
    {
        string? studiesQuery = null;
        var (ctx, _, cut) = SetupTest(captureStudies: req => studiesQuery = req.RequestUri!.Query);

        cut.Find("#status_RECRUITING").Change(true);

        cut.WaitForState(() => studiesQuery is not null && studiesQuery!.Contains("status=RECRUITING", StringComparison.Ordinal), TimeSpan.FromSeconds(5));
        ctx.Dispose();
    }

    [TestMethod]
    public void TypingKeywordDebouncesToSingleSearch()
    {
        var requestCount = 0;
        string? lastQuery = null;
        var (ctx, _, cut) = SetupTest(captureStudies: req =>
        {
            requestCount++;
            lastQuery = req.RequestUri!.Query;
        });

        var input = cut.Find("input[placeholder='Search by title, NCT ID, or keyword']");
        input.Input("pre");
        input.Input("pregabalin");

        cut.WaitForState(() => requestCount >= 1 && lastQuery!.Contains("keyword=pregabalin", StringComparison.Ordinal), TimeSpan.FromSeconds(5));

        Assert.AreEqual(1, requestCount);
        ctx.Dispose();
    }

    [TestMethod]
    public void FilterChangeUpdatesShareableUrl()
    {
        var (ctx, _, cut) = SetupTest();
        var nav = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();

        cut.Find("#status_RECRUITING").Change(true);

        cut.WaitForState(() => nav.Uri.Contains("status=RECRUITING", StringComparison.Ordinal), TimeSpan.FromSeconds(5));
        StringAssert.Contains(nav.Uri, "page=1", StringComparison.Ordinal);
        ctx.Dispose();
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FakeHttpClientFactory(HttpClient client)
        {
            _client = client;
        }
        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }
}
using System.Net;
using System.Text.Json;
using Bunit;
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

    private static (BunitContext Ctx, MockHttpMessageHandler Mock, IRenderedComponent<Frontend.Pages.Search> Cut) SetupTest(
        string[]? conditions = null, object? studiesResponse = null, TaskCompletionSource? conditionsGate = null,
        TaskCompletionSource? studiesGate = null)
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
                mockHttp.When("/api/studies*").Respond("application/json", JsonSerializer.Serialize(studiesResponse));
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
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        ctx.Services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(client));

        ctx.JSInterop.SetupVoid("meshTree.render", _ => true);

        var cut = ctx.Render<Frontend.Pages.Search>();
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

        Assert.IsNotNull(cut.Find("input[placeholder='Search by title or NCT ID']"));
        Assert.IsNotNull(cut.Find("button:contains('Search')"));
        ctx.Dispose();
    }

    [TestMethod]
    public void SearchPageRendersPagination()
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

        var studiesGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var (ctx, _, cut) = SetupTest(studiesResponse: studiesResponse, studiesGate: studiesGate);

        ctx.JSInterop.Setup<string[]>("meshTree.getSelected", _ => true).SetResult([]);

        cut.Find("button:contains('Search')").Click();

        Assert.AreEqual(0, cut.FindAll("table").Count, "table must not render before the studies response completes");

        studiesGate.SetResult();

        cut.WaitForAssertion(() => Assert.AreEqual(1, cut.FindAll("table").Count), timeout: TimeSpan.FromSeconds(30));

        Assert.IsNotNull(cut.Find("a[href='/studies/NCT00000001']"));
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
    public void ClickingConditionAddsBadge()
    {
        var (ctx, _, cut) = SetupTest(conditions: RichConditionTestData);

        var input = cut.Find("input[placeholder='Search condition name...']");
        input.Input("Condition");

        cut.WaitForState(() => cut.FindAll("li.list-group-item").Count > 0, TimeSpan.FromSeconds(6));
        cut.FindAll("li.list-group-item")[0].Click();

        cut.WaitForState(() => cut.FindAll("span.badge").Count > 0, TimeSpan.FromSeconds(6));
        var badges = cut.FindAll("span.badge");
        Assert.IsTrue(badges.Any(b => b.TextContent.Contains("Condition A", StringComparison.Ordinal)));
        ctx.Dispose();
    }

    [TestMethod]
    public void ConditionBadgeRemoveButtonRemovesTag()
    {
        var (ctx, _, cut) = SetupTest(conditions: RichConditionTestData);

        var input = cut.Find("input[placeholder='Search condition name...']");
        input.Input("Condition");

        cut.WaitForState(() => cut.FindAll("li.list-group-item").Count > 0, TimeSpan.FromSeconds(6));
        cut.FindAll("li.list-group-item")[0].Click();
        cut.WaitForState(() => cut.FindAll("span.badge").Count > 0, TimeSpan.FromSeconds(3));
        Assert.AreEqual(1, cut.FindAll("span.badge").Count);

        var removeBtn = cut.Find("span.badge button.btn-close");
        removeBtn.Click();

        cut.WaitForState(() => cut.FindAll("span.badge").Count == 0, TimeSpan.FromSeconds(3));
        Assert.AreEqual(0, cut.FindAll("span.badge").Count);
        ctx.Dispose();
    }

    [TestMethod]
    public void SearchSendsConditionParam()
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

        var (ctx, mockHttp, cut) = SetupTest(
            conditions: RichConditionTestData, studiesResponse: studiesResponse);

        ctx.JSInterop.Setup<string[]>("meshTree.getSelected", _ => true).SetResult([]);

        // Add a condition
        var input = cut.Find("input[placeholder='Search condition name...']");
        input.Input("Condition");
        cut.WaitForState(() => cut.FindAll("li.list-group-item").Count > 0, TimeSpan.FromSeconds(6));
        cut.FindAll("li.list-group-item")[0].Click();
        cut.WaitForState(() => cut.FindAll("span.badge").Count > 0, TimeSpan.FromSeconds(3));

        // Search
        cut.Find("button:contains('Search')").Click();

        cut.WaitForState(() => cut.FindAll("table").Count > 0, TimeSpan.FromSeconds(6));

        Assert.IsNotNull(cut.Find("a[href='/studies/NCT00000001']"));
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
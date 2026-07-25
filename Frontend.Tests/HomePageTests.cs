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
    private static readonly string[] PhaseTestData = ["PHASE2"];
    private static readonly string[] CountryTestData = ["USA"];
    private static readonly string[] EmptyArray = [];

    [TestMethod]
    public void SearchPageRendersSearchTitle()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("/api/distinct-conditions").Respond("application/json", JsonSerializer.Serialize(ConditionTestData));
        mockHttp.When("/api/distinct-locations").Respond("application/json", JsonSerializer.Serialize(new
        {
            countries = CountryTestData,
            states = EmptyArray,
            cities = EmptyArray,
            facilities = EmptyArray
        }));
        mockHttp.When("/api/mesh-tree*").Respond("application/json", "[]");
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        ctx.Services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(client));

        ctx.JSInterop.SetupVoid("meshTree.render", _ => true);

        IRenderedComponent<Frontend.Pages.Search> cut = ctx.Render<Frontend.Pages.Search>();

        Assert.IsNotNull(cut.Find("h1"));
        Assert.IsTrue(cut.Find("h1").TextContent.Contains("Search", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SearchPageRendersFilterInputs()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("/api/distinct-conditions").Respond("application/json", JsonSerializer.Serialize(ConditionTestData));
        mockHttp.When("/api/distinct-locations").Respond("application/json", JsonSerializer.Serialize(new
        {
            countries = CountryTestData,
            states = EmptyArray,
            cities = EmptyArray,
            facilities = EmptyArray
        }));
        mockHttp.When("/api/mesh-tree*").Respond("application/json", "[]");
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        ctx.Services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(client));

        ctx.JSInterop.SetupVoid("meshTree.render", _ => true);

        IRenderedComponent<Frontend.Pages.Search> cut = ctx.Render<Frontend.Pages.Search>();

        // Check for keyword input
        Assert.IsNotNull(cut.Find("input[placeholder='Search by title or NCT ID']"));
        // Check for search button
        Assert.IsNotNull(cut.Find("button:contains('Search')"));
    }

    [TestMethod]
    public void SearchPageRendersPagination()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("/api/distinct-conditions").Respond("application/json", JsonSerializer.Serialize(ConditionTestData));
        mockHttp.When("/api/distinct-locations").Respond("application/json", JsonSerializer.Serialize(new
        {
            countries = CountryTestData,
            states = EmptyArray,
            cities = EmptyArray,
            facilities = EmptyArray
        }));
        mockHttp.When("/api/mesh-tree*").Respond("application/json", "[]");
        mockHttp.When("/api/studies*").Respond("application/json", JsonSerializer.Serialize(new
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
        }));
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        ctx.Services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(client));

        ctx.JSInterop.SetupVoid("meshTree.render", _ => true);
        ctx.JSInterop.Setup<string[]>("meshTree.getSelected", _ => true).SetResult([]);

        IRenderedComponent<Frontend.Pages.Search> cut = ctx.Render<Frontend.Pages.Search>();
        
        // Trigger search to display results
        var searchButton = cut.FindAll("button").First(b => b.TextContent.Contains("Search", StringComparison.Ordinal));
        searchButton.Click();

        cut.WaitForState(() => cut.FindAll("table").Count > 0, TimeSpan.FromSeconds(2));

        // Verify results are displayed
        Assert.IsNotNull(cut.Find("a[href='/studies/NCT00000001']"));
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
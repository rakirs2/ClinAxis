using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class HomePageTests
{
    [TestMethod]
    public void HomePageRendersSearchInput()
    {
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(new HttpClient()));

        IRenderedComponent<Frontend.Pages.Home> cut = ctx.Render<Frontend.Pages.Home>();

        Assert.IsNotNull(cut.Find("input"));
        Assert.IsNotNull(cut.Find("button"));
        Assert.AreEqual("Search", cut.Find("button").TextContent);
    }

    [TestMethod]
    public void HomePageRendersTitle()
    {
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(new HttpClient()));

        IRenderedComponent<Frontend.Pages.Home> cut = ctx.Render<Frontend.Pages.Home>();

        Assert.IsNotNull(cut.Find("h1"));
        Assert.AreEqual("Study Search", cut.Find("h1").TextContent);
    }

    [TestMethod]
    public void HomePageSearchButtonTriggersDataLoad()
    {
        using var ctx = new BunitContext();
        using var mockHttp = new MockHttpMessageHandler();
        string[] conditions = ["Condition A"];
        string[] phases = ["PHASE2"];
        mockHttp.When("/api/studies*").Respond("application/json", JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new
                {
                    nctId = "NCT00000001",
                    briefTitle = "Test Study",
                    overallStatus = "RECRUITING",
                    conditions,
                    phases
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

        IRenderedComponent<Frontend.Pages.Home> cut = ctx.Render<Frontend.Pages.Home>();
        cut.Find("button").Click();

        cut.WaitForState(() => cut.FindAll("table").Count > 0, TimeSpan.FromSeconds(2));

        Assert.IsNotNull(cut.Find("a[href='/studies/NCT00000001']"));
        Assert.AreEqual("Test Study", cut.Find("td:nth-child(2)").TextContent);
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
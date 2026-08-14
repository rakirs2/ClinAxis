using System.Net;
using System.Text.Json;
using Bunit;
using Frontend.Components.Layouts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class NavBarTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [TestMethod]
    public void NavigationHasSevenTabs()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        Assert.AreEqual(6, cut.FindAll(".nav-link").Count);
    }

    [TestMethod]
    public void NavigationTabLabelsAreCorrect()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        Assert.AreEqual("Search", cut.FindAll(".nav-link")[0].TextContent.Trim());
        Assert.AreEqual("Investigators", cut.FindAll(".nav-link")[1].TextContent.Trim());
        Assert.AreEqual("Best-PI", cut.FindAll(".nav-link")[2].TextContent.Trim());
        Assert.AreEqual("Data Quality", cut.FindAll(".nav-link")[3].TextContent.Trim());
        Assert.AreEqual("Blog", cut.FindAll(".nav-link")[4].TextContent.Trim());
        Assert.AreEqual("Status", cut.FindAll(".nav-link")[5].TextContent.Trim());
    }

    [TestMethod]
    public void NavigationTabHrefsAreCorrect()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        Assert.AreEqual("/", cut.FindAll(".nav-link")[0].GetAttribute("href"));
        Assert.AreEqual("/investigators", cut.FindAll(".nav-link")[1].GetAttribute("href"));
        Assert.AreEqual("/recommend-investigators", cut.FindAll(".nav-link")[2].GetAttribute("href"));
        Assert.AreEqual("/data-quality", cut.FindAll(".nav-link")[3].GetAttribute("href"));
        Assert.AreEqual("/blog", cut.FindAll(".nav-link")[4].GetAttribute("href"));
        Assert.AreEqual("/status", cut.FindAll(".nav-link")[5].GetAttribute("href"));
    }

    private static HttpClient BuildClient(MockHttpMessageHandler mockHttp)
    {
        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("http://localhost:5003");
        return client;
    }

    private static void MockMainLayout(MockHttpMessageHandler mockHttp)
    {
        mockHttp.When("http://localhost:5003/api/page-views/stats?period=day")
            .Respond("application/json", JsonSerializer.Serialize(new { totalViews = 42, uniqueVisitors = 7 }, JsonOptions));

        mockHttp.When(HttpMethod.Post, "http://localhost:5003/api/page-views")
            .Respond(HttpStatusCode.OK, "application/json", "{\"id\": 1}");
    }
}

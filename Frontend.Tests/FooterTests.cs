using System.Net;
using System.Text.Json;
using Bunit;
using Frontend.Components.Layouts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace Frontend.Tests;

[TestClass]
public sealed class FooterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [TestMethod]
    public void FooterRendersDonationText()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        Assert.IsTrue(cut.Find("footer").TextContent.Contains("Support clinical trial research", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FooterRendersKoFiLink()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        var link = cut.Find("a[href='https://ko-fi.com/rakirs2']");
        Assert.IsNotNull(link);
        Assert.AreEqual("Donate on Ko-fi", link.TextContent.Trim());
        Assert.AreEqual("_blank", link.GetAttribute("target"));
    }

    [TestMethod]
    public void FooterRendersCreditText()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        Assert.IsTrue(cut.Find("footer").TextContent.Contains("Created by Srikar Mylavarapu", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FooterRendersGitHubLink()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        var link = cut.Find("a[href='https://github.com/rakirs2']");
        Assert.IsNotNull(link);
        Assert.AreEqual("Srikar Mylavarapu", link.TextContent.Trim());
        Assert.AreEqual("_blank", link.GetAttribute("target"));
    }

    [TestMethod]
    public void FooterRendersLinkedInLink()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        var link = cut.Find("a[href='https://linkedin.com/in/srikarmylavarapu']");
        Assert.IsNotNull(link);
        Assert.AreEqual("LinkedIn", link.TextContent.Trim());
        Assert.AreEqual("_blank", link.GetAttribute("target"));
    }

    [TestMethod]
    public void FooterRendersKoFiIcon()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        var link = cut.Find("a[href='https://ko-fi.com/rakirs2']");
        var icon = link.QuerySelector("i");
        Assert.IsNotNull(icon);
        Assert.IsTrue(icon.ClassList.Contains("bi-cup-hot-fill"));
    }

    [TestMethod]
    public void FooterRendersGitHubIcon()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        var link = cut.Find("a[href='https://github.com/rakirs2']");
        var icon = link.QuerySelector("i");
        Assert.IsNotNull(icon);
        Assert.IsTrue(icon.ClassList.Contains("bi-github"));
    }

    [TestMethod]
    public void FooterRendersLinkedInIcon()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        var link = cut.Find("a[href='https://linkedin.com/in/srikarmylavarapu']");
        var icon = link.QuerySelector("i");
        Assert.IsNotNull(icon);
        Assert.IsTrue(icon.ClassList.Contains("bi-linkedin"));
    }

    [TestMethod]
    public void FooterDoesNotRenderLegacyBadge()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        Assert.IsFalse(cut.Find("footer").TextContent.Contains("Grand Overlord Uma Mylavarapu", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FooterRendersVisitorCountWhenStatsAvailable()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        cut.WaitForState(() => cut.Markup.Contains("Visitors today:", StringComparison.Ordinal), timeout: TimeSpan.FromSeconds(5));
        var footer = cut.Find("footer");
        Assert.IsTrue(footer.TextContent.Contains("42", StringComparison.Ordinal), "Footer should show total views");
        Assert.IsTrue(footer.TextContent.Contains('7', StringComparison.Ordinal), "Footer should show unique visitors");
    }

    [TestMethod]
    public void FooterIsOutsideContainer()
    {
        using BunitContext ctx = new();
        using var mockHttp = new MockHttpMessageHandler();
        MockMainLayout(mockHttp);
        ctx.Services.AddSingleton(BuildClient(mockHttp));

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        var footer = cut.Find("footer");
        Assert.IsNotNull(footer);

        var containers = cut.FindAll(".container");
        Assert.IsTrue(containers.Count > 0);

        string html = cut.Markup;
        int lastContainerEnd = html.LastIndexOf("</div>", StringComparison.Ordinal);
        int footerStart = html.IndexOf("<footer", StringComparison.Ordinal);
        Assert.IsTrue(footerStart > lastContainerEnd, "Footer markup should appear after the last container div");
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

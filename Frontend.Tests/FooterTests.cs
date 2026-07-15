using Bunit;
using Frontend.Components.Layouts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class FooterTests
{
    [TestMethod]
    public void FooterRendersDonationText()
    {
        using BunitContext ctx = new();

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        Assert.IsTrue(cut.Find("footer").TextContent.Contains("Support clinical trial research", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FooterRendersKoFiLink()
    {
        using BunitContext ctx = new();

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

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        Assert.IsTrue(cut.Find("footer").TextContent.Contains("Created by Srikar Mylavarapu", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FooterRendersGitHubLink()
    {
        using BunitContext ctx = new();

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

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        var link = cut.Find("a[href='https://linkedin.com/in/srikarmylavarapu']");
        var icon = link.QuerySelector("i");
        Assert.IsNotNull(icon);
        Assert.IsTrue(icon.ClassList.Contains("bi-linkedin"));
    }

    [TestMethod]
    public void FooterRendersGrandOverlordBadge()
    {
        using BunitContext ctx = new();

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        var badge = cut.Find("span.badge");
        Assert.IsNotNull(badge);
        Assert.AreEqual("Grand Overlord Uma Mylavarapu", badge.TextContent.Trim());
        Assert.IsTrue(badge.ClassList.Contains("bg-warning"));
    }

    [TestMethod]
    public void FooterIsOutsideContainer()
    {
        using BunitContext ctx = new();

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
}

using Bunit;
using Frontend.Components.Layouts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class NavBarTests
{
    [TestMethod]
    public void NavigationHasFiveTabs()
    {
        using BunitContext ctx = new();

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        Assert.AreEqual(5, cut.FindAll(".nav-link").Count);
    }

    [TestMethod]
    public void NavigationTabLabelsAreCorrect()
    {
        using BunitContext ctx = new();

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        Assert.AreEqual("Search", cut.FindAll(".nav-link")[0].TextContent.Trim());
        Assert.AreEqual("Investigators", cut.FindAll(".nav-link")[1].TextContent.Trim());
        Assert.AreEqual("History", cut.FindAll(".nav-link")[2].TextContent.Trim());
        Assert.AreEqual("Dead Letter", cut.FindAll(".nav-link")[3].TextContent.Trim());
        Assert.AreEqual("Status", cut.FindAll(".nav-link")[4].TextContent.Trim());
    }

    [TestMethod]
    public void NavigationTabHrefsAreCorrect()
    {
        using BunitContext ctx = new();

        IRenderedComponent<MainLayout> cut = ctx.Render<MainLayout>(
            parameters => parameters.Add(p => p.Body, b => b.AddMarkupContent(0, string.Empty)));

        Assert.AreEqual("/", cut.FindAll(".nav-link")[0].GetAttribute("href"));
        Assert.AreEqual("/investigators", cut.FindAll(".nav-link")[1].GetAttribute("href"));
        Assert.AreEqual("/pipeline-runs", cut.FindAll(".nav-link")[2].GetAttribute("href"));
        Assert.AreEqual("/dead-letter", cut.FindAll(".nav-link")[3].GetAttribute("href"));
        Assert.AreEqual("/status", cut.FindAll(".nav-link")[4].GetAttribute("href"));
    }
}

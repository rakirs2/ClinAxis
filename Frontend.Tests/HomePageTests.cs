using Bunit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class HomePageTests
{
    [TestMethod]
    public void HomePageRendersSearchInput()
    {
        using var ctx = new BunitContext();
        IRenderedComponent<Frontend.Pages.Home> cut = ctx.Render<Frontend.Pages.Home>();
        Assert.IsNotNull(cut.Find("input"));
        Assert.IsNotNull(cut.Find("button"));
        Assert.AreEqual("Search", cut.Find("button").TextContent);
    }

    [TestMethod]
    public void HomePageRendersTitle()
    {
        using var ctx = new BunitContext();
        IRenderedComponent<Frontend.Pages.Home> cut = ctx.Render<Frontend.Pages.Home>();
        Assert.IsNotNull(cut.Find("h1"));
        Assert.AreEqual("Study Search", cut.Find("h1").TextContent);
    }
}
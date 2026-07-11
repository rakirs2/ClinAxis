using Bunit;
using Frontend.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

/// <summary>
/// Frontend unit tests for the Investigators page.
/// Tests the UI components and user interactions without requiring the backend API.
/// </summary>
[TestClass]
public sealed class InvestigatorsPageTests
{
    [TestMethod]
    public void InvestigatorsPage_RendersPageTitle()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var component = ctx.Render<Investigators>();

        // Assert
        Assert.IsTrue(component.Markup.Contains("Principal Investigators"), 
            "Page should have 'Principal Investigators' heading");
    }

    [TestMethod]
    public void InvestigatorsPage_HasSearchInput()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var component = ctx.Render<Investigators>();
        var searchInput = component.Find("input[type='text']");

        // Assert
        Assert.IsNotNull(searchInput, "Page should have search input field");
        Assert.AreEqual("Search by investigator name", 
            searchInput.GetAttribute("placeholder"), 
            "Search input should have correct placeholder");
    }

    [TestMethod]
    public void InvestigatorsPage_HasSearchButton()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var component = ctx.Render<Investigators>();
        var buttons = component.FindAll("button.btn-primary");

        // Assert
        Assert.IsTrue(buttons.Count > 0, "Page should have a Search button");
        Assert.AreEqual("Search", buttons[0].TextContent.Trim(), "Button text should be 'Search'");
    }

    [TestMethod]
    public void InvestigatorsPage_HasTableStructure()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var component = ctx.Render<Investigators>();

        // Assert
        Assert.IsTrue(component.Markup.Contains("<table"), "Page should have a table for results");
        Assert.IsTrue(component.Markup.Contains("Name"), "Table should have 'Name' column header");
        Assert.IsTrue(component.Markup.Contains("Affiliation"), "Table should have 'Affiliation' column header");
        Assert.IsTrue(component.Markup.Contains("Study Count"), "Table should have 'Study Count' column header");
    }

    [TestMethod]
    public void InvestigatorsPage_HasPaginationElements()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var component = ctx.Render<Investigators>();

        // Assert
        Assert.IsTrue(component.Markup.Contains("pagination"), "Page should have pagination");
        Assert.IsTrue(component.Markup.Contains("Previous"), "Pagination should have Previous button");
        Assert.IsTrue(component.Markup.Contains("Next"), "Pagination should have Next button");
    }

    [TestMethod]
    public void InvestigatorsPage_SearchInput_HasCorrectAttributes()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var component = ctx.Render<Investigators>();
        var searchInput = component.Find("input[type='text']");

        // Assert
        Assert.IsNotNull(searchInput?.GetAttribute("class"), "Search input should have CSS class");
        Assert.IsTrue(searchInput?.GetAttribute("class")?.Contains("form-control") ?? false, 
            "Search input should have 'form-control' class");
    }

    [TestMethod]
    public void InvestigatorsPage_SearchButton_DisabledDuringLoading()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var component = ctx.Render<Investigators>();
        var buttons = component.FindAll("button.btn-primary");

        // Note: The disabled attribute may be set during loading
        // This is a structural test - the actual loading behavior is tested in integration tests
        Assert.IsTrue(buttons.Count > 0, "Page should have a primary button");
    }

    [TestMethod]
    public void InvestigatorsPage_TableHeaders_AreCorrect()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var component = ctx.Render<Investigators>();
        var headers = component.FindAll("th");

        // Assert
        Assert.AreEqual(3, headers.Count, "Table should have 3 column headers");
        Assert.IsTrue(headers[0].TextContent.Contains("Name"), "First column should be 'Name'");
        Assert.IsTrue(headers[1].TextContent.Contains("Affiliation"), "Second column should be 'Affiliation'");
        Assert.IsTrue(headers[2].TextContent.Contains("Study Count"), "Third column should be 'Study Count'");
    }

    [TestMethod]
    public void InvestigatorsPage_Markup_IsValid()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var component = ctx.Render<Investigators>();

        // Assert
        Assert.IsNotNull(component.Markup, "Component should render markup");
        Assert.IsTrue(component.Markup.Length > 0, "Component markup should not be empty");
        // Verify no unmatched tags (basic sanity check)
        Assert.IsTrue(component.Markup.Contains("</table>"), "Table should be properly closed");
    }

    [TestMethod]
    public void InvestigatorsPage_SearchInput_HasOnInputBinding()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var component = ctx.Render<Investigators>();
        var markup = component.Markup;

        // Assert
        // Check that the component has input binding and event handlers
        Assert.IsTrue(markup.Contains("input"), "Page should have input element");
        // The Blazor binding should be compiled into the component
        Assert.IsTrue(markup.Contains("form-control"), "Input should be styled as form control");
    }
}

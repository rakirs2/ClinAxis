using DataApi.Endpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Scrapers.Tests.DataApiTests;

[TestClass]
public sealed class EndpointHelpersTests
{
    [TestMethod]
    public void ParseCsvParam_Null_ReturnsNull()
    {
        Assert.IsNull(EndpointHelpers.ParseCsvParam(null));
    }

    [TestMethod]
    public void ParseCsvParam_EmptyString_ReturnsNull()
    {
        Assert.IsNull(EndpointHelpers.ParseCsvParam(""));
    }

    [TestMethod]
    public void ParseCsvParam_WhitespaceOnly_ReturnsNull()
    {
        Assert.IsNull(EndpointHelpers.ParseCsvParam("   "));
    }

    [TestMethod]
    public void ParseCsvParam_SingleValue_ReturnsListWithOneItem()
    {
        var result = EndpointHelpers.ParseCsvParam("hello");
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("hello", result[0]);
    }

    [TestMethod]
    public void ParseCsvParam_MultipleValues_ReturnsAllItems()
    {
        var result = EndpointHelpers.ParseCsvParam("a,b,c");
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("a", result[0]);
        Assert.AreEqual("b", result[1]);
        Assert.AreEqual("c", result[2]);
    }

    [TestMethod]
    public void ParseCsvParam_TrailingComma_RemovesEmptyEntry()
    {
        var result = EndpointHelpers.ParseCsvParam("a,");
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("a", result[0]);
    }

    [TestMethod]
    public void ParseCsvParam_LeadingComma_RemovesEmptyEntry()
    {
        var result = EndpointHelpers.ParseCsvParam(",a");
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("a", result[0]);
    }

    [TestMethod]
    public void ParseCsvParam_InnerEmptyEntries_AreRemoved()
    {
        var result = EndpointHelpers.ParseCsvParam("a,,b");
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("a", result[0]);
        Assert.AreEqual("b", result[1]);
    }

    [TestMethod]
    public void ParseCsvParam_WhitespaceAroundValues_IsTrimmed()
    {
        var result = EndpointHelpers.ParseCsvParam(" a , b , c ");
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("a", result[0]);
        Assert.AreEqual("b", result[1]);
        Assert.AreEqual("c", result[2]);
    }

    [TestMethod]
    public void ParseCsvParam_MixedWhitespaceAndEmpties_Handled()
    {
        var result = EndpointHelpers.ParseCsvParam(" a, ,b ,, c ");
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("a", result[0]);
        Assert.AreEqual("b", result[1]);
        Assert.AreEqual("c", result[2]);
    }

    [TestMethod]
    public void ParseCsvParam_SingleValueWithSpaces_Trimmed()
    {
        var result = EndpointHelpers.ParseCsvParam("  spaced  ");
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("spaced", result[0]);
    }

    [TestMethod]
    public void ParseCsvParam_CommaOnly_ReturnsEmptyList()
    {
        var result = EndpointHelpers.ParseCsvParam(",");
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ParseCsvParam_MultipleCommasOnly_ReturnsEmptyList()
    {
        var result = EndpointHelpers.ParseCsvParam(",,,,");
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }
}

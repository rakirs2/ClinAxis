using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class PaginationHelpersTests
{
    [TestMethod]
    public void GetPaginationRangeFewerPagesThanMaxVisibleAllPagesListed()
    {
        var range = PaginationHelpers.GetPaginationRange(1, 3);

        Assert.AreEqual("1,2,3", string.Join(",", range.Select(p => p?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")));
    }

    [TestMethod]
    public void GetPaginationRangeMiddlePageAddsFirstAndLastWithEllipses()
    {
        var range = PaginationHelpers.GetPaginationRange(50, 100);

        Assert.AreEqual("1,null,45,46,47,48,49,50,51,52,53,54,null,100", string.Join(",", range.Select(p => p?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")));
    }

    [TestMethod]
    public void GetPaginationRangeFirstPageNoLeadingEllipsis()
    {
        var range = PaginationHelpers.GetPaginationRange(1, 50);

        Assert.AreEqual("1,2,3,4,5,6,7,8,9,10,null,50", string.Join(",", range.Select(p => p?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")));
    }

    [TestMethod]
    public void GetPaginationRangeLastPageNoTrailingEllipsis()
    {
        var range = PaginationHelpers.GetPaginationRange(50, 50);

        Assert.AreEqual("1,null,41,42,43,44,45,46,47,48,49,50", string.Join(",", range.Select(p => p?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")));
    }

    [TestMethod]
    public void GetPaginationRangeCustomMaxVisibleRespected()
    {
        var range = PaginationHelpers.GetPaginationRange(5, 100, maxVisible: 4);

        Assert.AreEqual("1,null,3,4,5,6,null,100", string.Join(",", range.Select(p => p?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")));
    }

    [TestMethod]
    public void GetPaginationRangeSinglePageReturnsSingleEntry()
    {
        var range = PaginationHelpers.GetPaginationRange(1, 1);

        Assert.AreEqual("1", string.Join(",", range.Select(p => p?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")));
    }

    [TestMethod]
    public void GetPaginationRangeFirstWindowNoDoubleEllipsis()
    {
        var range = PaginationHelpers.GetPaginationRange(3, 50);

        Assert.AreEqual("1,2,3,4,5,6,7,8,9,10,null,50", string.Join(",", range.Select(p => p?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")));
    }
}

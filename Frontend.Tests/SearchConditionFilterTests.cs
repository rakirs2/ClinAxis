using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class SearchConditionFilterTests
{
    [TestMethod]
    public void FilterCaseInsensitiveMatchReturnsMatches()
    {
        var result = SearchConditionFilter.Filter(
            ["Diabetes Mellitus", "Hypertension", "Asthma"], "dia");

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Diabetes Mellitus", result[0]);
    }

    [TestMethod]
    public void FilterMultipleMatchesAreCappedAtTwenty()
    {
        var all = Enumerable.Range(1, 40).Select(i => $"Condition {i}").ToList();
        var result = SearchConditionFilter.Filter(all, "condition");

        Assert.AreEqual(20, result.Count);
        Assert.AreEqual("Condition 1", result[0]);
        Assert.AreEqual("Condition 20", result[^1]);
    }

    [TestMethod]
    public void FilterNoMatchReturnsEmpty()
    {
        var result = SearchConditionFilter.Filter(
            ["Diabetes Mellitus", "Hypertension"], "zebra");

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void FilterEmptyTextReturnsEmpty()
    {
        var result = SearchConditionFilter.Filter(
            ["Diabetes Mellitus", "Hypertension"], "");

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void FilterNullListReturnsEmpty()
    {
        var result = SearchConditionFilter.Filter(null, "diabetes");

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void FilterWhitespaceTextReturnsEmpty()
    {
        var result = SearchConditionFilter.Filter(
            ["Diabetes Mellitus", "Hypertension"], "   ");

        Assert.AreEqual(0, result.Count);
    }
}

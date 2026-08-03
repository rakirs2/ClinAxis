using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class DataQualityFormattingTests
{
    [TestMethod]
    public void FormatDateRendersUtcDateAndTime()
    {
        var dt = new DateTime(2026, 8, 3, 14, 5, 9);

        Assert.AreEqual("2026-08-03 14:05 UTC", DataQualityFormatting.FormatDate(dt));
    }

    [TestMethod]
    public void TruncatePayloadNullReturnsDashDash()
    {
        Assert.AreEqual("--", DataQualityFormatting.TruncatePayload(null));
    }

    [TestMethod]
    public void TruncatePayloadEmptyReturnsDashDash()
    {
        Assert.AreEqual("--", DataQualityFormatting.TruncatePayload(""));
    }

    [TestMethod]
    public void TruncatePayloadShortPayloadIsUnchanged()
    {
        var payload = new string('x', 50);
        Assert.AreEqual(payload, DataQualityFormatting.TruncatePayload(payload));
    }

    [TestMethod]
    public void TruncatePayloadLongPayloadIsTruncated()
    {
        var payload = new string('x', 60);
        var result = DataQualityFormatting.TruncatePayload(payload);

        Assert.AreEqual(53, result.Length);
        Assert.IsTrue(result.EndsWith("...", StringComparison.Ordinal));
        Assert.IsTrue(result.StartsWith(new string('x', 50), StringComparison.Ordinal));
    }

    [TestMethod]
    public void TruncateTextNullReturnsEmpty()
    {
        Assert.AreEqual("", DataQualityFormatting.TruncateText(null, 80));
    }

    [TestMethod]
    public void TruncateTextShortTextIsUnchanged()
    {
        Assert.AreEqual("short", DataQualityFormatting.TruncateText("short", 80));
    }

    [TestMethod]
    public void TruncateTextLongTextIsTruncated()
    {
        var text = new string('y', 100);
        var result = DataQualityFormatting.TruncateText(text, 80);

        Assert.AreEqual(83, result.Length);
        Assert.IsTrue(result.EndsWith("...", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GetCountNullBreakdownReturnsZero()
    {
        Assert.AreEqual(0, DataQualityFormatting.GetCount(null, "assigned"));
    }

    [TestMethod]
    public void GetCountMissingKeyReturnsZero()
    {
        var breakdown = new Dictionary<string, int> { ["assigned"] = 85 };
        Assert.AreEqual(0, DataQualityFormatting.GetCount(breakdown, "pending"));
    }

    [TestMethod]
    public void GetCountPresentKeyReturnsValue()
    {
        var breakdown = new Dictionary<string, int> { ["ambiguous"] = 84 };
        Assert.AreEqual(84, DataQualityFormatting.GetCount(breakdown, "ambiguous"));
    }
}

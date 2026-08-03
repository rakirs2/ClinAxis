using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class StatusFormattingTests
{
    [TestMethod]
    public void FormatDurationNullReturnsDash()
    {
        Assert.AreEqual("--", StatusFormatting.FormatDuration(null));
    }

    [TestMethod]
    public void FormatDurationZeroReturnsDash()
    {
        Assert.AreEqual("--", StatusFormatting.FormatDuration(0));
    }

    [TestMethod]
    public void FormatDurationNegativeReturnsDash()
    {
        Assert.AreEqual("--", StatusFormatting.FormatDuration(-5000));
    }

    [TestMethod]
    public void FormatDurationSecondsOnly()
    {
        Assert.AreEqual("~50s", StatusFormatting.FormatDuration(50000));
    }

    [TestMethod]
    public void FormatDurationSubSecondRoundsDownToSeconds()
    {
        Assert.AreEqual("~5s", StatusFormatting.FormatDuration(5000));
    }

    [TestMethod]
    public void FormatDurationMinutesAndSeconds()
    {
        Assert.AreEqual("~1m 30s", StatusFormatting.FormatDuration(90000));
    }

    [TestMethod]
    public void FormatDurationExactMinute()
    {
        Assert.AreEqual("~1m 0s", StatusFormatting.FormatDuration(60000));
    }

    [TestMethod]
    public void FormatDurationJustUnderOneSecond()
    {
        Assert.AreEqual("~0s", StatusFormatting.FormatDuration(999));
    }
}

using System.Globalization;
using DataApi.Endpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataApi.Tests;

[TestClass]
public sealed class PiFeaturesExportValidationTests
{
    [TestMethod]
    public void ValidateWindow_MissingParams_ReturnsError()
    {
        string? error = PiFeaturesExportEndpoints.ValidateWindow(null, "2020-01-01", out _, out _);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "'from'", StringComparison.Ordinal);

        error = PiFeaturesExportEndpoints.ValidateWindow("2020-01-01", null, out _, out _);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "'to'", StringComparison.Ordinal);

        error = PiFeaturesExportEndpoints.ValidateWindow("", "", out _, out _);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void ValidateWindow_InvalidDate_ReturnsError()
    {
        string? error = PiFeaturesExportEndpoints.ValidateWindow("banana", "2020-01-01", out _, out _);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "'from'", StringComparison.Ordinal);

        error = PiFeaturesExportEndpoints.ValidateWindow("2020-01-01", "not-a-date", out _, out _);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "'to'", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ValidateWindow_DateBefore2000_ReturnsError()
    {
        string? error = PiFeaturesExportEndpoints.ValidateWindow("1999-12-31", "2020-01-01", out _, out _);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "2000-01-01", StringComparison.Ordinal);

        error = PiFeaturesExportEndpoints.ValidateWindow("2000-01-01", "1999-12-31", out _, out _);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "2000-01-01", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ValidateWindow_DateAfterToday_ReturnsError()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.Today).AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string? error = PiFeaturesExportEndpoints.ValidateWindow("2000-01-01", tomorrow, out _, out _);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "today", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ValidateWindow_FromAfterTo_ReturnsError()
    {
        string? error = PiFeaturesExportEndpoints.ValidateWindow("2020-01-01", "2019-12-31", out _, out _);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "must not be after", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ValidateWindow_ValidWindow_SetsBothDates()
    {
        string? error = PiFeaturesExportEndpoints.ValidateWindow("2020-01-01", "2020-12-31", out DateOnly start, out DateOnly end);
        Assert.IsNull(error);
        Assert.AreEqual(new DateOnly(2020, 1, 1), start);
        Assert.AreEqual(new DateOnly(2020, 12, 31), end);
    }
}

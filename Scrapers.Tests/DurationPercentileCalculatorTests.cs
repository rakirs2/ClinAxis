using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services.EventQueue;

namespace Scrapers.Tests;

[TestClass]
public sealed class DurationPercentileCalculatorTests
{
    [TestMethod]
    public void ComputePercentiles_IdenticalDurations_ReturnsSameForAllPercentiles()
    {
        var durations = new double[] { 100, 100, 100, 100, 100 };
        var result = DurationPercentileCalculator.ComputePercentiles(durations);

        Assert.AreEqual(5, result.Count);
        Assert.AreEqual(100, result.MinMs);
        Assert.AreEqual(100, result.P50Ms);
        Assert.AreEqual(100, result.P95Ms);
        Assert.AreEqual(100, result.P99Ms);
        Assert.AreEqual(100, result.MaxMs);
    }

    [TestMethod]
    public void ComputePercentiles_SingleValue_ReturnsSameForAll()
    {
        var durations = new double[] { 42 };
        var result = DurationPercentileCalculator.ComputePercentiles(durations);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(42, result.MinMs);
        Assert.AreEqual(42, result.P50Ms);
        Assert.AreEqual(42, result.P95Ms);
        Assert.AreEqual(42, result.P99Ms);
        Assert.AreEqual(42, result.MaxMs);
    }

    [TestMethod]
    public void ComputePercentiles_KnownValues_ReturnsCorrectPercentiles()
    {
        var durations = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
        var result = DurationPercentileCalculator.ComputePercentiles(durations);

        Assert.AreEqual(20, result.Count);
        Assert.AreEqual(1, result.MinMs);
        Assert.AreEqual(20, result.MaxMs);
        // P50 (median) of 1..20 = ~10.5
        Assert.AreEqual(10.5, result.P50Ms, 0.01);
        // P95 of 1..20 = linear interpolation at rank 95% * 19 = 18.05 → value at index 18 (19) + 0.05 * (20 - 19) = 19.05
        Assert.AreEqual(19.05, result.P95Ms, 0.01);
        // P99 of 1..20 = linear interpolation at rank 99% * 19 = 18.81 → value at index 18 (19) + 0.81 * (20 - 19) = 19.81
        Assert.AreEqual(19.81, result.P99Ms, 0.01);
    }

    [TestMethod]
    public void ComputePercentiles_UnsortedInput_StillReturnsCorrectValues()
    {
        var durations = new double[] { 100, 5, 50, 1, 200 };
        var result = DurationPercentileCalculator.ComputePercentiles(durations);

        Assert.AreEqual(5, result.Count);
        Assert.AreEqual(1, result.MinMs);
        Assert.AreEqual(200, result.MaxMs);
        // sorted: 1, 5, 50, 100, 200. P50 = value at rank 50% * 4 = 2.0 → index 2 = 50
        Assert.AreEqual(50, result.P50Ms, 0.01);
    }

    [TestMethod]
    public void ComputePercentiles_EmptyList_ReturnsZeroCount()
    {
        var durations = Array.Empty<double>();
        var result = DurationPercentileCalculator.ComputePercentiles(durations);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ComputePercentiles_NullInput_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            DurationPercentileCalculator.ComputePercentiles(null!));
    }

    [TestMethod]
    public void ComputePercentiles_TwoValues_ReturnsExactValues()
    {
        var durations = new double[] { 10, 20 };
        var result = DurationPercentileCalculator.ComputePercentiles(durations);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(10, result.MinMs);
        Assert.AreEqual(20, result.MaxMs);
        // P50 of [10, 20] = rank 50% * 1 = 0.5 → 10 + 0.5 * (20-10) = 15
        Assert.AreEqual(15, result.P50Ms, 0.01);
        // P95 of [10, 20] = rank 95% * 1 = 0.95 → 10 + 0.95 * (20-10) = 19.5
        Assert.AreEqual(19.5, result.P95Ms, 0.01);
    }
}
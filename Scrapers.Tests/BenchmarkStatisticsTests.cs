using MeshBench;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Scrapers.Tests;

[TestClass]
public sealed class BenchmarkStatisticsTests
{
    [TestMethod]
    public void PercentileInterpolatesBetweenSamples()
    {
        var result = BenchmarkStatistics.Percentile([10, 20, 30, 40], 95);

        Assert.AreEqual(38.5, result, 0.0001);
    }

    [TestMethod]
    public void PercentileRejectsEmptySamples()
    {
        Assert.Throws<ArgumentException>(() => BenchmarkStatistics.Percentile([], 50));
    }
}

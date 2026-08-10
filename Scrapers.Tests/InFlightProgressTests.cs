using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class InFlightProgressTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void Calculate_NullEverything_ReturnsNulls()
    {
        var (percent, rate, eta) = InFlightProgress.Calculate(null, null, null, Now);

        Assert.IsNull(percent);
        Assert.IsNull(rate);
        Assert.IsNull(eta);
    }

    [TestMethod]
    public void Calculate_ZeroProcessed_NullTotal_ReturnsNulls()
    {
        var (percent, rate, eta) = InFlightProgress.Calculate(0, null, Now.AddMinutes(-10), Now);

        Assert.IsNull(percent);
        Assert.IsNull(rate);
        Assert.IsNull(eta);
    }

    [TestMethod]
    public void Calculate_PartialProgress_ComputesPercentRateAndEta()
    {
        var claimedAt = Now.AddMinutes(-20);
        var (percent, rate, eta) = InFlightProgress.Calculate(50, 200, claimedAt, Now);

        Assert.AreEqual(25.0, percent!.Value, 0.001);
        Assert.AreEqual(2.5, rate!.Value, 0.001);
        Assert.IsNotNull(eta);
        Assert.IsTrue(eta.Value > Now, "ETA must be in the future while progress is incomplete.");
        var expectedEta = Now.AddMinutes(150 / 2.5);
        Assert.AreEqual(expectedEta, eta.Value);
    }

    [TestMethod]
    public void Calculate_CompleteProgress_PercentCapsAt100AndNoEta()
    {
        var (percent, rate, eta) = InFlightProgress.Calculate(200, 200, Now.AddMinutes(-20), Now);

        Assert.AreEqual(100.0, percent!.Value, 0.001);
        Assert.IsNotNull(rate);
        Assert.IsNull(eta, "No ETA once everything is processed.");
    }

    [TestMethod]
    public void Calculate_ProcessedExceedsTotal_CapsPercentAt100()
    {
        var (percent, _, _) = InFlightProgress.Calculate(250, 200, Now.AddMinutes(-20), Now);

        Assert.AreEqual(100.0, percent!.Value, 0.001);
    }

    [TestMethod]
    public void Calculate_NoClaimYet_NullRateEvenWithProgress()
    {
        var (_, rate, eta) = InFlightProgress.Calculate(10, 100, null, Now);

        Assert.IsNull(rate, "Rate needs a claim timestamp to anchor the elapsed time.");
        Assert.IsNull(eta);
    }

    [TestMethod]
    public void Calculate_NegativeProcessed_TreatedAsZero()
    {
        var (percent, rate, _) = InFlightProgress.Calculate(-5, 100, Now.AddMinutes(-10), Now);

        Assert.AreEqual(0.0, percent!.Value, 0.001);
        Assert.IsNull(rate);
    }

    [TestMethod]
    public void Calculate_ZeroTotal_PercentNull()
    {
        var (percent, rate, _) = InFlightProgress.Calculate(5, 0, Now.AddMinutes(-10), Now);

        Assert.IsNull(percent, "Percent is unknown when no total was reported.");
        Assert.IsNotNull(rate, "Rate can still be derived from processed time elapsed.");
    }
}

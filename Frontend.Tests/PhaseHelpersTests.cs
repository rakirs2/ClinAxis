using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class PhaseHelpersTests
{
    [TestMethod]
    public void DisplayNameEarlyPhase1ReturnsHumanReadable()
    {
        Assert.AreEqual("Early Phase 1", PhaseHelpers.DisplayName("EARLY_PHASE1"));
    }

    [TestMethod]
    public void DisplayNamePhase1ReturnsHumanReadable()
    {
        Assert.AreEqual("Phase 1", PhaseHelpers.DisplayName("PHASE1"));
    }

    [TestMethod]
    public void DisplayNamePhase2ReturnsHumanReadable()
    {
        Assert.AreEqual("Phase 2", PhaseHelpers.DisplayName("PHASE2"));
    }

    [TestMethod]
    public void DisplayNamePhase3ReturnsHumanReadable()
    {
        Assert.AreEqual("Phase 3", PhaseHelpers.DisplayName("PHASE3"));
    }

    [TestMethod]
    public void DisplayNamePhase4ReturnsHumanReadable()
    {
        Assert.AreEqual("Phase 4", PhaseHelpers.DisplayName("PHASE4"));
    }

    [TestMethod]
    public void DisplayNameNAReturnsNotApplicable()
    {
        Assert.AreEqual("Not Applicable", PhaseHelpers.DisplayName("NA"));
    }

    [TestMethod]
    public void DisplayNameUnknownValueReturnsOriginal()
    {
        Assert.AreEqual("UNKNOWN_PHASE", PhaseHelpers.DisplayName("UNKNOWN_PHASE"));
    }

    [TestMethod]
    public void DisplayNameEmptyStringReturnsEmpty()
    {
        Assert.AreEqual("", PhaseHelpers.DisplayName(""));
    }

    [TestMethod]
    public void DisplayPhasesWithNullReturnsNone()
    {
        Assert.AreEqual("None", PhaseHelpers.DisplayPhases(null));
    }

    [TestMethod]
    public void DisplayPhasesWithEmptyListReturnsEmpty()
    {
        Assert.AreEqual("", PhaseHelpers.DisplayPhases([]));
    }

    [TestMethod]
    public void DisplayPhasesWithMultiplePhasesJoinsWithCommas()
    {
        List<string> phases = ["PHASE1", "PHASE3"];
        Assert.AreEqual("Phase 1, Phase 3", PhaseHelpers.DisplayPhases(phases));
    }

    [TestMethod]
    public void DisplayPhasesWithNAReturnsNotApplicable()
    {
        List<string> phases = ["NA"];
        Assert.AreEqual("Not Applicable", PhaseHelpers.DisplayPhases(phases));
    }

    [TestMethod]
    public void DisplayPhasesMixedValuesMapsAllCorrectly()
    {
        List<string> phases = ["EARLY_PHASE1", "PHASE2", "NA"];
        Assert.AreEqual("Early Phase 1, Phase 2, Not Applicable", PhaseHelpers.DisplayPhases(phases));
    }
}

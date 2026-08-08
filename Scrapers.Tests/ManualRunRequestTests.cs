using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class ManualRunRequestTests
{
    [TestMethod]
    [DataRow("incremental")]
    [DataRow("full")]
    [DataRow("INCREMENTAL")]
    [DataRow("Full")]
    [DataRow("FULL")]
    public void IsValidMode_AcceptsSupportedModes(string mode)
    {
        Assert.IsTrue(ManualRunRequest.IsValidMode(mode));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("bogus")]
    [DataRow("incrementalx")]
    [DataRow("fullnow")]
    public void IsValidMode_RejectsUnsupportedModes(string? mode)
    {
        Assert.IsFalse(ManualRunRequest.IsValidMode(mode));
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class IncrementalDiscoveryEventPayloadTests
{
    [TestMethod]
    public void ToJson_RoundTripsBoundedWindow()
    {
        var from = new DateTime(2026, 8, 7, 20, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 8, 7, 21, 0, 0, DateTimeKind.Utc);

        var original = new IncrementalDiscoveryEventPayload(12, from, to);

        Assert.IsTrue(IncrementalDiscoveryEventPayload.TryParse(original.ToJson(), out var parsed));
        Assert.IsNotNull(parsed);
        Assert.AreEqual(12, parsed.Count);
        Assert.AreEqual(from, parsed.LastUpdatedPost);
        Assert.AreEqual(to, parsed.LastUpdatedPostTo);
        Assert.IsTrue(parsed.HasWindow);
    }

    [TestMethod]
    public void TryParse_AcceptsLegacyCountOnlyPayload()
    {
        Assert.IsTrue(IncrementalDiscoveryEventPayload.TryParse("{\"count\":7}", out var parsed));
        Assert.IsNotNull(parsed);
        Assert.AreEqual(7, parsed.Count);
        Assert.IsNull(parsed.LastUpdatedPost);
        Assert.IsNull(parsed.LastUpdatedPostTo);
        Assert.IsFalse(parsed.HasWindow);
    }

    [TestMethod]
    public void TryParse_RejectsInvalidWindow()
    {
        const string payload = "{\"count\":7,\"lastUpdatedPost\":\"2026-08-08T00:00:00Z\",\"lastUpdatedPostTo\":\"2026-08-07T00:00:00Z\"}";

        Assert.IsFalse(IncrementalDiscoveryEventPayload.TryParse(payload, out _));
    }

    [TestMethod]
    public void TryParse_RejectsMissingOrInvalidCount()
    {
        Assert.IsFalse(IncrementalDiscoveryEventPayload.TryParse(null, out _));
        Assert.IsFalse(IncrementalDiscoveryEventPayload.TryParse("{}", out _));
        Assert.IsFalse(IncrementalDiscoveryEventPayload.TryParse("{\"count\":0}", out _));
        Assert.IsFalse(IncrementalDiscoveryEventPayload.TryParse("{\"count\":\"many\"}", out _));
    }
}

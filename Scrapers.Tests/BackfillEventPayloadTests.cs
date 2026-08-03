using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class BackfillEventPayloadTests
{
    [TestMethod]
    public void TryParse_ValidPayload_ReturnsValues()
    {
        const string data = """{"chunkIndex":3,"count":10000,"dateFrom":"2026-07-01","dateTo":"2026-07-31","sweepStartedUtc":"2026-08-03T12:00:00Z"}""";

        Assert.IsTrue(BackfillEventPayload.TryParse(data, out var payload));
        Assert.IsNotNull(payload);
        Assert.AreEqual(3, payload.ChunkIndex);
        Assert.AreEqual(10000, payload.Count);
        Assert.AreEqual(new DateOnly(2026, 7, 1), payload.DateFrom);
        Assert.AreEqual(new DateOnly(2026, 7, 31), payload.DateTo);
        Assert.AreEqual(new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc), payload.SweepStartedUtc);
    }

    [TestMethod]
    public void TryParse_MissingChunkIndex_IsAllowed()
    {
        const string data = """{"count":500,"dateFrom":"2026-07-01","dateTo":"2026-07-31","sweepStartedUtc":"2026-08-03T12:00:00Z"}""";

        Assert.IsTrue(BackfillEventPayload.TryParse(data, out var payload));
        Assert.IsNotNull(payload);
        Assert.IsNull(payload.ChunkIndex);
    }

    [TestMethod]
    public void TryParse_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(BackfillEventPayload.TryParse(null, out _));
        Assert.IsFalse(BackfillEventPayload.TryParse("", out _));
        Assert.IsFalse(BackfillEventPayload.TryParse("   ", out _));
    }

    [TestMethod]
    public void TryParse_MalformedJson_ReturnsFalse()
    {
        Assert.IsFalse(BackfillEventPayload.TryParse("{not json", out _));
    }

    [TestMethod]
    public void TryParse_MissingFields_ReturnsFalse()
    {
        Assert.IsFalse(BackfillEventPayload.TryParse("""{"count":500,"dateFrom":"2026-07-01","sweepStartedUtc":"2026-08-03T12:00:00Z"}""", out _));
        Assert.IsFalse(BackfillEventPayload.TryParse("""{"count":500,"dateTo":"2026-07-31","sweepStartedUtc":"2026-08-03T12:00:00Z"}""", out _));
        Assert.IsFalse(BackfillEventPayload.TryParse("""{"dateFrom":"2026-07-01","dateTo":"2026-07-31","sweepStartedUtc":"2026-08-03T12:00:00Z"}""", out _));
        Assert.IsFalse(BackfillEventPayload.TryParse("""{"count":500,"dateFrom":"2026-07-01","dateTo":"2026-07-31"}""", out _));
    }

    [TestMethod]
    public void TryParse_InvalidValues_ReturnsFalse()
    {
        Assert.IsFalse(BackfillEventPayload.TryParse("""{"count":0,"dateFrom":"2026-07-01","dateTo":"2026-07-31","sweepStartedUtc":"2026-08-03T12:00:00Z"}""", out _));
        Assert.IsFalse(BackfillEventPayload.TryParse("""{"count":-5,"dateFrom":"2026-07-01","dateTo":"2026-07-31","sweepStartedUtc":"2026-08-03T12:00:00Z"}""", out _));
        Assert.IsFalse(BackfillEventPayload.TryParse("""{"count":"many","dateFrom":"2026-07-01","dateTo":"2026-07-31","sweepStartedUtc":"2026-08-03T12:00:00Z"}""", out _));
        Assert.IsFalse(BackfillEventPayload.TryParse("""{"count":500,"dateFrom":"07/01/2026","dateTo":"2026-07-31","sweepStartedUtc":"2026-08-03T12:00:00Z"}""", out _));
        Assert.IsFalse(BackfillEventPayload.TryParse("""{"count":500,"dateFrom":"2026-08-01","dateTo":"2026-07-31","sweepStartedUtc":"2026-08-03T12:00:00Z"}""", out _));
        Assert.IsFalse(BackfillEventPayload.TryParse("""{"count":500,"dateFrom":"2026-07-01","dateTo":"2026-07-31","sweepStartedUtc":"not-a-date"}""", out _));
    }
}

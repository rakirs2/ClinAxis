using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class ConditionDecisionTests
{
    [TestMethod]
    public void MatcherUnavailable_SerializesTermAndReason()
    {
        var json = JsonSerializer.Serialize(ConditionDecision.MatcherUnavailable("Hypertension"));

        StringAssert.Contains(json, "\"term\":\"Hypertension\"", StringComparison.Ordinal);
        StringAssert.Contains(json, "\"reason\":\"mesh_matcher_not_initialized\"", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CuiNotFound_SerializesTermReasonMeshCuiAndSimilarity()
    {
        var json = JsonSerializer.Serialize(ConditionDecision.CuiNotFound("E11.9", "C0011860", 0.82));

        StringAssert.Contains(json, "\"term\":\"E11.9\"", StringComparison.Ordinal);
        StringAssert.Contains(json, "\"reason\":\"cui_not_found\"", StringComparison.Ordinal);
        StringAssert.Contains(json, "\"meshCui\":\"C0011860\"", StringComparison.Ordinal);
        StringAssert.Contains(json, "\"similarity\":0.82", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Rejected_SerializesTermReasonAndSimilarity()
    {
        var json = JsonSerializer.Serialize(ConditionDecision.Rejected("C.O.P.D.", "low_similarity", 0.41));

        StringAssert.Contains(json, "\"term\":\"C.O.P.D.\"", StringComparison.Ordinal);
        StringAssert.Contains(json, "\"reason\":\"low_similarity\"", StringComparison.Ordinal);
        StringAssert.Contains(json, "\"similarity\":0.41", StringComparison.Ordinal);
        Assert.IsFalse(json.Contains("meshCui", StringComparison.Ordinal), "Rejected record must not include meshCui to preserve JSON shape");
    }
}

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class DataApiSchemaGuardTests
{
    [TestMethod]
    public void StudiesSearchResponse_HasRequiredFields()
    {
        var json = FixtureLoader.LoadDataApiJson("studies-search-response.json");
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.IsTrue(root.TryGetProperty("data", out JsonElement data), "Response must contain 'data'");
        Assert.AreEqual(JsonValueKind.Array, data.ValueKind, "'data' must be an array");

        Assert.IsTrue(root.TryGetProperty("total", out JsonElement total), "Response must contain 'total'");
        Assert.AreEqual(JsonValueKind.Number, total.ValueKind, "'total' must be a number");

        Assert.IsTrue(root.TryGetProperty("page", out _), "Response must contain 'page'");
        Assert.IsTrue(root.TryGetProperty("pageSize", out _), "Response must contain 'pageSize'");
        Assert.IsTrue(root.TryGetProperty("totalPages", out _), "Response must contain 'totalPages'");
    }

    [TestMethod]
    public void StudySummary_HasRequiredFields()
    {
        var json = FixtureLoader.LoadDataApiJson("studies-search-response.json");
        using var doc = JsonDocument.Parse(json);
        JsonElement study = doc.RootElement.GetProperty("data")[0];

        Assert.IsTrue(study.TryGetProperty("nctId", out JsonElement nctId), "Study must contain 'nctId'");
        Assert.AreEqual(JsonValueKind.String, nctId.ValueKind, "'nctId' must be a string");
        Assert.IsFalse(string.IsNullOrWhiteSpace(nctId.GetString()), "'nctId' must not be empty");

        Assert.IsTrue(study.TryGetProperty("briefTitle", out _), "Study must contain 'briefTitle'");
        Assert.IsTrue(study.TryGetProperty("overallStatus", out _), "Study must contain 'overallStatus'");

        Assert.IsTrue(study.TryGetProperty("studyType", out _), "Study must contain 'studyType'");
        Assert.IsTrue(study.TryGetProperty("enrollmentCount", out _), "Study must contain 'enrollmentCount'");
        Assert.IsTrue(study.TryGetProperty("investigatorCount", out _), "Study must contain 'investigatorCount'");
        Assert.IsTrue(study.TryGetProperty("pubmedPaperCount", out _), "Study must contain 'pubmedPaperCount'");
        Assert.IsTrue(study.TryGetProperty("isIncomplete", out _), "Study must contain 'isIncomplete'");

        Assert.IsTrue(study.TryGetProperty("conditions", out JsonElement conditions), "Study must contain 'conditions'");
        Assert.AreEqual(JsonValueKind.Array, conditions.ValueKind, "'conditions' must be an array");

        Assert.IsTrue(study.TryGetProperty("phases", out JsonElement phases), "Study must contain 'phases'");
        Assert.AreEqual(JsonValueKind.Array, phases.ValueKind, "'phases' must be an array");
    }
}

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models;
using Scrapers.Services;

namespace Scrapers.Tests;

[TestClass]
public sealed class MeSHMatcherBatchTests
{
    private static MeSHMatcher? s_matcher;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        var resourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources", "mesh");
        Assert.IsTrue(Directory.Exists(resourcesPath), $"MeSH resources missing: {resourcesPath}");
        s_matcher = new MeSHMatcher(resourcesPath);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        s_matcher?.Dispose();
    }

    private static List<string> LoadFixtureTerms()
    {
        var terms = new List<string>();
        foreach (var fixture in new[] { "studies-page1.json", "studies-page2.json" })
        {
            var json = Helpers.FixtureLoader.LoadClinicalTrialsGovJson(fixture);
            using var doc = JsonDocument.Parse(json);
            foreach (var study in doc.RootElement.GetProperty("studies").EnumerateArray())
            {
                var protocol = study.GetProperty("protocolSection");
                if (protocol.TryGetProperty("conditionsModule", out var conditionsModule))
                {
                    foreach (var c in conditionsModule.GetProperty("conditions").EnumerateArray())
                    {
                        terms.Add(c.GetString()!.Trim());
                    }
                    if (conditionsModule.TryGetProperty("keywords", out var keywords))
                    {
                        foreach (var k in keywords.EnumerateArray())
                        {
                            terms.Add(k.GetString()!.Trim());
                        }
                    }
                }
                if (protocol.TryGetProperty("armsInterventionsModule", out var armsModule) &&
                    armsModule.TryGetProperty("interventions", out var interventions))
                {
                    foreach (var i in interventions.EnumerateArray())
                    {
                        if (i.TryGetProperty("name", out var name))
                        {
                            terms.Add(name.GetString()!.Trim());
                        }
                    }
                }
            }
        }

        Assert.IsTrue(terms.Count > 0, "Fixture must contain terms");
        return terms;
    }

    [TestMethod]
    public void MatchBatch_ReturnsResultsIdenticalToSerialMatch()
    {
        var terms = LoadFixtureTerms();
        Assert.IsNotNull(s_matcher);

        var batchResults = s_matcher.MatchBatch(terms, "condition", "NCT-TEST");
        Assert.AreEqual(terms.Count, batchResults.Count, "One result per input term, in order");

        for (int i = 0; i < terms.Count; i++)
        {
            var serial = s_matcher.Match(terms[i], "condition", "NCT-TEST");
            var batched = batchResults[i];

            Assert.AreEqual(serial.Value, batched.Value, $"Value mismatch at {i}: '{terms[i]}'");
            Assert.AreEqual(serial.SideBMatched, batched.SideBMatched, $"SideBMatched mismatch at {i}: '{terms[i]}'");
            Assert.AreEqual(serial.MeshTerm, batched.MeshTerm, $"MeshTerm mismatch at {i}: '{terms[i]}'");
            Assert.AreEqual(serial.MeshCui, batched.MeshCui, $"MeshCui mismatch at {i}: '{terms[i]}'");
            Assert.AreEqual(serial.Category, batched.Category, $"Category mismatch at {i}: '{terms[i]}'");
            Assert.AreEqual(serial.Similarity, batched.Similarity, $"Similarity mismatch at {i}: '{terms[i]}'");
        }
    }

    [TestMethod]
    public void MatchBatch_DuplicateTerms_ReturnSameResultPerOccurrence()
    {
        Assert.IsNotNull(s_matcher);
        var terms = new List<string> { "Influenza", "Influenza", "Neoplasms", "Influenza" };

        var batchResults = s_matcher.MatchBatch(terms, "condition", "NCT-DUP");
        Assert.AreEqual(terms.Count, batchResults.Count);

        for (int i = 0; i < terms.Count; i++)
        {
            var serial = s_matcher.Match(terms[i], "condition", "NCT-DUP");
            Assert.AreEqual(serial.MeshCui, batchResults[i].MeshCui, $"MeshCui mismatch at {i}");
            Assert.AreEqual(serial.Similarity, batchResults[i].Similarity, $"Similarity mismatch at {i}");
        }
    }

    [TestMethod]
    public void MatchBatch_EmptyInput_ReturnsEmptyList()
    {
        Assert.IsNotNull(s_matcher);
        var batchResults = s_matcher.MatchBatch([]);
        Assert.AreEqual(0, batchResults.Count);
    }

    [TestMethod]
    public void MatchBatch_AcrossThresholdBoundary_AgreesWithSerialMatch()
    {
        Assert.IsNotNull(s_matcher);
        // Real captured terms spanning the 0.65 acceptance boundary: exact
        // descriptor names (score 1.0), common disease phrases, and junk terms
        // that fall below the threshold.
        var terms = LoadFixtureTerms();
        Assert.IsTrue(terms.Any(t => s_matcher.Match(t).Accepted), "Fixture should include accepted terms");
        Assert.IsTrue(terms.Any(t => !s_matcher.Match(t).Accepted), "Fixture should include rejected terms");

        var batchResults = s_matcher.MatchBatch(terms, "condition", "NCT-THRESH");
        for (int i = 0; i < terms.Count; i++)
        {
            Assert.AreEqual(
                s_matcher.Match(terms[i]).Accepted,
                batchResults[i].Accepted,
                $"Accepted mismatch at {i}: '{terms[i]}'");
        }
    }
}

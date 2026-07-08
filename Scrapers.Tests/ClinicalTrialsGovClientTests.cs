using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class ClinicalTrialsGovClientTests
{
    private static ClinicalTrialsGov CreateClient(FakeHttpMessageHandler handler, int pageSize = 2)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://clinicaltrials.gov/api/v2/")
        };

        return new ClinicalTrialsGov(httpClient, pageSize);
    }

    [TestMethod]
    public async Task GetTrialsAsync_ReturnsRequestedCountAcrossPages()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadClinicalTrialsGovJson("studies-page1.json"));
        handler.EnqueueJsonResponse(FixtureLoader.LoadClinicalTrialsGovJson("studies-page2.json"));

        ClinicalTrialsGov client = CreateClient(handler);
        IReadOnlyList<StudySummary> results = await client.GetTrialsAsync(count: 3);

        Assert.AreEqual(3, results.Count, "Should return exactly three studies.");
        CollectionAssert.AreEqual(
            new[] { "NCT00660335", "NCT06702735", "NCT03022435" },
            results.Select(r => r.NctId).Where(id => id is not null).Take(3).ToArray());
        Assert.AreEqual(2, handler.Requests.Count, "Pagination should make two HTTP requests.");
    }

    [TestMethod]
    public async Task GetTrialsAsync_ReturnsSingleStudy()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadClinicalTrialsGovJson("studies-page1.json"));

        ClinicalTrialsGov client = CreateClient(handler);
        IReadOnlyList<StudySummary> results = await client.GetTrialsAsync(count: 1);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("NCT00660335", results[0].NctId);
        Assert.AreEqual(1, handler.Requests.Count, "Single study request should only fetch one page.");
    }

    [TestMethod]
    public async Task GetTrialRecordsAsync_ReturnsInvestigators()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadClinicalTrialsGovJson("studies-page1.json"));

        ClinicalTrialsGov client = CreateClient(handler);
        IReadOnlyList<ClinicalTrialRecord> records = await client.GetTrialRecordsAsync(count: 1);

        Assert.AreEqual(1, records.Count);
        Assert.IsTrue(records[0]!.OverallOfficials!.Count > 0, "Expected investigators to be parsed from fixture.");
        Assert.AreEqual("David R Jacoby, MD, PhD", records[0]!.OverallOfficials![0]!.Name);
    }

    [TestMethod]
    public void SchemaGuard_RequiredFieldsRemainPresent()
    {
        var json = FixtureLoader.LoadClinicalTrialsGovJson("studies-page1.json");

        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement studies = GetProperty(root, "studies");
        Assert.AreNotEqual(0, studies.GetArrayLength(), "Expected at least one study in fixture.");

        foreach (JsonElement study in studies.EnumerateArray())
        {
            JsonElement protocolSection = GetProperty(study, "protocolSection");
            JsonElement identification = GetProperty(protocolSection, "identificationModule");
            JsonElement status = GetProperty(protocolSection, "statusModule");

            _ = GetProperty(identification, "nctId").GetString();
            _ = GetProperty(identification, "briefTitle").GetString();
            _ = GetProperty(status, "overallStatus").GetString();
        }
    }

    private static JsonElement GetProperty(JsonElement source, string name)
    {
        if (!source.TryGetProperty(name, out JsonElement value))
        {
            Assert.Fail($"Schema mismatch: missing '{name}'.");
        }

        return value;
    }
}

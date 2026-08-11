using System.Net;
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
    public async Task GetTrialRecordsBatchedAsync_PrefetchesNextPageBeforeBatchCallback()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadClinicalTrialsGovJson("studies-page1.json"));
        handler.EnqueueJsonResponse(FixtureLoader.LoadClinicalTrialsGovJson("studies-page2.json"));

        ClinicalTrialsGov client = CreateClient(handler);
        var requestsDuringFirstCallback = 0;
        await client.GetTrialRecordsBatchedAsync(
            count: 3,
            onBatch: _ =>
            {
                requestsDuringFirstCallback = handler.Requests.Count;
                return Task.CompletedTask;
            });

        Assert.AreEqual(2, requestsDuringFirstCallback,
            "The next API page must be fetched before slow batch persistence begins.");
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
    public async Task GetTrialRecordsAsync_ReturnsReferences()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadClinicalTrialsGovJson("studies-page1.json"));

        ClinicalTrialsGov client = CreateClient(handler);
        IReadOnlyList<ClinicalTrialRecord> records = await client.GetTrialRecordsAsync(count: 1);

        Assert.AreEqual(1, records.Count);
        Assert.IsNotNull(records[0]!.References, "Expected references to be parsed from fixture.");
        Assert.IsTrue(records[0]!.References!.Count > 0, "Expected at least one reference.");
        Assert.AreEqual("24906040", records[0]!.References![0].Pmid);
        Assert.IsNotNull(records[0]!.References![0]!.Citation);
        Assert.IsNotNull(records[0]!.References![0]!.Type);
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

    [TestMethod]
    public async Task CountStudiesAsync_ReturnsTotalCountFromApi()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("""{"totalCount": 4123, "studies": []}""");

        ClinicalTrialsGov client = CreateClient(handler);
        var count = await client.CountStudiesAsync();

        Assert.AreEqual(4123, count);
        Assert.AreEqual(1, handler.Requests.Count, "Count should make a single lightweight request.");
        StringAssert.Contains(handler.Requests[0].Query, "pageSize=1", StringComparison.Ordinal);
        StringAssert.Contains(handler.Requests[0].Query, "countTotal=true", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task CountStudiesAsync_NoTotalCount_FallsBackToPageStudyCount()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("""{"studies": [{}, {}]}""");

        ClinicalTrialsGov client = CreateClient(handler);
        var count = await client.CountStudiesAsync();

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public async Task CountStudiesAsync_WithLastUpdatedPost_AddsFilter()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("""{"totalCount": 5, "studies": []}""");

        var since = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        ClinicalTrialsGov client = CreateClient(handler);
        var count = await client.CountStudiesAsync(lastUpdatedPost: since);

        Assert.AreEqual(5, count);
        StringAssert.Contains(handler.Requests[0].Query, "sort=LastUpdatePostDate:asc", StringComparison.Ordinal);
        StringAssert.Contains(
            handler.Requests[0].Query,
            "filter.advanced=AREA%5BLastUpdatePostDate%5DRANGE%5B2026-07-01%2CMAX%5D",
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task GetTrialRecordsBatchedAsync_WithLastUpdatedPost_AddsFilter()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadClinicalTrialsGovJson("studies-page1.json"));

        var since = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        ClinicalTrialsGov client = CreateClient(handler);
        int fetched = 0;
        await client.GetTrialRecordsBatchedAsync(
            count: 2,
            onBatch: batch => { fetched += batch.Count; return Task.CompletedTask; },
            lastUpdatedPost: since);

        Assert.AreEqual(2, fetched);
        StringAssert.Contains(handler.Requests[0].Query, "sort=LastUpdatePostDate:asc", StringComparison.Ordinal);
        StringAssert.Contains(
            handler.Requests[0].Query,
            "filter.advanced=AREA%5BLastUpdatePostDate%5DRANGE%5B2026-08-01%2CMAX%5D",
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task CountStudiesAsync_WithToBound_AddsWindowedFilter()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("""{"totalCount": 7, "studies": []}""");

        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        ClinicalTrialsGov client = CreateClient(handler);
        var count = await client.CountStudiesAsync(lastUpdatedPost: from, lastUpdatedPostTo: to);

        Assert.AreEqual(7, count);
        StringAssert.Contains(handler.Requests[0].Query, "sort=LastUpdatePostDate:asc", StringComparison.Ordinal);
        StringAssert.Contains(
            handler.Requests[0].Query,
            "filter.advanced=AREA%5BLastUpdatePostDate%5DRANGE%5B2026-07-01%2C2026-08-01%5D",
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task GetTrialRecordsBatchedAsync_WithToBound_AddsWindowedFilter()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadClinicalTrialsGovJson("studies-page1.json"));
        handler.EnqueueJsonResponse(FixtureLoader.LoadClinicalTrialsGovJson("studies-page2.json"));

        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        ClinicalTrialsGov client = CreateClient(handler);
        int fetched = 0;
        await client.GetTrialRecordsBatchedAsync(
            count: 4,
            onBatch: batch => { fetched += batch.Count; return Task.CompletedTask; },
            lastUpdatedPost: from,
            lastUpdatedPostTo: to);

        Assert.AreEqual(4, fetched);
        StringAssert.Contains(handler.Requests[0].Query, "sort=LastUpdatePostDate:asc", StringComparison.Ordinal);
        StringAssert.Contains(
            handler.Requests[0].Query,
            "filter.advanced=AREA%5BLastUpdatePostDate%5DRANGE%5B2026-07-01%2C2026-08-01%5D",
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task GetTrialRecordsAsync_RetriesTransientServerErrorsBeforeSucceeding()
    {
        var handler = new FakeHttpMessageHandler();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            handler.EnqueueJsonResponse("{\"error\":\"temporary gateway failure\"}", HttpStatusCode.InternalServerError);
        }

        handler.EnqueueJsonResponse(FixtureLoader.LoadClinicalTrialsGovJson("studies-page1.json"));

        ClinicalTrialsGov client = CreateClient(handler);
        IReadOnlyList<ClinicalTrialRecord> records = await client.GetTrialRecordsAsync(count: 1);

        Assert.AreEqual(1, records.Count);
        Assert.AreEqual(5, handler.Requests.Count, "The client should continue through transient failures before succeeding.");
    }

    [TestMethod]
    public async Task GetTrialRecordsBatchedAsync_RestartsWhenPaginationIsInvalidatedBeforeFirstBatch()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(
            """{"error":"The data have probably changed while you were paginating. Please, start over from the first page."}""",
            HttpStatusCode.BadRequest);
        handler.EnqueueJsonResponse(FixtureLoader.LoadClinicalTrialsGovJson("studies-page1.json"));

        ClinicalTrialsGov client = CreateClient(handler);
        var fetched = 0;
        await client.GetTrialRecordsBatchedAsync(
            count: 1,
            onBatch: batch =>
            {
                fetched += batch.Count;
                return Task.CompletedTask;
            });

        Assert.AreEqual(1, fetched);
        Assert.AreEqual(2, handler.Requests.Count, "The client should restart from the first page once.");
        Assert.IsFalse(
            handler.Requests[1].Query.Contains("pageToken=", StringComparison.Ordinal),
            "A pagination restart must not reuse the invalid page token.");
    }

    [TestMethod]
    public async Task GetTrialRecordsBatchedAsync_DoesNotReplayCallbackAfterPaginationInvalidation()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(
            """{"studies":[{"protocolSection":{"identificationModule":{"nctId":"NCT-FIRST"}}}],"nextPageToken":"stale"}""");
        handler.EnqueueJsonResponse(
            """{"studies":[{"protocolSection":{"identificationModule":{"nctId":"NCT-SECOND"}}}],"nextPageToken":"stale-again"}""");
        handler.EnqueueJsonResponse(
            """{"error":"The data have probably changed while you were paginating. Please, start over from the first page."}""",
            HttpStatusCode.BadRequest);

        ClinicalTrialsGov client = CreateClient(handler);
        var callbackCount = 0;

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTrialRecordsBatchedAsync(
            count: 3,
            onBatch: _ =>
            {
                callbackCount++;
                return Task.CompletedTask;
            }));

        Assert.AreEqual(1, callbackCount, "A batch already emitted before invalidation must not be replayed in the same call.");
        Assert.AreEqual(3, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetTrialRecordsBatchedAsync_BoundsPaginationRestarts()
    {
        var handler = new FakeHttpMessageHandler();
        for (var attempt = 0; attempt <= 3; attempt++)
        {
            handler.EnqueueJsonResponse(
                """{"error":"The data have probably changed while you were paginating."}""",
                HttpStatusCode.BadRequest);
        }

        ClinicalTrialsGov client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTrialRecordsBatchedAsync(
            count: 1,
            onBatch: _ => Task.CompletedTask));

        Assert.AreEqual(4, handler.Requests.Count, "Pagination restart attempts must be bounded.");
    }

    [TestMethod]
    public void Constructor_AcceptsMaxPageSize_RejectsOutOfRange()
    {
        _ = new ClinicalTrialsGov(pageSize: 500);

        Assert.Throws<ArgumentOutOfRangeException>(() => new ClinicalTrialsGov(pageSize: 501));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClinicalTrialsGov(pageSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClinicalTrialsGov(pageSize: -1));
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

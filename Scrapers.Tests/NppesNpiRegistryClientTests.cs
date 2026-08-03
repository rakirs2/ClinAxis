using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services.Enrichment;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class NppesNpiRegistryClientTests
{
    private static NppesNpiRegistryClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://npiregistry.cms.hhs.gov/")
        };
        return new NppesNpiRegistryClient(httpClient);
    }

    [TestMethod]
    public async Task SearchByNameAsync_ReturnsMultipleResults()
    {
        var handler = new FakeHttpMessageHandler();
        var json = FixtureLoader.LoadNppesNpiJson("search-multiple-results.json");
        handler.EnqueueJsonResponse(json);

        var client = CreateClient(handler);
        var results = await client.SearchByNameAsync("John", "Smith");

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("1234567890", results[0].Number);
        Assert.AreEqual("9876543210", results[1].Number);
    }

    [TestMethod]
    public async Task SearchByNameAsync_DoesNotFilterByAffiliationOrState()
    {
        var handler = new FakeHttpMessageHandler();
        var json = FixtureLoader.LoadNppesNpiJson("search-multiple-results.json");
        handler.EnqueueJsonResponse(json);

        var client = CreateClient(handler);
        var results = await client.SearchByNameAsync("John", "Smith");

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual(1, handler.Requests.Count);
        var requestUrl = handler.Requests[0].ToString();
        Assert.IsFalse(requestUrl.Contains("state=", StringComparison.Ordinal),
            "Query must not narrow by state — the scorer evaluates the full candidate set");
    }

    [TestMethod]
    public async Task SearchByNameAsync_EmptyResponse_ReturnsEmptyList()
    {
        var handler = new FakeHttpMessageHandler();
        var json = FixtureLoader.LoadNppesNpiJson("search-empty-results.json");
        handler.EnqueueJsonResponse(json);

        var client = CreateClient(handler);
        var results = await client.SearchByNameAsync("Nonexistent", "Nobody");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchByNameAsync_NonSuccessStatusCode_ReturnsEmptyList()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("", HttpStatusCode.InternalServerError);

        var client = CreateClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SearchByNameAsync("John", "Smith"));
    }

    [TestMethod]
    public async Task SearchByOrganizationAsync_ReturnsResults()
    {
        var handler = new FakeHttpMessageHandler();
        var json = FixtureLoader.LoadNppesNpiJson("search-single-result.json");
        handler.EnqueueJsonResponse(json);

        var client = CreateClient(handler);
        var results = await client.SearchByOrganizationAsync("Mayo Clinic");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("1234567890", results[0].Number);
    }

    [TestMethod]
    public async Task SearchByOrganizationAsync_EmptyResponse_ReturnsEmptyList()
    {
        var handler = new FakeHttpMessageHandler();
        var json = FixtureLoader.LoadNppesNpiJson("search-empty-results.json");
        handler.EnqueueJsonResponse(json);

        var client = CreateClient(handler);
        var results = await client.SearchByOrganizationAsync("Nonexistent");

        Assert.AreEqual(0, results.Count);
    }
}

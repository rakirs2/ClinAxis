using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services.Cms;
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
    public async Task LookupNpiAsync_ReturnsNpiForKnownName()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadNppesNpiRegistryJson("search-response.json"));

        var client = CreateClient(handler);
        var npi = await client.LookupNpiAsync("David", "Jacoby");

        Assert.AreEqual("1234567890", npi);
    }

    [TestMethod]
    public async Task LookupNpiAsync_ReturnsNullWhenNoResults()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("""{"result_count":0,"results":[]}""");

        var client = CreateClient(handler);
        var npi = await client.LookupNpiAsync("Xyzzy", "Nonexistent");

        Assert.IsNull(npi);
    }

    [TestMethod]
    public async Task SearchByNameAsync_ReturnsAllResults()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadNppesNpiRegistryJson("search-response.json"));

        var client = CreateClient(handler);
        var results = await client.SearchByNameAsync("David", "Jacoby");

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual(1234567890, results[0].Number);
        Assert.AreEqual(9876543210, results[1].Number);
    }

    [TestMethod]
    public async Task SearchByNameAsync_ParsesBasicFields()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadNppesNpiRegistryJson("search-response.json"));

        var client = CreateClient(handler);
        var results = await client.SearchByNameAsync("David", "Jacoby");

        Assert.IsNotNull(results[0].Basic);
        Assert.AreEqual("DAVID", results[0].Basic!.FirstName);
        Assert.AreEqual("JACOBY", results[0].Basic!.LastName);
        Assert.AreEqual("MD", results[0].Basic!.Credential);
        Assert.AreEqual("M", results[0].Basic!.Gender);
    }

    [TestMethod]
    public async Task SearchByNameAsync_ParsesAddresses()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadNppesNpiRegistryJson("search-response.json"));

        var client = CreateClient(handler);
        var results = await client.SearchByNameAsync("David", "Jacoby");

        Assert.IsNotNull(results[0].Addresses);
        Assert.AreEqual(1, results[0].Addresses!.Count);
        Assert.AreEqual("PORTLAND", results[0].Addresses![0]!.City);
        Assert.AreEqual("OR", results[0].Addresses![0]!.State);
    }

    [TestMethod]
    public async Task SearchByNameAsync_ParsesTaxonomies()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadNppesNpiRegistryJson("search-response.json"));

        var client = CreateClient(handler);
        var results = await client.SearchByNameAsync("David", "Jacoby");

        Assert.IsNotNull(results[0].Taxonomies);
        Assert.AreEqual(1, results[0].Taxonomies!.Count);
        Assert.AreEqual("Cardiovascular Disease", results[0].Taxonomies![0]!.Desc);
        Assert.IsTrue(results[0].Taxonomies![0]!.Primary);
    }

    [TestMethod]
    public async Task SearchByNameAsync_HandlesHttpError()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("""{"error":"Not found"}""", HttpStatusCode.NotFound);

        var client = CreateClient(handler);

        await Assert.ThrowsExceptionAsync<HttpRequestException>(() =>
            client.SearchByNameAsync("Unknown", "Person"));
    }
}

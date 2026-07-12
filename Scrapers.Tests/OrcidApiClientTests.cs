using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services.Cms;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class OrcidApiClientTests
{
    private static OrcidApiClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://pub.orcid.org/v3.0/")
        };
        return new OrcidApiClient(httpClient);
    }

    [TestMethod]
    public async Task LookupOrcidAsync_ReturnsOrcidForKnownName()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadOrcidApiJson("search-response.json"));

        var client = CreateClient(handler);
        var orcid = await client.LookupOrcidAsync("David", "Jacoby");

        Assert.AreEqual("0000-0001-2345-6789", orcid);
    }

    [TestMethod]
    public async Task LookupOrcidAsync_ReturnsNullWhenNoResults()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("""{"num-found":0,"result":[]}""");

        var client = CreateClient(handler);
        var orcid = await client.LookupOrcidAsync("Xyzzy", "Nonexistent");

        Assert.IsNull(orcid);
    }

    [TestMethod]
    public async Task SearchByNameAsync_ReturnsAllResults()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadOrcidApiJson("search-response.json"));

        var client = CreateClient(handler);
        var results = await client.SearchByNameAsync("David", "Jacoby");

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("0000-0001-2345-6789", results[0].Path);
        Assert.AreEqual("0000-0002-8765-4321", results[1].Path);
    }

    [TestMethod]
    public async Task SearchByNameAsync_SetsAcceptHeader()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadOrcidApiJson("search-response.json"));

        var client = CreateClient(handler);
        await client.SearchByNameAsync("David", "Jacoby");

        Assert.IsTrue(handler.Requests.Count > 0);
    }

    [TestMethod]
    public async Task SearchByNameAsync_HandlesHttpError()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("""{"error":"Forbidden"}""", HttpStatusCode.Forbidden);

        var client = CreateClient(handler);

        await Assert.ThrowsExceptionAsync<HttpRequestException>(() =>
            client.SearchByNameAsync("Unknown", "Person"));
    }
}

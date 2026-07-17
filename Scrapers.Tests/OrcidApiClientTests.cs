using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services.Enrichment;
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
    public async Task SearchByNameAsync_ReturnsMultipleOrcids()
    {
        var handler = new FakeHttpMessageHandler();
        var json = FixtureLoader.LoadOrcidJson("search-response.json");
        handler.EnqueueJsonResponse(json);

        var client = CreateClient(handler);
        var results = await client.SearchByNameAsync("John", "Smith");

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("0000-0001-2345-6789", results[0].Orcid);
        Assert.AreEqual("0000-0002-9876-5432", results[1].Orcid);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task SearchByNameAsync_EmptyResponse_ReturnsEmptyList()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("{\"num-found\":0,\"result\":[]}");

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
        var results = await client.SearchByNameAsync("John", "Smith");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task GetRecordAsync_ReturnsPersonWithExternalIdentifiers()
    {
        var handler = new FakeHttpMessageHandler();
        var json = FixtureLoader.LoadOrcidJson("record-response.json");
        handler.EnqueueJsonResponse(json);

        var client = CreateClient(handler);
        var record = await client.GetRecordAsync("0000-0001-2345-6789");

        Assert.IsNotNull(record);
        Assert.IsNotNull(record.Person);
        Assert.IsNotNull(record.Person.Name);
        Assert.AreEqual("John", record.Person.Name.GivenNames?.Value);
        Assert.AreEqual("Smith", record.Person.Name.FamilyNames?.Value);

        Assert.IsNotNull(record.Person.ExternalIdentifiers);
        Assert.IsNotNull(record.Person.ExternalIdentifiers.ExternalIdentifier);
        Assert.AreEqual(2, record.Person.ExternalIdentifiers.ExternalIdentifier.Count);
        Assert.AreEqual("NPI", record.Person.ExternalIdentifiers.ExternalIdentifier[0].ExternalIdType);
        Assert.AreEqual("1234567890", record.Person.ExternalIdentifiers.ExternalIdentifier[0].ExternalIdValue);
    }

    [TestMethod]
    public async Task GetRecordAsync_NonSuccessStatusCode_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("", HttpStatusCode.NotFound);

        var client = CreateClient(handler);
        var record = await client.GetRecordAsync("0000-0000-0000-0000");

        Assert.IsNull(record);
    }
}

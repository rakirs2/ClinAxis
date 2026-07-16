using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services.Enrichment;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class SemanticScholarClientTests
{
    private static SemanticScholarClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.semanticscholar.org/")
        };
        return new SemanticScholarClient(httpClient);
    }

    [TestMethod]
    public async Task SearchByNameAsync_ValidName_ReturnsAuthorWithMetrics()
    {
        var handler = new FakeHttpMessageHandler();
        var json = FixtureLoader.LoadSemanticScholarJson("search-response.json");
        handler.EnqueueJsonResponse(json);

        var client = CreateClient(handler);
        var result = await client.SearchByNameAsync("Albert Johnson");

        Assert.IsNotNull(result);
        Assert.AreEqual("2259775814", result.AuthorId);
        Assert.AreEqual("Albert Johnson", result.Name);
        Assert.AreEqual(66, result.PaperCount);
        Assert.AreEqual(1071, result.CitationCount);
        Assert.AreEqual(15, result.HIndex);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task SearchByNameAsync_WithAffiliation_IncludesAffiliationInQuery()
    {
        var handler = new FakeHttpMessageHandler();
        var json = FixtureLoader.LoadSemanticScholarJson("search-response.json");
        handler.EnqueueJsonResponse(json);

        var client = CreateClient(handler);
        await client.SearchByNameAsync("Albert Johnson", "Stanford University");

        Assert.AreEqual(1, handler.Requests.Count);
        var requestUrl = handler.Requests[0].ToString();
        // The URL will have form-encoded values, so Albert Johnson becomes Albert%20Johnson and space between words
        Assert.IsTrue(requestUrl.Contains("Albert", StringComparison.Ordinal));
        Assert.IsTrue(requestUrl.Contains("Johnson", StringComparison.Ordinal));
        Assert.IsTrue(requestUrl.Contains("Stanford", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SearchByNameAsync_EmptyName_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        var client = CreateClient(handler);

        var result = await client.SearchByNameAsync("");

        Assert.IsNull(result);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task SearchByNameAsync_NullName_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        var client = CreateClient(handler);

        var result = await client.SearchByNameAsync(null!);

        Assert.IsNull(result);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task SearchByNameAsync_NoResults_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        var emptyResponse = """{"total": 0, "offset": 0, "data": []}""";
        handler.EnqueueJsonResponse(emptyResponse);

        var client = CreateClient(handler);
        var result = await client.SearchByNameAsync("Unknown Researcher Name XYZ");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SearchByNameAsync_HttpError_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("{}", System.Net.HttpStatusCode.InternalServerError);

        var client = CreateClient(handler);
        var result = await client.SearchByNameAsync("John Smith");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SearchByNameAsync_MultipleResults_PicksHighestCitationCount()
    {
        var handler = new FakeHttpMessageHandler();
        var json = FixtureLoader.LoadSemanticScholarJson("search-response.json");
        handler.EnqueueJsonResponse(json);

        var client = CreateClient(handler);
        var result = await client.SearchByNameAsync("Albert Johnson");

        // Should pick the first author (2259775814) with highest citation count (1071)
        Assert.IsNotNull(result);
        Assert.AreEqual("2259775814", result.AuthorId);
        Assert.AreEqual(1071, result.CitationCount);
    }

    [TestMethod]
    public async Task SearchByNameAsync_NoHIndex_ReturnsAuthorWithNullHIndex()
    {
        var handler = new FakeHttpMessageHandler();
        var json = """
        {
          "total": 1,
          "offset": 0,
          "data": [
            {
              "authorId": "9999999999",
              "name": "New Researcher",
              "paperCount": 2,
              "citationCount": 5,
              "hIndex": null
            }
          ]
        }
        """;
        handler.EnqueueJsonResponse(json);

        var client = CreateClient(handler);
        var result = await client.SearchByNameAsync("New Researcher");

        Assert.IsNotNull(result);
        Assert.AreEqual("9999999999", result.AuthorId);
        Assert.AreEqual(5, result.CitationCount);
        Assert.IsNull(result.HIndex);
    }

    [TestMethod]
    public async Task SearchByNameAsync_WhitespaceNameOnly_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        var client = CreateClient(handler);

        var result = await client.SearchByNameAsync("   ");

        Assert.IsNull(result);
        Assert.AreEqual(0, handler.Requests.Count);
    }
}

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.IntegrationTests.Helpers;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class FrontendProxySearchLiveTests
{
    private StudyRepository _repo = null!;
    private static readonly HttpClient Client = new();

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _repo = new StudyRepository(PostgresTestHelper.ConnectionString);
        await _repo.EnsureSchemaAsync();
    }

    [TestMethod]
    public async Task ProxySearch_ReturnsMatchingResults()
    {
        var record = new ClinicalTrialRecord
        {
            NctId = "NCT09999002",
            BriefTitle = "Pancreatic Cancer Vaccine Study Double Blind",
            OverallStatus = "ACTIVE",
            OverallOfficials = new List<Investigator>
            {
                new() { Name = "Dr. Jones", Role = "PRINCIPAL_INVESTIGATOR" }
            }
        };
        await _repo.UpdateStudiesWithClinicalTrialsAsync(new[] { record });

        using var response = await Client.GetAsync(
            new Uri("http://localhost:5001/api-proxy/studies?search=pancreatic+cancer&page=1&pageSize=10"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        JsonElement data = doc.RootElement.GetProperty("data");
        Assert.IsTrue(data.GetArrayLength() > 0, "Proxy search should return at least one result");

        var found = data.EnumerateArray().Any(s =>
            s.GetProperty("nctId").GetString() == "NCT09999002");
        Assert.IsTrue(found, "Inserted study should appear in proxy search results");
    }

    [TestMethod]
    public async Task ProxySearch_ReturnsSameShapeAsDataApi()
    {
        using var proxyResponse = await Client.GetAsync(
            new Uri("http://localhost:5001/api-proxy/studies?search=cancer&page=1&pageSize=2"));
        proxyResponse.EnsureSuccessStatusCode();
        var proxyBody = await proxyResponse.Content.ReadAsStringAsync();
        using JsonDocument proxyDoc = JsonDocument.Parse(proxyBody);

        using HttpResponseMessage apiResponse = await Client.GetAsync(
            new Uri("http://localhost:5003/api/studies?search=cancer&page=1&pageSize=2"));
        apiResponse.EnsureSuccessStatusCode();
        var apiBody = await apiResponse.Content.ReadAsStringAsync();
        using JsonDocument apiDoc = JsonDocument.Parse(apiBody);

        Assert.AreEqual(apiDoc.RootElement.GetProperty("total").GetInt32(),
            proxyDoc.RootElement.GetProperty("total").GetInt32(),
            "Proxy and API should return same total");

        List<string?> proxyIds = proxyDoc.RootElement.GetProperty("data").EnumerateArray()
            .Select(s => s.GetProperty("nctId").GetString()).ToList();
        List<string?> apiIds = apiDoc.RootElement.GetProperty("data").EnumerateArray()
            .Select(s => s.GetProperty("nctId").GetString()).ToList();

        CollectionAssert.AreEquivalent(apiIds, proxyIds, "Proxy and API should return same studies");
    }
}

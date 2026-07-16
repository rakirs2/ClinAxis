using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services.Enrichment;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class CmsMedicareClientTests
{
    private static CmsMedicareClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://data.cms.gov/data-api/v1/dataset/")
        };

        return new CmsMedicareClient(httpClient);
    }

    [TestMethod]
    public async Task GetByNpiAsync_ValidNpi_ReturnsRecord()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadCmsMedicareJson("sample-response.json"));

        CmsMedicareClient client = CreateClient(handler);
        CmsMedicareRecord? record = await client.GetByNpiAsync("1234567890");

        Assert.IsNotNull(record);
        Assert.AreEqual("1234567890", record.Npi);
        Assert.AreEqual("JOHNSON", record.LastName);
        Assert.AreEqual("ROBERT", record.FirstName);
        Assert.AreEqual("Internal Medicine", record.ProviderType);
        Assert.AreEqual(150, record.TotalBeneficiaries);
        Assert.AreEqual(450, record.TotalServices);
        Assert.AreEqual(87500.00m, record.TotalMedicareAllowedAmount);
        Assert.AreEqual(65000.00m, record.TotalMedicarePaymentAmount);
        Assert.AreEqual(72000.00m, record.TotalMedicareStandardizedAmount);
        Assert.AreEqual(42.5m, record.BeneCcPhHypertensionPct);
        Assert.AreEqual(22.1m, record.BeneCcPhDiabetesPct);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetByNpiAsync_InvalidNpi_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        CmsMedicareClient client = CreateClient(handler);

        CmsMedicareRecord? result = await client.GetByNpiAsync("");

        Assert.IsNull(result);
        Assert.AreEqual(0, handler.Requests.Count, "No HTTP request should be made for empty NPI");
    }

    [TestMethod]
    public async Task GetByNpiAsync_NonSuccessStatusCode_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("[]", System.Net.HttpStatusCode.InternalServerError);

        CmsMedicareClient client = CreateClient(handler);
        CmsMedicareRecord? result = await client.GetByNpiAsync("1234567890");

        Assert.IsNull(result);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetAllByNpiAsync_ReturnsMultipleRecords()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadCmsMedicareJson("sample-response.json"));

        CmsMedicareClient client = CreateClient(handler);
        IReadOnlyList<CmsMedicareRecord> records = await client.GetAllByNpiAsync("1234567890");

        Assert.AreEqual(1, records.Count);
        Assert.AreEqual("1234567890", records[0].Npi);
    }

    [TestMethod]
    public async Task GetAllByNpiAsync_InvalidNpi_ReturnsEmpty()
    {
        var handler = new FakeHttpMessageHandler();
        CmsMedicareClient client = CreateClient(handler);

        IReadOnlyList<CmsMedicareRecord> results = await client.GetAllByNpiAsync("");

        Assert.AreEqual(0, results.Count);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetByNpiAsync_WhitespaceNpi_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        CmsMedicareClient client = CreateClient(handler);

        CmsMedicareRecord? result = await client.GetByNpiAsync("   ");

        Assert.IsNull(result);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task Constructor_AcceptsCustomDatasetUuid()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadCmsMedicareJson("sample-response.json"));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://data.cms.gov/data-api/v1/dataset/")
        };

        var client = new CmsMedicareClient(httpClient, datasetUuid: "custom-uuid");
        CmsMedicareRecord? record = await client.GetByNpiAsync("1234567890");

        Assert.IsNotNull(record);
        StringAssert.Contains(handler.Requests[0].ToString()!, "custom-uuid",
            StringComparison.OrdinalIgnoreCase);
    }
}

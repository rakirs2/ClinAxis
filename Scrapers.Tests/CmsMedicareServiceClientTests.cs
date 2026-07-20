using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services.Enrichment;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class CmsMedicareServiceClientTests
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
    public async Task GetServicesByNpiAsync_ValidNpi_ReturnsRecords()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadCmsMedicareServiceJson("services-response.json"));

        CmsMedicareClient client = CreateClient(handler);
        IReadOnlyList<CmsMedicareServiceRecord> records = await client.GetServicesByNpiAsync("1234567890");

        Assert.IsNotNull(records);
        Assert.AreEqual(3, records.Count);
        Assert.AreEqual("99214", records[0].HcpcsCode);
        Assert.AreEqual("Office/outpatient visit est low to high 30-39 min", records[0].HcpcsDescription);
        Assert.AreEqual("O", records[0].PlaceOfService);
        Assert.AreEqual(85, records[0].BeneficiaryCount);
        Assert.AreEqual(180, records[0].ServiceCount);
        Assert.AreEqual(200.00m, records[0].SubmittedChargeAmount);
        Assert.AreEqual(140.00m, records[0].MedicareAllowedAmount);
        Assert.AreEqual(105.56m, records[0].MedicarePaymentAmount);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetServicesByNpiAsync_MultipleRecords_HasAllHcpcsCodes()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadCmsMedicareServiceJson("services-response.json"));

        CmsMedicareClient client = CreateClient(handler);
        IReadOnlyList<CmsMedicareServiceRecord> records = await client.GetServicesByNpiAsync("1234567890");

        Assert.AreEqual(3, records.Count);
        Assert.AreEqual("99214", records[0].HcpcsCode);
        Assert.AreEqual("99396", records[1].HcpcsCode);
        Assert.AreEqual("93000", records[2].HcpcsCode);
    }

    [TestMethod]
    public async Task GetServicesByNpiAsync_InvalidNpi_ReturnsEmpty()
    {
        var handler = new FakeHttpMessageHandler();
        CmsMedicareClient client = CreateClient(handler);

        IReadOnlyList<CmsMedicareServiceRecord> result = await client.GetServicesByNpiAsync("");

        Assert.AreEqual(0, result.Count);
        Assert.AreEqual(0, handler.Requests.Count, "No HTTP request should be made for empty NPI");
    }

    [TestMethod]
    public async Task GetServicesByNpiAsync_WhitespaceNpi_ReturnsEmpty()
    {
        var handler = new FakeHttpMessageHandler();
        CmsMedicareClient client = CreateClient(handler);

        IReadOnlyList<CmsMedicareServiceRecord> result = await client.GetServicesByNpiAsync("   ");

        Assert.AreEqual(0, result.Count);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetServicesByNpiAsync_NonSuccessStatusCode_ReturnsEmpty()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("[]", System.Net.HttpStatusCode.InternalServerError);

        CmsMedicareClient client = CreateClient(handler);
        IReadOnlyList<CmsMedicareServiceRecord> result = await client.GetServicesByNpiAsync("1234567890");

        Assert.AreEqual(0, result.Count);
        Assert.AreEqual(1, handler.Requests.Count);
    }
}

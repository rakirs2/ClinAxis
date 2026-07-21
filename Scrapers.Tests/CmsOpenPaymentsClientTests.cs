using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services.Enrichment;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class CmsOpenPaymentsClientTests
{
    private static CmsOpenPaymentsClient CreateClient(FakeHttpMessageHandler handler)
    {
        return new CmsOpenPaymentsClient(new HttpClient(handler));
    }

    [TestMethod]
    public async Task GetResearchPaymentsByNpiAsync_ValidNpi_ReturnsRecords()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadCmsOpenPaymentsJson("payments-response.json"));

        CmsOpenPaymentsClient client = CreateClient(handler);
        List<OpenPaymentRecord> records = await client.GetResearchPaymentsByNpiAsync("1234567890", "2024");

        Assert.AreEqual(3, records.Count);
        Assert.AreEqual("1234567890", records[0].RecipientNpi);
        Assert.AreEqual("SMITH", records[0].RecipientLastName);
        Assert.AreEqual("JOHN", records[0].RecipientFirstName);
        Assert.AreEqual(15000.00m, records[0].PaymentAmount);
        Assert.AreEqual("PharmaCorp Inc.", records[0].PayorName);
        Assert.AreEqual("NCT01234567", records[0].ClinicalTrialsId);
        Assert.AreEqual("Oncology", records[0].ProductCategory);
        Assert.AreEqual("1000001", records[0].RecordId);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetGeneralPaymentsByNpiAsync_ValidNpi_ReturnsRecords()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadCmsOpenPaymentsJson("payments-response.json"));

        CmsOpenPaymentsClient client = CreateClient(handler);
        List<OpenPaymentRecord> records = await client.GetGeneralPaymentsByNpiAsync("1234567890", "2024");

        Assert.AreEqual(3, records.Count);
        Assert.AreEqual(2500.00m, records[1].PaymentAmount);
        Assert.AreEqual("BioMed Devices LLC", records[1].PayorName);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetOwnershipByNpiAsync_ValidNpi_ReturnsRecords()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse(FixtureLoader.LoadCmsOpenPaymentsJson("payments-response.json"));

        CmsOpenPaymentsClient client = CreateClient(handler);
        List<OpenPaymentRecord> records = await client.GetOwnershipByNpiAsync("1234567890", "2024");

        Assert.AreEqual(3, records.Count);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetAllPaymentsByNpiAsync_ReturnsCombinedRecords()
    {
        var handler = new FakeHttpMessageHandler();
        var fixture = FixtureLoader.LoadCmsOpenPaymentsJson("payments-response.json");
        for (int i = 0; i < 21; i++)
            handler.EnqueueJsonResponse(fixture);

        CmsOpenPaymentsClient client = CreateClient(handler);
        List<OpenPaymentRecord> records = await client.GetAllPaymentsByNpiAsync("1234567890");

        Assert.AreEqual(63, records.Count);
        Assert.AreEqual(21, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetResearchPaymentsByNpiAsync_EmptyNpi_ReturnsEmpty()
    {
        var handler = new FakeHttpMessageHandler();
        CmsOpenPaymentsClient client = CreateClient(handler);

        List<OpenPaymentRecord> records = await client.GetResearchPaymentsByNpiAsync("");

        Assert.AreEqual(0, records.Count);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetResearchPaymentsByNpiAsync_WhitespaceNpi_ReturnsEmpty()
    {
        var handler = new FakeHttpMessageHandler();
        CmsOpenPaymentsClient client = CreateClient(handler);

        List<OpenPaymentRecord> records = await client.GetResearchPaymentsByNpiAsync("   ");

        Assert.AreEqual(0, records.Count);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetResearchPaymentsByNpiAsync_NonSuccessStatusCode_ReturnsEmpty()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJsonResponse("[]", System.Net.HttpStatusCode.InternalServerError);

        CmsOpenPaymentsClient client = CreateClient(handler);
        List<OpenPaymentRecord> records = await client.GetResearchPaymentsByNpiAsync("1234567890", "2024");

        Assert.AreEqual(0, records.Count);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetAllPaymentsByNpiAsync_EmptyNpi_ReturnsEmpty()
    {
        var handler = new FakeHttpMessageHandler();
        CmsOpenPaymentsClient client = CreateClient(handler);

        List<OpenPaymentRecord> records = await client.GetAllPaymentsByNpiAsync("");

        Assert.AreEqual(0, records.Count);
        Assert.AreEqual(0, handler.Requests.Count);
    }
}

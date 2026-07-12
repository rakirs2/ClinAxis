using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services.Cms;
using Scrapers.Tests.Helpers;

namespace Scrapers.Tests;

[TestClass]
public sealed class CmsMedicareScraperTests
{
    [TestMethod]
    public async Task ParseProviderCsvAsync_ParsesAllRows()
    {
        using var stream = FixtureLoader.LoadCmsMedicareCsv("sample-providers.csv");
        var records = await CmsMedicareScraper.ParseProviderCsvAsync(stream);

        Assert.AreEqual(3, records.Count);
    }

    [TestMethod]
    public async Task ParseProviderCsvAsync_ParsesNpi()
    {
        using var stream = FixtureLoader.LoadCmsMedicareCsv("sample-providers.csv");
        var records = await CmsMedicareScraper.ParseProviderCsvAsync(stream);

        Assert.AreEqual("1234567890", records[0].Npi);
        Assert.AreEqual("9876543210", records[1].Npi);
        Assert.AreEqual("5556667777", records[2].Npi);
    }

    [TestMethod]
    public async Task ParseProviderCsvAsync_ParsesProviderName()
    {
        using var stream = FixtureLoader.LoadCmsMedicareCsv("sample-providers.csv");
        var records = await CmsMedicareScraper.ParseProviderCsvAsync(stream);

        Assert.AreEqual("DAVID JACOBY", records[0].ProviderName);
        Assert.AreEqual("JOHN SMITH", records[1].ProviderName);
        Assert.AreEqual("JANE WILLIAMS", records[2].ProviderName);
    }

    [TestMethod]
    public async Task ParseProviderCsvAsync_ParsesSpecialty()
    {
        using var stream = FixtureLoader.LoadCmsMedicareCsv("sample-providers.csv");
        var records = await CmsMedicareScraper.ParseProviderCsvAsync(stream);

        Assert.AreEqual("Cardiovascular Disease", records[0].PrimarySpecialty);
        Assert.AreEqual("Hospitalist", records[1].PrimarySpecialty);
        Assert.AreEqual("Neurology", records[2].PrimarySpecialty);
    }

    [TestMethod]
    public async Task ParseProviderCsvAsync_ParsesAddress()
    {
        using var stream = FixtureLoader.LoadCmsMedicareCsv("sample-providers.csv");
        var records = await CmsMedicareScraper.ParseProviderCsvAsync(stream);

        Assert.AreEqual("PORTLAND", records[0].PracticeAddressCity);
        Assert.AreEqual("OR", records[0].PracticeAddressState);
        Assert.AreEqual("97239", records[0].PracticeAddressZip);
    }

    [TestMethod]
    public async Task ParseProviderCsvAsync_ParsesMedicareData()
    {
        using var stream = FixtureLoader.LoadCmsMedicareCsv("sample-providers.csv");
        var records = await CmsMedicareScraper.ParseProviderCsvAsync(stream);

        Assert.AreEqual("Yes", records[0].MedicareParticipation);
        Assert.AreEqual(1500, records[0].TotalMedicareServices);
        Assert.AreEqual(125000.50m, records[0].TotalMedicarePayments);
        Assert.AreEqual(500, records[0].TotalMedicareBeneficiaries);
    }

    [TestMethod]
    public async Task ParseProviderCsvAsync_ParsesEducation()
    {
        using var stream = FixtureLoader.LoadCmsMedicareCsv("sample-providers.csv");
        var records = await CmsMedicareScraper.ParseProviderCsvAsync(stream);

        Assert.AreEqual("OHSU School of Medicine", records[0].MedicalSchoolName);
        Assert.AreEqual(1995, records[0].GraduationYear);
    }

    [TestMethod]
    public async Task ParseProviderCsvAsync_SkipsRowWithoutNpi()
    {
        var csv = "NPI,Name\n,\n123,Test";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var records = await CmsMedicareScraper.ParseProviderCsvAsync(stream);

        Assert.AreEqual(1, records.Count);
    }
}

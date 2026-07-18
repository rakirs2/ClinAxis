using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class NameClassificationIntegrationTests : DbTestBase
{
    private StudyRepository _repo = null!;

    [TestInitialize]
    public void TestInit()
    {
        _repo = new StudyRepository(ConnectionString);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task IngestRecords_LogsNameClassification_ForEachOfficial()
    {
        var records = new[]
        {
            CreateRecord("NCT10000001", "Test Study 1", "RECRUITING", new[]
            {
                new Investigator { Name = "John Smith", Role = "PRINCIPAL_INVESTIGATOR" },
                new Investigator { Name = "Study Director", Role = "STUDY_DIRECTOR" },
            }),
            CreateRecord("NCT10000002", "Test Study 2", "COMPLETED", new[]
            {
                new Investigator { Name = "Jane Doe, MD", Role = "SUB_INVESTIGATOR" },
            }),
        };

        var ingested = await _repo.UpdateStudiesWithClinicalTrialsAsync(records);
        Assert.AreEqual(2, ingested);

        using var ctx = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(ConnectionString).Options);

        var logs = ctx.NameClassificationLogs.ToList();
        Assert.AreEqual(3, logs.Count, "Should log one row per official");

        var johnLog = logs.First(l => l.Name == "John Smith");
        Assert.AreEqual("NCT10000001", johnLog.StudyNctId);
        Assert.AreEqual("ACCEPT", johnLog.NameFilterDecision);
        Assert.IsTrue(johnLog.MlDecision is "ACCEPT" or "REJECT");
        Assert.IsTrue(johnLog.MlConfidence > 0);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task IngestRecords_LogsCorrectDecisions()
    {
        var records = new[]
        {
            CreateRecord("NCT20000001", "Test", "RECRUITING", new[]
            {
                new Investigator { Name = "+1-555-555-0199 Support", Role = null },
                new Investigator { Name = "Alice Researcher", Role = "PRINCIPAL_INVESTIGATOR" },
            }),
        };

        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        using var ctx = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(ConnectionString).Options);

        var logs = ctx.NameClassificationLogs.OrderBy(l => l.Name).ToList();
        Assert.AreEqual(2, logs.Count);

        var phoneLog = logs.First(l => l.Name == "+1-555-555-0199 Support");
        Assert.AreEqual("REJECT", phoneLog.NameFilterDecision);
        Assert.AreEqual("PhonePrefix", phoneLog.NameFilterReason);

        var aliceLog = logs.First(l => l.Name == "Alice Researcher");
        Assert.AreEqual("ACCEPT", aliceLog.NameFilterDecision);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task LogEntry_Review_SetsClassification()
    {
        var records = new[]
        {
            CreateRecord("NCT30000001", "Test", "RECRUITING", new[]
            {
                new Investigator { Name = "Someone", Role = null },
            }),
        };

        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        using var ctx = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(ConnectionString).Options);

        var entry = ctx.NameClassificationLogs.First();
        Assert.IsNull(entry.UserClassification);
        Assert.IsNull(entry.ReviewedAt);

        entry.UserClassification = "HUMAN";
        entry.ReviewedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var reloaded = await ctx.NameClassificationLogs.FindAsync(entry.Id);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual("HUMAN", reloaded.UserClassification);
        Assert.IsNotNull(reloaded.ReviewedAt);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CsvExport_ContainsHeaderAndRows()
    {
        var records = new[]
        {
            CreateRecord("NCT40000001", "Test", "RECRUITING", new[]
            {
                new Investigator { Name = "Test Person", Role = null },
            }),
        };

        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        using var ctx = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(ConnectionString).Options);

        var logs = ctx.NameClassificationLogs.ToList();
        Assert.IsTrue(logs.Count > 0, "Should have at least one log entry");

        var log = logs.First();
        Assert.AreEqual("Test Person", log.Name);
        Assert.AreEqual("NCT40000001", log.StudyNctId);
    }

    private static ClinicalTrialRecord CreateRecord(string nctId, string title, string status,
        Investigator[]? investigators)
    {
        return new ClinicalTrialRecord
        {
            NctId = nctId,
            BriefTitle = title,
            OverallStatus = status,
            OverallOfficials = investigators?.ToList()
        };
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services;
using Scrapers.Testing;
using Microsoft.EntityFrameworkCore;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class AggregationIntegrationTests : DbTestBase
{
    private StudyRepository _repo = null!;

    [TestInitialize]
    public void TestInit()
    {
        _repo = new StudyRepository(ConnectionString);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task AggregateAsync_ComputesPiCounts()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00000001", "Study Alpha", "RECRUITING",
                new Investigator { Name = "Alice Smith", Affiliation = "Acme", Role = "PI" },
                new Investigator { Name = "Bob Jones", Affiliation = "Acme", Role = "SUB_I" }),
            CreateRecord("NCT00000002", "Study Beta", "COMPLETED",
                new Investigator { Name = "Alice Smith", Affiliation = "Acme", Role = "PI" }),
            CreateRecord("NCT00000003", "Study Gamma", "ACTIVE",
                new Investigator { Name = "Carol White", Affiliation = "Beta Corp", Role = "PI" })
        ];

        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        var service = new AggregationService(_repo);
        await service.AggregateAsync();

        var piRows = await Context.PiAggregations
            .OrderByDescending(p => p.StudyCount)
            .ToListAsync();

        Assert.AreEqual(3, piRows.Count, "Should have one row per unique PI name.");

        var alice = piRows[0];
        Assert.AreEqual("Alice Smith", alice.InvestigatorName);
        Assert.AreEqual(2, alice.StudyCount);
        Assert.IsTrue(alice.StudyNctIds.Contains("NCT00000001", StringComparison.Ordinal));
        Assert.IsTrue(alice.StudyNctIds.Contains("NCT00000002", StringComparison.Ordinal));

        var bob = piRows[1];
        Assert.AreEqual("Bob Jones", bob.InvestigatorName);
        Assert.AreEqual(1, bob.StudyCount);

        var carol = piRows[2];
        Assert.AreEqual("Carol White", carol.InvestigatorName);
        Assert.AreEqual(1, carol.StudyCount);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task AggregateAsync_ComputesCategoryCounts()
    {
        ClinicalTrialRecord[] records =
        [
            CreateRecord("NCT00000001", "Heart Study", "RECRUITING",
                "CARDIOLOGY", ["cardiac-risk", "heart-failure"],
                ["PHASE3"],
                new Investigator { Name = "Alice Smith", Affiliation = "Acme", Role = "PI" }),
            CreateRecord("NCT00000002", "Diabetes Study", "COMPLETED",
                "DIABETES", ["insulin-therapy", "hypertension"],
                ["PHASE2"],
                new Investigator { Name = "Bob Jones", Affiliation = "Acme", Role = "PI" })
        ];

        await _repo.UpdateStudiesWithClinicalTrialsAsync(records);

        var service = new AggregationService(_repo);
        await service.AggregateAsync();

        var catRows = await Context.CategoryAggregations
            .OrderByDescending(c => c.StudyCount)
            .ToListAsync();

        var diabetesRow = catRows.FirstOrDefault(c => c.CategoryName == "DIABETES");
        Assert.IsNotNull(diabetesRow, "DIABETES should appear in aggregations.");
        Assert.AreEqual("condition", diabetesRow!.CategoryType);
        Assert.AreEqual(1, diabetesRow.StudyCount);

        var insulinRow = catRows.FirstOrDefault(c => c.CategoryName == "insulin-therapy");
        Assert.IsNotNull(insulinRow, "insulin-therapy should appear in aggregations.");
        Assert.AreEqual("keyword", insulinRow!.CategoryType);
        Assert.AreEqual(1, insulinRow.StudyCount);

        var phase3Row = catRows.FirstOrDefault(c => c.CategoryName == "PHASE3");
        Assert.IsNotNull(phase3Row, "PHASE3 should appear in aggregations.");
        Assert.AreEqual("phase", phase3Row!.CategoryType);
        Assert.AreEqual(1, phase3Row.StudyCount);
    }

    private static ClinicalTrialRecord CreateRecord(string nctId, string title, string status,
        string condition, string[] keywords, string[] phases,
        params Investigator[] investigators)
    {
        return new ClinicalTrialRecord
        {
            NctId = nctId,
            BriefTitle = title,
            OverallStatus = status,
            OverallOfficials = investigators.ToList(),
            Conditions = [condition],
            Keywords = keywords.ToList(),
            Phases = phases.ToList()
        };
    }

    private static ClinicalTrialRecord CreateRecord(string nctId, string title, string status,
        params Investigator[] investigators)
    {
        return new ClinicalTrialRecord
        {
            NctId = nctId,
            BriefTitle = title,
            OverallStatus = status,
            OverallOfficials = investigators.ToList()
        };
    }
}

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class DataLossRemediationTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task NewScalarFields_ArePersisted()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        var record = new ClinicalTrialRecord
        {
            NctId = "NCT00999999",
            BriefTitle = "Data Loss Test Study",
            OverallStatus = "ACTIVE",
            OrgStudyId = "ORG-001",
            LeadSponsorName = "Test Sponsor Inc.",
            CollaboratorNames = ["Collab A", "Collab B"],
            EligibilityCriteria = "Inclusion: 18+ years",
            HealthyVolunteers = "true",
            Masking = "Double",
            Allocation = "Randomized",
            OverallOfficials = []
        };

        await repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        StudyEntity? study = await repo.GetStudyByNctIdAsync("NCT00999999");
        Assert.IsNotNull(study);
        Assert.AreEqual("ORG-001", study.OrgStudyId);
        Assert.AreEqual("Test Sponsor Inc.", study.LeadSponsorName);
        Assert.AreEqual("Collab A; Collab B", study.CollaboratorNames);
        Assert.AreEqual("Inclusion: 18+ years", study.EligibilityCriteria);
        Assert.AreEqual("true", study.HealthyVolunteers);
        Assert.AreEqual("Double", study.Masking);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Outcomes_ArePersisted()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        var record = new ClinicalTrialRecord
        {
            NctId = "NCT00999998",
            BriefTitle = "Outcomes Test",
            OverallStatus = "ACTIVE",
            PrimaryOutcomes =
            [
                new ClinicalTrialRecord.Outcome
                {
                    Measure = "Blood Pressure Change",
                    Description = "Change from baseline",
                    TimeFrame = "6 months"
                }
            ],
            SecondaryOutcomes =
            [
                new ClinicalTrialRecord.Outcome
                {
                    Measure = "Heart Rate",
                    Description = "Resting heart rate change",
                    TimeFrame = "3 months"
                }
            ],
            OverallOfficials = []
        };

        await repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        StudyEntity? study = await repo.GetStudyByNctIdAsync("NCT00999998");
        Assert.IsNotNull(study);
        Assert.IsTrue(study.Outcomes?.Count >= 2, "Should have primary and secondary outcomes");
        Assert.AreEqual(1, study.Outcomes!.Count(o => o.OutcomeType == "primary"));
        Assert.AreEqual(1, study.Outcomes!.Count(o => o.OutcomeType == "secondary"));

        var primary = study.Outcomes!.First(o => o.OutcomeType == "primary");
        Assert.AreEqual("Blood Pressure Change", primary.Measure);
        Assert.AreEqual("6 months", primary.TimeFrame);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ArmGroups_ArePersisted()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        var record = new ClinicalTrialRecord
        {
            NctId = "NCT00999997",
            BriefTitle = "Arm Group Test",
            OverallStatus = "ACTIVE",
            ArmGroups =
            [
                new ClinicalTrialRecord.ArmGroup
                {
                    Label = "Experimental",
                    Type = "Experimental",
                    Description = "New drug 50mg"
                },
                new ClinicalTrialRecord.ArmGroup
                {
                    Label = "Placebo",
                    Type = "Placebo Comparator",
                    Description = "Sugar pill"
                }
            ],
            OverallOfficials = []
        };

        await repo.UpdateStudiesWithClinicalTrialsAsync([record]);

        StudyEntity? study = await repo.GetStudyByNctIdAsync("NCT00999997");
        Assert.IsNotNull(study);
        Assert.AreEqual(2, study.ArmGroups?.Count, "Should have 2 arm groups");

        var experimental = study.ArmGroups!.First(a => a.Label == "Experimental");
        Assert.AreEqual("New drug 50mg", experimental.Description);
        Assert.AreEqual("Experimental", experimental.Type);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task NewColumns_ExistInSchema()
    {
        DbContextOptions<ClinicalTrialsContext> opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(ConnectionString).Options;
        await using var context = new ClinicalTrialsContext(opts);
        await context.Database.EnsureCreatedAsync();

        DbConnection connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_name = 'studies'
            ORDER BY ordinal_position";

        await using var reader = await cmd.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        Assert.IsTrue(columns.Contains("masking"), "masking column missing");
        Assert.IsTrue(columns.Contains("org_study_id"), "org_study_id column missing");
        Assert.IsTrue(columns.Contains("lead_sponsor_name"), "lead_sponsor_name column missing");
        Assert.IsTrue(columns.Contains("collaborator_names"), "collaborator_names column missing");
        Assert.IsTrue(columns.Contains("eligibility_criteria"), "eligibility_criteria column missing");
        Assert.IsTrue(columns.Contains("healthy_volunteers"), "healthy_volunteers column missing");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task NewTables_ExistInSchema()
    {
        DbContextOptions<ClinicalTrialsContext> opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(ConnectionString).Options;
        await using var context = new ClinicalTrialsContext(opts);
        await context.Database.EnsureCreatedAsync();

        DbConnection connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            ORDER BY table_name";

        await using var reader = await cmd.ExecuteReaderAsync();
        var tables = new List<string>();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.IsTrue(tables.Contains("study_outcomes"), "study_outcomes table missing");
        Assert.IsTrue(tables.Contains("study_arm_groups"), "study_arm_groups table missing");
    }
}

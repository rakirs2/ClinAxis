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
}

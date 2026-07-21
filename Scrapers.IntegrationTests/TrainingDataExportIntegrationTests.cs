using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class TrainingDataExportIntegrationTests : DbTestBase
{
    [TestMethod]
    public async Task TrainingKeywordsExport_CombinesAcceptedAndRejected()
    {
        Context.Studies.Add(new StudyEntity { NctId = "NCT0001", BriefTitle = "Study 1", OverallStatus = "ACTIVE" });
        Context.StudyKeywords.Add(new StudyKeywordEntity { StudyNctId = "NCT0001", Keyword = "Cancer" });
        Context.StudyKeywords.Add(new StudyKeywordEntity { StudyNctId = "NCT0001", Keyword = "Diabetes" });
        Context.StudyKeywords.Add(new StudyKeywordEntity { StudyNctId = "NCT0001", Keyword = "Immunotherapy" });
        Context.RejectedEntities.Add(new RejectedEntityEntity { EntityType = "keyword", Value = "junk text", StudyNctId = "NCT0001", RejectedAt = System.DateTime.UtcNow });
        Context.RejectedEntities.Add(new RejectedEntityEntity { EntityType = "keyword", Value = "abc123", StudyNctId = "NCT0001", RejectedAt = System.DateTime.UtcNow });
        await Context.SaveChangesAsync();

        var accepted = await Context.StudyKeywords
            .Select(k => new { Value = k.Keyword, Label = 1, k.StudyNctId })
            .ToListAsync();
        var rejected = await Context.RejectedEntities
            .Where(r => r.EntityType == "keyword")
            .Select(r => new { r.Value, Label = 0, r.StudyNctId })
            .ToListAsync();

        Assert.AreEqual(3, accepted.Count, "Should have 3 accepted keywords");
        Assert.AreEqual(2, rejected.Count, "Should have 2 rejected keywords");
        Assert.IsTrue(accepted.All(a => a.Label == 1), "All accepted should have label 1");
        Assert.IsTrue(rejected.All(r => r.Label == 0), "All rejected should have label 0");
    }

    [TestMethod]
    public async Task TrainingNamesExport_CombinesAcceptedAndRejected()
    {
        Context.Studies.Add(new StudyEntity { NctId = "NCT0002", BriefTitle = "Study 2", OverallStatus = "COMPLETED" });
        var person1 = new InvestigatorPersonEntity { FullName = "Alice Smith", IsHuman = true };
        var person2 = new InvestigatorPersonEntity { FullName = "Bob Jones", IsHuman = true };
        Context.InvestigatorPersons.AddRange(person1, person2);
        await Context.SaveChangesAsync();

        Context.StudyInvestigators.Add(new StudyInvestigatorEntity { StudyNctId = "NCT0002", InvestigatorPersonId = person1.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR" });
        Context.StudyInvestigators.Add(new StudyInvestigatorEntity { StudyNctId = "NCT0002", InvestigatorPersonId = person2.Id, RoleOnStudy = "SUB_INVESTIGATOR" });
        Context.RejectedEntities.Add(new RejectedEntityEntity { EntityType = "investigator_name", Value = "XYZ Pharma Research", StudyNctId = "NCT0002", RejectedAt = System.DateTime.UtcNow });
        await Context.SaveChangesAsync();

        var accepted = await Context.InvestigatorPersons
            .Join(Context.StudyInvestigators,
                p => p.Id,
                si => si.InvestigatorPersonId,
                (p, si) => new { Value = p.FullName, Label = 1, si.StudyNctId })
            .ToListAsync();
        var rejected = await Context.RejectedEntities
            .Where(r => r.EntityType == "investigator_name")
            .Select(r => new { r.Value, Label = 0, r.StudyNctId })
            .ToListAsync();

        Assert.AreEqual(2, accepted.Count, "Should have 2 accepted investigator names");
        Assert.AreEqual(1, rejected.Count, "Should have 1 rejected investigator name");
        Assert.IsTrue(accepted.All(a => a.Label == 1));
        Assert.IsTrue(rejected.All(r => r.Label == 0));
    }

    [TestMethod]
    public async Task TrainingConditionsExport_CombinesAcceptedAndRejected()
    {
        Context.Studies.Add(new StudyEntity { NctId = "NCT0003", BriefTitle = "Study 3", OverallStatus = "RECRUITING" });
        Context.StudyConditions.Add(new StudyConditionEntity { StudyNctId = "NCT0003", Condition = "Diabetes Mellitus" });
        Context.StudyConditions.Add(new StudyConditionEntity { StudyNctId = "NCT0003", Condition = "Hypertension" });
        Context.RejectedEntities.Add(new RejectedEntityEntity { EntityType = "condition", Value = "misc", StudyNctId = "NCT0003", RejectedAt = System.DateTime.UtcNow });
        Context.RejectedEntities.Add(new RejectedEntityEntity { EntityType = "condition", Value = "xyz disorder", StudyNctId = "NCT0003", RejectedAt = System.DateTime.UtcNow });
        Context.RejectedEntities.Add(new RejectedEntityEntity { EntityType = "condition", Value = "test condition", StudyNctId = "NCT0003", RejectedAt = System.DateTime.UtcNow });
        await Context.SaveChangesAsync();

        var accepted = await Context.StudyConditions
            .Select(c => new { Value = c.Condition, Label = 1, c.StudyNctId })
            .ToListAsync();
        var rejected = await Context.RejectedEntities
            .Where(r => r.EntityType == "condition")
            .Select(r => new { r.Value, Label = 0, r.StudyNctId })
            .ToListAsync();

        Assert.AreEqual(2, accepted.Count, "Should have 2 accepted conditions");
        Assert.AreEqual(3, rejected.Count, "Should have 3 rejected conditions");
        Assert.IsTrue(accepted.All(a => a.Label == 1));
        Assert.IsTrue(rejected.All(r => r.Label == 0));
    }

    [TestMethod]
    public async Task TrainingAffiliationsExport_CombinesAcceptedAndRejected()
    {
        Context.Studies.Add(new StudyEntity { NctId = "NCT0004", BriefTitle = "Study 4", OverallStatus = "ACTIVE" });
        var person = new InvestigatorPersonEntity { FullName = "Carol White", IsHuman = true };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        Context.StudyInvestigators.Add(new StudyInvestigatorEntity { StudyNctId = "NCT0004", InvestigatorPersonId = person.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR" });
        Context.InvestigatorAffiliations.Add(new InvestigatorAffiliationEntity { InvestigatorPersonId = person.Id, InstitutionName = "Stanford University" });
        Context.InvestigatorAffiliations.Add(new InvestigatorAffiliationEntity { InvestigatorPersonId = person.Id, InstitutionName = "Mayo Clinic" });
        Context.RejectedEntities.Add(new RejectedEntityEntity { EntityType = "affiliation", Value = "Some Unknown Corp LLC", StudyNctId = "NCT0004", RejectedAt = System.DateTime.UtcNow });
        await Context.SaveChangesAsync();

        var accepted = await Context.InvestigatorAffiliations
            .Join(Context.StudyInvestigators,
                af => af.InvestigatorPersonId,
                si => si.InvestigatorPersonId,
                (af, si) => new { Value = af.InstitutionName, Label = 1, si.StudyNctId })
            .ToListAsync();
        var rejected = await Context.RejectedEntities
            .Where(r => r.EntityType == "affiliation")
            .Select(r => new { r.Value, Label = 0, r.StudyNctId })
            .ToListAsync();

        Assert.AreEqual(2, accepted.Count, "Should have 2 accepted affiliations");
        Assert.AreEqual(1, rejected.Count, "Should have 1 rejected affiliation");
        Assert.IsTrue(accepted.All(a => a.Label == 1));
        Assert.IsTrue(rejected.All(r => r.Label == 0));
    }

    [TestMethod]
    public async Task TrainingKeywordsExport_AcceptedValuesMatchExpected()
    {
        Context.Studies.Add(new StudyEntity { NctId = "NCT0010", BriefTitle = "Study 10", OverallStatus = "ACTIVE" });
        Context.StudyKeywords.Add(new StudyKeywordEntity { StudyNctId = "NCT0010", Keyword = "Lung Cancer" });
        Context.StudyKeywords.Add(new StudyKeywordEntity { StudyNctId = "NCT0010", Keyword = "Covid-19" });
        await Context.SaveChangesAsync();

        var keywords = await Context.StudyKeywords
            .Select(k => new { k.Keyword, k.StudyNctId })
            .OrderBy(k => k.Keyword)
            .ToListAsync();

        Assert.AreEqual(2, keywords.Count);
        Assert.AreEqual("Covid-19", keywords[0].Keyword);
        Assert.AreEqual("Lung Cancer", keywords[1].Keyword);
        Assert.IsTrue(keywords.All(k => k.StudyNctId == "NCT0010"));
    }

    [TestMethod]
    public async Task TrainingNamesExport_MultipleStudiesSamePerson()
    {
        Context.Studies.Add(new StudyEntity { NctId = "NCT0101", BriefTitle = "Study 101", OverallStatus = "ACTIVE" });
        Context.Studies.Add(new StudyEntity { NctId = "NCT0102", BriefTitle = "Study 102", OverallStatus = "COMPLETED" });
        var person = new InvestigatorPersonEntity { FullName = "Dana King", IsHuman = true };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        Context.StudyInvestigators.Add(new StudyInvestigatorEntity { StudyNctId = "NCT0101", InvestigatorPersonId = person.Id });
        Context.StudyInvestigators.Add(new StudyInvestigatorEntity { StudyNctId = "NCT0102", InvestigatorPersonId = person.Id });
        await Context.SaveChangesAsync();

        var accepted = await Context.InvestigatorPersons
            .Join(Context.StudyInvestigators,
                p => p.Id,
                si => si.InvestigatorPersonId,
                (p, si) => new { p.FullName, si.StudyNctId })
            .OrderBy(x => x.StudyNctId)
            .ToListAsync();

        Assert.AreEqual(2, accepted.Count, "Same person on 2 studies = 2 rows");
        Assert.AreEqual("NCT0101", accepted[0].StudyNctId);
        Assert.AreEqual("NCT0102", accepted[1].StudyNctId);
    }

    [TestMethod]
    public async Task TrainingCsv_NoData_ReturnsEmptyResults()
    {
        var acceptedKeywords = await Context.StudyKeywords
            .Select(k => new { k.Keyword })
            .ToListAsync();
        var rejectedKeywords = await Context.RejectedEntities
            .Where(r => r.EntityType == "keyword")
            .Select(r => new { r.Value })
            .ToListAsync();
        var acceptedNames = await Context.InvestigatorPersons
            .Join(Context.StudyInvestigators,
                p => p.Id,
                si => si.InvestigatorPersonId,
                (p, si) => new { p.FullName })
            .ToListAsync();
        var acceptedConditions = await Context.StudyConditions
            .Select(c => new { c.Condition })
            .ToListAsync();
        var acceptedAffiliations = await Context.InvestigatorAffiliations
            .Join(Context.StudyInvestigators,
                af => af.InvestigatorPersonId,
                si => si.InvestigatorPersonId,
                (af, si) => new { af.InstitutionName })
            .ToListAsync();

        Assert.AreEqual(0, acceptedKeywords.Count, "No keywords seeded");
        Assert.AreEqual(0, rejectedKeywords.Count, "No rejected keywords seeded");
        Assert.AreEqual(0, acceptedNames.Count, "No investigators seeded");
        Assert.AreEqual(0, acceptedConditions.Count, "No conditions seeded");
        Assert.AreEqual(0, acceptedAffiliations.Count, "No affiliations seeded");
    }
}

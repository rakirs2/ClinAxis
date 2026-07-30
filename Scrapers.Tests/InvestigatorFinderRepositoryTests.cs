using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;

namespace Scrapers.Tests;

[TestClass]
public sealed class InvestigatorFinderRepositoryTests : DbTestBase
{
    [TestMethod]
    public async Task GetInvestigatorFinderCandidatesAsync_WithTreePaths_ReturnsInvestigators()
    {
        using (var ctx = CreateContext())
        {
            var desc = new MeshDescriptorEntity
            {
                Id = 9001, Cui = "TEST001", Name = "Diabetes Mellitus, Type 2",
                TreeNumbers = ["C19.246"],
                TreeNumberPaths = [new MeshTreePathEntity { MeshDescriptorId = 9001, TreeNumber = "C19.246" }]
            };
            ctx.MeshDescriptors.Add(desc);

            var investigator = new InvestigatorPersonEntity
            {
                Id = Guid.Parse("F0000000-0000-0000-0000-000000000001"),
                FullName = "Dr. Test Investigator",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            ctx.InvestigatorPersons.Add(investigator);

            var study = new StudyEntity
            {
                NctId = "NCT99999999",
                BriefTitle = "Test Diabetes Study",
                OverallStatus = "RECRUITING",
                Conditions = [new StudyConditionEntity { MeshDescriptorId = 9001 }],
            };
            ctx.Studies.Add(study);

            ctx.StudyInvestigators.Add(new StudyInvestigatorEntity
            {
                StudyNctId = study.NctId,
                InvestigatorPersonId = investigator.Id,
                RoleOnStudy = "PRINCIPAL_INVESTIGATOR",
                IsOverallOfficial = true,
            });

            await ctx.SaveChangesAsync();
        }

        var repo = new StudyRepository(ConnectionString);
        var results = await repo.GetInvestigatorFinderCandidatesAsync(
            treePrefixes: ["C19.246"],
            topN: 20);

        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.Any(r => r.Name == "Dr. Test Investigator"));
    }

    [TestMethod]
    public async Task GetInvestigatorFinderCandidatesAsync_WithoutTreePaths_ReturnsEmpty()
    {
        using (var ctx = CreateContext())
        {
            var desc = new MeshDescriptorEntity
            {
                Id = 9002, Cui = "TEST002", Name = "Diabetes Mellitus, Type 2",
                TreeNumbers = ["C19.246"],
                TreeNumberPaths = []
            };
            ctx.MeshDescriptors.Add(desc);

            var investigator = new InvestigatorPersonEntity
            {
                Id = Guid.Parse("F0000000-0000-0000-0000-000000000002"),
                FullName = "Dr. Missing Paths",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            ctx.InvestigatorPersons.Add(investigator);

            var study = new StudyEntity
            {
                NctId = "NCT99999998",
                BriefTitle = "Test No Paths Study",
                OverallStatus = "RECRUITING",
                Conditions = [new StudyConditionEntity { MeshDescriptorId = 9002 }],
            };
            ctx.Studies.Add(study);

            ctx.StudyInvestigators.Add(new StudyInvestigatorEntity
            {
                StudyNctId = study.NctId,
                InvestigatorPersonId = investigator.Id,
                RoleOnStudy = "PRINCIPAL_INVESTIGATOR",
                IsOverallOfficial = true,
            });

            await ctx.SaveChangesAsync();
        }

        var repo = new StudyRepository(ConnectionString);
        var results = await repo.GetInvestigatorFinderCandidatesAsync(
            treePrefixes: ["C19.246"],
            topN: 20);

        Assert.IsNotNull(results);
        Assert.AreEqual(0, results.Count);
    }

    private ClinicalTrialsContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<ClinicalTrialsContext>();
        builder.ConfigureNpgsql(ConnectionString);
        return new ClinicalTrialsContext(builder.Options);
    }
}

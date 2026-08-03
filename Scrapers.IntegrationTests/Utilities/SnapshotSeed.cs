using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.IntegrationTests.Utilities
{
    public static class SnapshotSeed
    {
    public static async Task SeedAsync(ClinicalTrialsContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
            // Seed studies
            ctx.Studies.Add(new StudyEntity
            {
                NctId = "NCT00000001",
                BriefTitle = "Sample Study 1",
                OverallStatus = "Completed",
                StudyType = "Interventional",
                EnrollmentCount = 100,
                StartDate = new DateOnly(2020, 1, 1),
                CompletionDate = new DateOnly(2021, 1, 1),
                IsIncomplete = false,
                CreatedAt = DateTime.UtcNow
            });
            ctx.Studies.Add(new StudyEntity
            {
                NctId = "NCT00000002",
                BriefTitle = "Sample Study 2",
                OverallStatus = "Recruiting",
                StudyType = "Observational",
                EnrollmentCount = 50,
                StartDate = new DateOnly(2021, 6, 1),
                CompletionDate = null,
                IsIncomplete = true,
                CreatedAt = DateTime.UtcNow
            });
            ctx.Studies.Add(new StudyEntity
            {
                NctId = "NCT00000003",
                BriefTitle = "Sample Study 3",
                OverallStatus = "Withdrawn",
                StudyType = "Expanded Access",
                EnrollmentCount = null,
                StartDate = null,
                CompletionDate = null,
                IsIncomplete = true,
                CreatedAt = DateTime.UtcNow
            });
            ctx.Studies.Add(new StudyEntity
            {
                NctId = "NCT00000004",
                BriefTitle = "Sample Study 4",
                OverallStatus = "Active, not recruiting",
                StudyType = "Interventional",
                EnrollmentCount = 200,
                StartDate = new DateOnly(2019, 5, 15),
                CompletionDate = null,
                IsIncomplete = false,
                CreatedAt = DateTime.UtcNow
            });
            ctx.Studies.Add(new StudyEntity
            {
                NctId = "NCT00000005",
                BriefTitle = "Sample Study 5",
                OverallStatus = "Completed",
                StudyType = "Interventional",
                EnrollmentCount = 150,
                StartDate = new DateOnly(2018, 3, 10),
                CompletionDate = new DateOnly(2019, 3, 10),
                IsIncomplete = false,
                CreatedAt = DateTime.UtcNow
            });

            // Investigator person
            var person = new InvestigatorPersonEntity
            {
                Id = Guid.Parse("A0000000-0000-0000-0000-000000000001"),
                FullName = "Dr. Sample",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            ctx.InvestigatorPersons.Add(person);
            ctx.StudyInvestigators.Add(new StudyInvestigatorEntity
            {
                StudyNctId = "NCT00000001",
                InvestigatorPersonId = person.Id,
                RoleOnStudy = "PRINCIPAL_INVESTIGATOR",
                IsOverallOfficial = true
            });

            // PubMed paper
            var pubmedPaper = new PubmedPaperEntity
            {
                Pmid = "12345678",
                Doi = "10.1000/xyz123",
                Title = "Sample PubMed Study",
                Journal = "Sample Journal",
                PublicationDate = new DateTime(2020, 2, 1),
                Abstract = "Sample abstract",
                IsNonEnglish = false,
                PublicationTypes = "Journal Article",
            };
            ctx.PubmedPapers.Add(pubmedPaper);
            ctx.StudyPapers.Add(new StudyPaperEntity
            {
                StudyNctId = "NCT00000001",
                PubmedPaperId = pubmedPaper.Id,
            });

            await ctx.SaveChangesAsync();
        }
    }
}

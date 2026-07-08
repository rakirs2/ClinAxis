using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Services;

namespace Scrapers.Coordinators
{
    public static class PipelineRunner
    {
        public static async Task<PipelineResult> RunAsync(
            int clinicalTrialsCount = 50,
            CancellationToken cancellationToken = default)
        {
            var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("POSTGRES_CONNECTION_STRING environment variable is not set.");

            var studyRepo = new StudyRepository(connectionString);

            await studyRepo.EnsureSchemaAsync(cancellationToken);
            await studyRepo.ClearAsync(cancellationToken);

            var clinicalTrialsClient = new ClinicalTrialsGov(pageSize: 100);
            var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);
            await clinicalTrialsIngestionService.IngestAsync(clinicalTrialsCount, cancellationToken);

            var contextOptions = new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(connectionString)
                .Options;

            await using (var context = new ClinicalTrialsContext(contextOptions))
            {
                var pubMedClient = new PubMedClient(new HttpClient());
                var pubMedScraperService = new PubMedScraperService(context, pubMedClient);
                await pubMedScraperService.IngestPubMedPapersAsync(cancellationToken);
            }

            await using (var context = new ClinicalTrialsContext(contextOptions))
            {
                var studyCount = await context.Studies.CountAsync(cancellationToken);
                var investigatorCount = await context.Investigators.CountAsync(cancellationToken);
                var pubmedStudyCount = await context.PubmedStudies.CountAsync(cancellationToken);

                var errors = new List<ValidationError>();

                if (studyCount < clinicalTrialsCount)
                {
                    errors.Add(new ValidationError("Studies",
                        $"Expected at least {clinicalTrialsCount} studies, found {studyCount}."));
                }

                if (investigatorCount == 0 && studyCount > 0)
                {
                    errors.Add(new ValidationError("Investigators",
                        "No investigators found despite having studies."));
                }

                var studies = await context.Studies
                    .Include(s => s.Investigators)
                    .ToListAsync(cancellationToken);

                foreach (var study in studies)
                {
                    if (!study.IsIncomplete && (study.Investigators == null || study.Investigators.Count == 0))
                    {
                        errors.Add(new ValidationError("StudyIntegrity",
                            $"Study {study.NctId} is not marked incomplete but has no investigators."));
                    }
                }

                var pubmedStudies = await context.PubmedStudies.ToListAsync(cancellationToken);
                var investigatorIds = await context.Investigators.Select(i => i.Id).ToListAsync(cancellationToken);

                foreach (var pubmed in pubmedStudies)
                {
                    if (!investigatorIds.Contains(pubmed.InvestigatorId))
                    {
                        errors.Add(new ValidationError("PubmedStudyIntegrity",
                            $"PubMed study '{pubmed.Title}' references non-existent investigator ID {pubmed.InvestigatorId}."));
                    }
                }

                return new PipelineResult(
                    studyCount,
                    investigatorCount,
                    pubmedStudyCount,
                    errors.Count > 0 ? errors.AsReadOnly() : null
                );
            }
        }
    }

    public record PipelineResult(
        int StudyCount,
        int InvestigatorCount,
        int PubmedStudyCount,
        IReadOnlyList<ValidationError>? Errors = null
    );

    public record ValidationError(string Entity, string Detail);
}
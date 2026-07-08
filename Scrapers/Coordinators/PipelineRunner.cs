using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
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

            var run = new PipelineRunEntity
            {
                StartedAt = DateTime.UtcNow,
                Status = "Running"
            };
            var runId = await studyRepo.AddPipelineRunAsync(run, cancellationToken);

            await studyRepo.EnsureSchemaAsync(cancellationToken);
            await studyRepo.ClearAsync(cancellationToken);

            try
            {

            var clinicalTrialsClient = new ClinicalTrialsGov(pageSize: 100);
            var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);
            await clinicalTrialsIngestionService.IngestAsync(clinicalTrialsCount, cancellationToken);

            var pubMedScraperService = new PubMedScraperService(connectionString);
            await pubMedScraperService.IngestPubMedPapersAsync(cancellationToken);

            var studyCount = await studyRepo.CountStudiesAsync(cancellationToken);
            var investigatorCount = await studyRepo.CountInvestigatorsAsync(cancellationToken);
            var pubmedStudyCount = await studyRepo.CountPubmedStudiesAsync(cancellationToken);
            var keywordCount = await studyRepo.CountKeywordsAsync(cancellationToken);
            var authorCount = await studyRepo.CountAuthorsAsync(cancellationToken);

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

            var studies = await studyRepo.GetStudiesWithInvestigatorsAsync(cancellationToken);

            foreach (var study in studies)
            {
                if (!study.IsIncomplete && (study.Investigators == null || study.Investigators.Count == 0))
                {
                    errors.Add(new ValidationError("StudyIntegrity",
                        $"Study {study.NctId} is not marked incomplete but has no investigators."));
                }
            }

            var pubmedStudies = await studyRepo.GetPubmedStudiesAsync(cancellationToken);
            var studyIds = studies.Select(s => s.NctId).ToHashSet();

            foreach (var pubmed in pubmedStudies)
            {
                if (!studyIds.Contains(pubmed.StudyNctId))
                {
                    errors.Add(new ValidationError("PubmedStudyIntegrity",
                        $"PubMed study PMID {pubmed.Pmid} references non-existent study NCT ID {pubmed.StudyNctId}."));
                }
            }

            var hasErrors = errors.Count > 0;
            await studyRepo.CompletePipelineRunAsync(runId, hasErrors ? "CompletedWithErrors" : "Completed",
                studyCount, investigatorCount, pubmedStudyCount, keywordCount, authorCount,
                cancellationToken: cancellationToken);

            return new PipelineResult(
                studyCount,
                investigatorCount,
                pubmedStudyCount,
                hasErrors ? errors.AsReadOnly() : null
            );
            }
            catch (Exception ex)
            {
                await studyRepo.CompletePipelineRunAsync(runId, "Failed",
                    errorMessage: ex.Message, cancellationToken: cancellationToken);
                throw;
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
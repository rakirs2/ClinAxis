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
            var connectionString = ConnectionStringProvider.Default;

            var studyRepo = new StudyRepository(connectionString);

            var run = new PipelineRunEntity
            {
                StartedAt = DateTime.UtcNow,
                Status = "Running"
            };
            var runId = await studyRepo.AddPipelineRunAsync(run, cancellationToken).ConfigureAwait(false);

            await studyRepo.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
            await studyRepo.ClearAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var clinicalTrialsClient = new ClinicalTrialsGov(pageSize: 100);
                var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);
                await clinicalTrialsIngestionService.IngestAsync(clinicalTrialsCount, cancellationToken: cancellationToken).ConfigureAwait(false);

                var pubMedScraperService = new PubMedScraperService(connectionString);
                await pubMedScraperService.IngestPubMedPapersAsync(cancellationToken).ConfigureAwait(false);

                var aggregationService = new AggregationService(studyRepo);
                await aggregationService.AggregateAsync(cancellationToken).ConfigureAwait(false);

                var studyCount = await studyRepo.CountStudiesAsync(cancellationToken).ConfigureAwait(false);
                var investigatorCount = await studyRepo.CountInvestigatorsAsync(cancellationToken).ConfigureAwait(false);
                var pubmedStudyCount = await studyRepo.CountPubmedStudiesAsync(cancellationToken).ConfigureAwait(false);
                var keywordCount = await studyRepo.CountKeywordsAsync(cancellationToken).ConfigureAwait(false);
                var authorCount = await studyRepo.CountAuthorsAsync(cancellationToken).ConfigureAwait(false);

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

                IReadOnlyList<StudyEntity> studies = await studyRepo.GetStudiesWithInvestigatorsAsync(cancellationToken).ConfigureAwait(false);

                foreach (StudyEntity study in studies)
                {
                    if (!study.IsIncomplete && (study.Investigators == null || study.Investigators.Count == 0))
                    {
                        errors.Add(new ValidationError("StudyIntegrity",
                            $"Study {study.NctId} is not marked incomplete but has no investigators."));
                    }
                }

                IReadOnlyList<PubmedStudyEntity> pubmedStudies = await studyRepo.GetPubmedStudiesAsync(cancellationToken).ConfigureAwait(false);
                var studyIds = studies.Select(s => s.NctId).ToHashSet();

                foreach (PubmedStudyEntity pubmed in pubmedStudies)
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
                    cancellationToken: cancellationToken).ConfigureAwait(false);

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
                    errorMessage: ex.Message, cancellationToken: cancellationToken).ConfigureAwait(false);
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

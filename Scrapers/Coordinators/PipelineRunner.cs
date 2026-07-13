using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

            await studyRepo.ResetDatabaseAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var clinicalTrialsClient = new ClinicalTrialsGov(pageSize: 100);
                var clinicalTrialsIngestionService = new ClinicalTrialsIngestionService(clinicalTrialsClient, studyRepo);
                await clinicalTrialsIngestionService.IngestAsync(clinicalTrialsCount, cancellationToken).ConfigureAwait(false);

                var pubMedScraperService = new PubMedScraperService(connectionString);
                await pubMedScraperService.IngestPubMedPapersAsync(cancellationToken).ConfigureAwait(false);

                using var requeueContext = new ClinicalTrialsContext(
                    new DbContextOptionsBuilder<ClinicalTrialsContext>()
                        .UseNpgsql(connectionString)
                        .Options);
                var requeuedCount = await StudyRepository.RequeueInvestigatorScrubEventsAsync(
                    requeueContext, cancellationToken).ConfigureAwait(false);

                var aggregationService = new AggregationService(studyRepo);
                await aggregationService.AggregateAsync(cancellationToken).ConfigureAwait(false);

                var studyCount = await studyRepo.CountStudiesAsync(cancellationToken).ConfigureAwait(false);
                var investigatorCount = await studyRepo.CountInvestigatorsAsync(cancellationToken).ConfigureAwait(false);
                var pubmedStudyCount = await studyRepo.CountPubmedPapersAsync(cancellationToken).ConfigureAwait(false);
                var keywordCount = await studyRepo.CountKeywordsAsync(cancellationToken).ConfigureAwait(false);

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
                    if (!study.IsIncomplete && (study.StudyInvestigators == null || study.StudyInvestigators.Count == 0))
                    {
                        errors.Add(new ValidationError("StudyIntegrity",
                            $"Study {study.NctId} is not marked incomplete but has no investigators."));
                    }
                }

                IReadOnlyList<PubmedPaperEntity> pubmedPapers = await studyRepo.GetPubmedPapersAsync(cancellationToken).ConfigureAwait(false);

                foreach (PubmedPaperEntity paper in pubmedPapers)
                {
                    if (string.IsNullOrWhiteSpace(paper.Pmid))
                    {
                        errors.Add(new ValidationError("PubmedPaperIntegrity",
                            $"PubMed paper {paper.Id} has no PMID."));
                    }
                }

                var hasErrors = errors.Count > 0;
                await studyRepo.CompletePipelineRunAsync(runId, hasErrors ? "CompletedWithErrors" : "Completed",
                    studyCount, investigatorCount, pubmedStudyCount, keywordCount,
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

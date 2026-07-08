using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence.Entities;

namespace Scrapers.Persistence
{
    public class StudyRepository
    {
        private readonly DbContextOptions<ClinicalTrialsContext> _options;

        public StudyRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string must be provided.", nameof(connectionString));
            }

            var builder = new DbContextOptionsBuilder<ClinicalTrialsContext>();
            builder.UseNpgsql(connectionString);
            _options = builder.Options;
        }

        public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
        {
            await using ClinicalTrialsContext context = CreateContext();
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> UpdateStudiesWithClinicalTrialsAsync(IEnumerable<ClinicalTrialRecord> records, CancellationToken cancellationToken = default)
        {
            var recordList = records?.ToList();
            if (recordList == null || recordList.Count == 0)
            {
                return 0;
            }

            await using ClinicalTrialsContext context = CreateContext();
            foreach (ClinicalTrialRecord? record in recordList)
            {
                if (record == null)
                {
                    throw new ArgumentNullException(nameof(records), "StudyRepository.UpdateStudiesWithClinicalTrialsAsync: record in list is null.");
                }

                if (string.IsNullOrWhiteSpace(record.NctId))
                {
                    throw new ArgumentException("Record NctId is null or whitespace.");
                }

                var incomplete = false;
                List<Investigator>? officials = record.OverallOfficials;
                if (officials == null || officials.Count == 0 || officials.Any(i => i == null || string.IsNullOrWhiteSpace(i.Name)))
                {
                    incomplete = true;
                }

                StudyEntity? entity = await context.Studies
                    .Include(s => s.Investigators)
                    .Include(s => s.Keywords)
                    .Include(s => s.Conditions)
                    .Include(s => s.Phases)
                    .FirstOrDefaultAsync(s => s.NctId == record.NctId, cancellationToken)
                    .ConfigureAwait(false);

                if (entity == null)
                {
                    entity = new StudyEntity
                    {
                        NctId = record.NctId!,
                        CreatedAt = DateTime.UtcNow,
                        Investigators = new List<InvestigatorEntity>(),
                        Keywords = new List<StudyKeywordEntity>(),
                        Conditions = new List<StudyConditionEntity>(),
                        Phases = new List<StudyPhaseEntity>()
                    };
                    context.Studies.Add(entity);
                }

                MapRecordToEntity(record, entity, incomplete);

                if (!incomplete)
                {
                    entity.Investigators!.Clear();
                    foreach (Investigator investigator in officials!)
                    {
                        entity.Investigators.Add(new InvestigatorEntity
                        {
                            StudyNctId = record.NctId!,
                            Name = investigator!.Name!,
                            Affiliation = investigator.Affiliation,
                            Role = investigator.Role
                        });
                    }

                    entity.Keywords!.Clear();
                    if (record.Keywords != null)
                    {
                        foreach (var kw in record.Keywords)
                        {
                            if (!string.IsNullOrWhiteSpace(kw))
                            {
                                entity.Keywords.Add(new StudyKeywordEntity { StudyNctId = record.NctId!, Keyword = kw.Trim() });
                            }
                        }
                    }

                    entity.Conditions!.Clear();
                    if (record.Conditions != null)
                    {
                        foreach (var cond in record.Conditions)
                        {
                            if (!string.IsNullOrWhiteSpace(cond))
                            {
                                entity.Conditions!.Add(new StudyConditionEntity { StudyNctId = record.NctId!, Condition = cond.Trim() });
                            }
                        }
                    }

                    entity.Phases!.Clear();
                    if (record.Phases != null)
                    {
                        foreach (var phase in record.Phases)
                        {
                            if (!string.IsNullOrWhiteSpace(phase))
                            {
                                entity.Phases!.Add(new StudyPhaseEntity { StudyNctId = record.NctId!, Phase = phase.Trim() });
                            }
                        }
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return recordList.Count;
        }

        private static void MapRecordToEntity(ClinicalTrialRecord record, StudyEntity entity, bool incomplete)
        {
            entity.BriefTitle = record.BriefTitle;
            entity.OfficialTitle = record.OfficialTitle;
            entity.OverallStatus = record.OverallStatus;
            entity.StudyType = record.StudyType;
            entity.BriefSummary = record.BriefSummary;
            entity.PrimaryPurpose = record.PrimaryPurpose;
            entity.InterventionModel = record.InterventionModel;
            entity.Allocation = record.Allocation;
            entity.EnrollmentCount = record.EnrollmentCount;
            entity.Sex = record.Sex;
            entity.MinimumAge = record.MinimumAge;
            entity.MaximumAge = record.MaximumAge;
            entity.StartDate = record.StartDate;
            entity.CompletionDate = record.CompletionDate;
            entity.StudyFirstPostDate = record.StudyFirstPostDate;
            entity.IsIncomplete = incomplete;
        }

        public async Task<int> CountStudiesAsync(CancellationToken cancellationToken = default)
        {
            await using ClinicalTrialsContext context = CreateContext();
            return await context.Studies.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountInvestigatorsAsync(CancellationToken cancellationToken = default)
        {
            await using ClinicalTrialsContext context = CreateContext();
            return await context.Investigators.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountPubmedStudiesAsync(CancellationToken cancellationToken = default)
        {
            await using ClinicalTrialsContext context = CreateContext();
            return await context.PubmedStudies.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountKeywordsAsync(CancellationToken cancellationToken = default)
        {
            await using ClinicalTrialsContext context = CreateContext();
            return await context.StudyKeywords.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountAuthorsAsync(CancellationToken cancellationToken = default)
        {
            await using ClinicalTrialsContext context = CreateContext();
            return await context.StudyAuthors.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> AddPipelineRunAsync(PipelineRunEntity run, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(run);

            await using ClinicalTrialsContext context = CreateContext();
            context.PipelineRuns.Add(run);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return run.Id;
        }

        public async Task CompletePipelineRunAsync(int runId, string status, int? studies = null, int? investigators = null,
            int? pubmedPapers = null, int? keywords = null, int? authors = null, string? errorMessage = null,
            CancellationToken cancellationToken = default)
        {
            await using ClinicalTrialsContext context = CreateContext();
            PipelineRunEntity? run = await context.PipelineRuns.FindAsync(new object[] { runId }, cancellationToken).ConfigureAwait(false);
            if (run != null)
            {
                run.CompletedAt = DateTime.UtcNow;
                run.Status = status;
                run.TotalStudies = studies;
                run.TotalInvestigators = investigators;
                run.TotalPubmedPapers = pubmedPapers;
                run.TotalKeywords = keywords;
                run.TotalAuthors = authors;
                run.ErrorMessage = errorMessage;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<List<PipelineRunEntity>> GetPipelineRunsAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            await using ClinicalTrialsContext context = CreateContext();
            return await context.PipelineRuns
                .OrderByDescending(r => r.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await using ClinicalTrialsContext context = CreateContext();
            context.StudyAuthors.RemoveRange(context.StudyAuthors);
            context.StudyKeywords.RemoveRange(context.StudyKeywords);
            context.StudyConditions.RemoveRange(context.StudyConditions);
            context.StudyPhases.RemoveRange(context.StudyPhases);
            context.PubmedStudies.RemoveRange(context.PubmedStudies);
            context.Investigators.RemoveRange(context.Investigators);
            context.Studies.RemoveRange(context.Studies);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<StudyEntity>> GetStudiesWithInvestigatorsAsync(CancellationToken cancellationToken = default)
        {
            await using ClinicalTrialsContext context = CreateContext();
            return await context.Studies
                .Include(s => s.Investigators)
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<PubmedStudyEntity>> GetPubmedStudiesAsync(CancellationToken cancellationToken = default)
        {
            await using ClinicalTrialsContext context = CreateContext();
            return await context.PubmedStudies.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<int>> GetInvestigatorIdsAsync(CancellationToken cancellationToken = default)
        {
            await using ClinicalTrialsContext context = CreateContext();
            return await context.Investigators.Select(i => i.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        private ClinicalTrialsContext CreateContext()
        {
            return new(_options);
        }
    }
}

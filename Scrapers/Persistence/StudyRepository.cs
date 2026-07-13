using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence.Entities;
using Scrapers.Services;
using Scrapers.Utilities;

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
            using ClinicalTrialsContext context = CreateContext();
            // Create schema based on EF Core model (no migrations needed until production)
            await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            // Drop and recreate schema fresh. Used on startup of IngestionApp / PipelineRunner
            // so every redeploy starts with a clean database.
            await context.Database.EnsureDeletedAsync(cancellationToken).ConfigureAwait(false);
            await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> UpdateStudiesWithClinicalTrialsAsync(IEnumerable<ClinicalTrialRecord> records, CancellationToken cancellationToken = default)
        {
            var recordList = records?.ToList();
            if (recordList == null || recordList.Count == 0)
            {
                return 0;
            }

            using ClinicalTrialsContext context = CreateContext();
            var batchPersons = new Dictionary<string, InvestigatorPersonEntity>(StringComparer.OrdinalIgnoreCase);
            var rejectedNames = new List<string>();
            var rejectedKeywords = new List<string>();
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
                var allOfficials = record.OverallOfficials?
                    .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Name))
                    .Select(i => (Name: i!.Name!, Role: i.Role))
                    .ToList();
                var officials = allOfficials?
                    .Where(t => NameFilter.IsHumanName(t.Name, t.Role))
                    .ToList();

                if (allOfficials != null && officials != null)
                {
                    rejectedNames.AddRange(allOfficials
                        .Where(o => !officials.Any(f => f.Name == o.Name))
                        .Select(o => $"{record.NctId}: {o.Name}"));
                }

                if (officials == null || officials.Count == 0)
                {
                    incomplete = true;
                }

                StudyEntity? entity = await context.Studies
                    .Include(s => s.StudyInvestigators)
                    .Include(s => s.Keywords)
                    .Include(s => s.Conditions)
                    .Include(s => s.Phases)
                    .Include(s => s.Locations)
                    .Include(s => s.References)
                    .FirstOrDefaultAsync(s => s.NctId == record.NctId, cancellationToken)
                    .ConfigureAwait(false);

                if (entity == null)
                {
                    entity = new StudyEntity
                    {
                        NctId = record.NctId!,
                        CreatedAt = DateTime.UtcNow,
                        StudyInvestigators = new List<StudyInvestigatorEntity>(),
                        Keywords = new List<StudyKeywordEntity>(),
                        Conditions = new List<StudyConditionEntity>(),
                        Phases = new List<StudyPhaseEntity>(),
                        Locations = new List<StudyLocationEntity>(),
                        References = new List<StudyReferenceEntity>(),
                        Outcomes = new List<StudyOutcomeEntity>(),
                        ArmGroups = new List<StudyArmGroupEntity>(),
                        StudyPapers = new List<StudyPaperEntity>()
                    };
                    context.Studies.Add(entity);
                }

                MapRecordToEntity(record, entity, incomplete);

                if (!incomplete)
                {
                    entity.StudyInvestigators!.Clear();
                    foreach (var (officialName, officialRole) in officials!)
                    {
                        var person = await FindOrCreatePersonAsync(context, batchPersons, officialName, cancellationToken);
                        entity.StudyInvestigators.Add(new StudyInvestigatorEntity
                        {
                            StudyNctId = record.NctId!,
                            InvestigatorPersonId = person.Id,
                            RoleOnStudy = officialRole,
                            IsOverallOfficial = true
                        });
                    }

                    entity.Keywords!.Clear();
                    if (record.Keywords != null)
                    {
                        var conditions = record.Conditions?
                            .Where(c => !string.IsNullOrWhiteSpace(c))
                            .Select(c => c.Trim())
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                        var knownShortMedicalTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "HIV", "HPV", "ALS", "MS", "IBS", "COPD", "ICU", "GI",
                            "ENT", "CT", "MRI", "PET", "CVD", "CHF", "CAD", "CKD",
                            "UTI", "STD", "PTSD", "ADHD", "GERD", "RA", "SLE",
                            "NASH", "NAFLD", "COPD", "OSA", "PCOS", "TBI", "SCI",
                        };

                        var originalKeywords = record.Keywords
                            .Where(k => !string.IsNullOrWhiteSpace(k))
                            .Select(k => k.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        var cleanedKeywords = originalKeywords
                            .Where(k => k.Length >= 4 || (k.Length >= 2 && knownShortMedicalTerms.Contains(k)))
                            .Where(k => k.Length <= 200)
                            .Where(k => conditions == null || !conditions.Contains(k))
                            .ToList();

                        var rejected = originalKeywords.Except(cleanedKeywords, StringComparer.OrdinalIgnoreCase).ToList();
                        rejectedKeywords.AddRange(rejected.Select(kw => $"{record.NctId}: {kw}"));

                        foreach (var kw in cleanedKeywords)
                        {
                            entity.Keywords.Add(new StudyKeywordEntity { StudyNctId = record.NctId!, Keyword = kw });
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

                    // Populate locations from API response (fix data loss bug)
                    entity.Locations!.Clear();
                    if (record.Locations != null && record.Locations.Count > 0)
                    {
                        foreach (var location in record.Locations)
                        {
                            if (location != null)
                            {
                                entity.Locations.Add(new StudyLocationEntity
                                {
                                    StudyNctId = record.NctId!,
                                    Facility = location.Facility,
                                    City = location.City,
                                    State = location.State,
                                    Country = location.Country
                                });
                            }
                        }
                    }
                }

                // Populate references from API response (fix data loss + eliminate redundant CT.gov per-study call)
                entity.References!.Clear();
                if (record.References != null && record.References.Count > 0)
                {
                    foreach (var reference in record.References)
                    {
                        if (reference != null && !string.IsNullOrWhiteSpace(reference.Pmid))
                        {
                            entity.References.Add(new StudyReferenceEntity
                            {
                                StudyNctId = record.NctId!,
                                Pmid = reference.Pmid,
                                Citation = reference.Citation,
                                Type = reference.Type
                            });
                        }
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (rejectedNames.Count > 0)
            {
                context.ScrapeEvents.Add(new ScrapeEventEntity
                {
                    Timestamp = DateTime.UtcNow,
                    Source = "NameFilter",
                    EventType = "rejection",
                    Level = "Warning",
                    Message = $"Rejected non-person official names ({rejectedNames.Count}): {string.Join("; ", rejectedNames.Take(20))}{(rejectedNames.Count > 20 ? $" (+{rejectedNames.Count - 20} more)" : "")}"
                });
            }

            if (rejectedKeywords.Count > 0)
            {
                context.ScrapeEvents.Add(new ScrapeEventEntity
                {
                    Timestamp = DateTime.UtcNow,
                    Source = "KeywordFilter",
                    EventType = "rejection",
                    Level = "Info",
                    Message = $"Filtered keywords ({rejectedKeywords.Count}): {string.Join("; ", rejectedKeywords.Take(20))}{(rejectedKeywords.Count > 20 ? $" (+{rejectedKeywords.Count - 20} more)" : "")}"
                });
            }

            if (rejectedNames.Count > 0 || rejectedKeywords.Count > 0)
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

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
            entity.Masking = record.Masking;
            entity.OrgStudyId = record.OrgStudyId;
            entity.LeadSponsorName = record.LeadSponsorName;
            entity.CollaboratorNames = record.CollaboratorNames != null
                ? string.Join("; ", record.CollaboratorNames)
                : null;
            entity.EligibilityCriteria = record.EligibilityCriteria;
            entity.HealthyVolunteers = record.HealthyVolunteers;
            entity.EnrollmentCount = record.EnrollmentCount;
            entity.Sex = record.Sex;
            entity.MinimumAge = record.MinimumAge;
            entity.MaximumAge = record.MaximumAge;
            entity.StartDate = record.StartDate;
            entity.CompletionDate = record.CompletionDate;
            entity.StudyFirstPostDate = record.StudyFirstPostDate;
            entity.IsIncomplete = incomplete;

            if (record.PrimaryOutcomes != null)
            {
                entity.Outcomes ??= new List<StudyOutcomeEntity>();
                foreach (var outcome in record.PrimaryOutcomes)
                {
                    entity.Outcomes.Add(new StudyOutcomeEntity
                    {
                        OutcomeType = "primary",
                        Measure = outcome.Measure,
                        Description = outcome.Description,
                        TimeFrame = outcome.TimeFrame
                    });
                }
            }

            if (record.SecondaryOutcomes != null)
            {
                entity.Outcomes ??= new List<StudyOutcomeEntity>();
                foreach (var outcome in record.SecondaryOutcomes)
                {
                    entity.Outcomes.Add(new StudyOutcomeEntity
                    {
                        OutcomeType = "secondary",
                        Measure = outcome.Measure,
                        Description = outcome.Description,
                        TimeFrame = outcome.TimeFrame
                    });
                }
            }

            if (record.ArmGroups != null)
            {
                entity.ArmGroups ??= new List<StudyArmGroupEntity>();
                foreach (var armGroup in record.ArmGroups)
                {
                    entity.ArmGroups.Add(new StudyArmGroupEntity
                    {
                        Label = armGroup.Label,
                        Type = armGroup.Type,
                        Description = armGroup.Description
                    });
                }
            }
        }

        public async Task<int> CountStudiesAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.Studies.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountInvestigatorsAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.InvestigatorPersons.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountPubmedPapersAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.PubmedPapers.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountKeywordsAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.StudyKeywords.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> AddPipelineRunAsync(PipelineRunEntity run, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(run);

            using ClinicalTrialsContext context = CreateContext();
            context.PipelineRuns.Add(run);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return run.Id;
        }

        public async Task CompletePipelineRunAsync(int runId, string status, int? studies = null, int? investigators = null,
            int? pubmedPapers = null, int? keywords = null, int? authors = null, string? errorMessage = null,
            CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
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
            using ClinicalTrialsContext context = CreateContext();
            return await context.PipelineRuns
                .OrderByDescending(r => r.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            context.PiAggregations.RemoveRange(context.PiAggregations);
            context.CategoryAggregations.RemoveRange(context.CategoryAggregations);
            context.StudyKeywords.RemoveRange(context.StudyKeywords);
            context.StudyConditions.RemoveRange(context.StudyConditions);
            context.StudyPhases.RemoveRange(context.StudyPhases);
            context.StudyPapers.RemoveRange(context.StudyPapers);
            context.PubmedPapers.RemoveRange(context.PubmedPapers);
            context.StudyInvestigators.RemoveRange(context.StudyInvestigators);
            context.InvestigatorAffiliations.RemoveRange(context.InvestigatorAffiliations);
            context.InvestigatorPersons.RemoveRange(context.InvestigatorPersons);
            context.Studies.RemoveRange(context.Studies);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<StudyEntity>> GetStudiesWithInvestigatorsAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.Studies
                .Include(s => s.StudyInvestigators)
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<PubmedPaperEntity>> GetPubmedPapersAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.PubmedPapers.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<StudyEntity>> GetStudiesByInvestigatorPersonIdAsync(Guid personId, StudySearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            using ClinicalTrialsContext context = CreateContext();

            var query = context.Studies
                .Include(s => s.StudyInvestigators!)
                    .ThenInclude(si => si.InvestigatorPerson)
                        .ThenInclude(ip => ip!.Affiliations)
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .Include(s => s.StudyPapers!).ThenInclude(sp => sp.PubmedPaper)
                .AsNoTracking()
                .Where(s => s.StudyInvestigators != null && s.StudyInvestigators.Any(si => si.InvestigatorPersonId == personId));

            if (!string.IsNullOrWhiteSpace(criteria.Keyword))
            {
                var keyword = $"%{criteria.Keyword}%";
                query = query.Where(s =>
                    (s.BriefTitle != null && EF.Functions.ILike(s.BriefTitle, keyword)) ||
                    (s.OfficialTitle != null && EF.Functions.ILike(s.OfficialTitle, keyword)) ||
                    (s.BriefSummary != null && EF.Functions.ILike(s.BriefSummary, keyword)) ||
                    EF.Functions.ILike(s.NctId, keyword));
            }

            if (criteria.Statuses != null && criteria.Statuses.Count > 0)
            {
                query = query.Where(s => s.OverallStatus != null && criteria.Statuses.Contains(s.OverallStatus));
            }

            if (criteria.Phases != null && criteria.Phases.Count > 0)
            {
                query = query.Where(s => s.Phases != null && s.Phases.Any(p => p.Phase != null && criteria.Phases.Contains(p.Phase)));
            }

            if (criteria.Conditions != null && criteria.Conditions.Count > 0)
            {
                query = query.Where(s => s.Conditions != null && s.Conditions.Any(c => c.Condition != null && criteria.Conditions.Contains(c.Condition)));
            }

            if (criteria.EnrollmentMin.HasValue)
            {
                query = query.Where(s => s.EnrollmentCount.HasValue && s.EnrollmentCount >= criteria.EnrollmentMin.Value);
            }

            if (criteria.EnrollmentMax.HasValue)
            {
                query = query.Where(s => s.EnrollmentCount.HasValue && s.EnrollmentCount <= criteria.EnrollmentMax.Value);
            }

            if (criteria.StartDateFrom.HasValue)
            {
                var fromDate = new DateOnly(criteria.StartDateFrom.Value.Year, criteria.StartDateFrom.Value.Month, criteria.StartDateFrom.Value.Day);
                query = query.Where(s => s.StartDate >= fromDate);
            }

            if (criteria.StartDateTo.HasValue)
            {
                var toDate = new DateOnly(criteria.StartDateTo.Value.Year, criteria.StartDateTo.Value.Month, criteria.StartDateTo.Value.Day);
                query = query.Where(s => s.StartDate <= toDate);
            }

            var skip = (criteria.Page - 1) * criteria.PageSize;
            query = query
                .OrderBy(s => s.BriefTitle)
                .Skip(skip)
                .Take(criteria.PageSize);

            return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<StudyEntity>> GetAllStudiesWithFullDataAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.Studies
                .Include(s => s.StudyInvestigators!)
                    .ThenInclude(si => si.InvestigatorPerson)
                        .ThenInclude(ip => ip!.Affiliations)
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .Include(s => s.StudyPapers!).ThenInclude(sp => sp.PubmedPaper)
                .Include(s => s.Outcomes)
                .Include(s => s.ArmGroups)
                .AsNoTracking()
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<StudyEntity>> GetStudiesPagedAsync(int page, int pageSize, string? search = null, string? status = null, string? phase = null, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            IQueryable<StudyEntity> query = context.Studies
                .Include(s => s.StudyInvestigators!)
                    .ThenInclude(si => si.InvestigatorPerson)
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .Include(s => s.StudyPapers!).ThenInclude(sp => sp.PubmedPaper)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s =>
                    (s.BriefTitle != null && EF.Functions.ILike(s.BriefTitle, $"%{search}%")) ||
                    (s.OfficialTitle != null && EF.Functions.ILike(s.OfficialTitle, $"%{search}%")) ||
                    (s.BriefSummary != null && EF.Functions.ILike(s.BriefSummary, $"%{search}%")) ||
                    EF.Functions.ILike(s.NctId, $"%{search}%"));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var statuses = status.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (statuses.Length > 0)
                {
                    query = query.Where(s => s.OverallStatus != null && statuses.Any(st => st == s.OverallStatus));
                }
            }

            if (!string.IsNullOrWhiteSpace(phase))
            {
                query = query.Where(s => s.Phases != null && s.Phases.Any(p => p.Phase == phase));
            }

            return await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountStudiesFilteredAsync(string? search = null, string? status = null, string? phase = null, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            IQueryable<StudyEntity> query = context.Studies.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s =>
                    (s.BriefTitle != null && EF.Functions.ILike(s.BriefTitle, $"%{search}%")) ||
                    (s.OfficialTitle != null && EF.Functions.ILike(s.OfficialTitle, $"%{search}%")) ||
                    (s.BriefSummary != null && EF.Functions.ILike(s.BriefSummary, $"%{search}%")) ||
                    EF.Functions.ILike(s.NctId, $"%{search}%"));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var statuses = status.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (statuses.Length > 0)
                {
                    query = query.Where(s => s.OverallStatus != null && statuses.Any(st => st == s.OverallStatus));
                }
            }

            if (!string.IsNullOrWhiteSpace(phase))
            {
                query = query.Where(s => s.Phases != null && s.Phases.Any(p => p.Phase == phase));
            }

            return await query.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<StudyEntity>> SearchStudiesAsync(
            StudySearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            using var context = CreateContext();

            var query = context.Studies
                .Include(s => s.StudyInvestigators!)
                    .ThenInclude(si => si.InvestigatorPerson)
                        .ThenInclude(ip => ip!.Affiliations)
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .Include(s => s.Locations)
                .Include(s => s.StudyPapers!).ThenInclude(sp => sp.PubmedPaper)
                .AsNoTracking();

            // Apply filters in order (helps query planner use indices)

            // 1. Keyword search (case-insensitive) - searches title, summary, NCT ID, and investigator names
            if (!string.IsNullOrWhiteSpace(criteria.Keyword))
            {
                var keyword = $"%{criteria.Keyword}%";
                query = query.Where(s =>
                    (s.BriefTitle != null && EF.Functions.ILike(s.BriefTitle, keyword)) ||
                    (s.OfficialTitle != null && EF.Functions.ILike(s.OfficialTitle, keyword)) ||
                    (s.BriefSummary != null && EF.Functions.ILike(s.BriefSummary, keyword)) ||
                    EF.Functions.ILike(s.NctId, keyword));
            }

            // 2. Status filter (multi-select)
            if (criteria.Statuses != null && criteria.Statuses.Count > 0)
            {
                var statuses = criteria.Statuses.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (statuses.Count > 0)
                {
                    query = query.Where(s => s.OverallStatus != null && statuses.Contains(s.OverallStatus));
                }
            }

            // 3. Phase filter (multi-select)
            if (criteria.Phases != null && criteria.Phases.Count > 0)
            {
                var phases = criteria.Phases.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                if (phases.Count > 0)
                {
                    query = query.Where(s => s.Phases!.Any(p => phases.Contains(p.Phase)));
                }
            }

            // 4. Condition filter (multi-select)
            if (criteria.Conditions != null && criteria.Conditions.Count > 0)
            {
                var conditions = criteria.Conditions.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                if (conditions.Count > 0)
                {
                    query = query.Where(s => s.Conditions!.Any(c => conditions.Contains(c.Condition)));
                }
            }

            // 5. Location filters (independent OR logic within each dimension)
            if (criteria.Countries != null && criteria.Countries.Count > 0)
            {
                var countries = criteria.Countries.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                if (countries.Count > 0)
                {
                    query = query.Where(s => s.Locations!.Any(l => l.Country != null && countries.Contains(l.Country)));
                }
            }

            if (criteria.States != null && criteria.States.Count > 0)
            {
                var states = criteria.States.Where(st => !string.IsNullOrWhiteSpace(st)).ToList();
                if (states.Count > 0)
                {
                    query = query.Where(s => s.Locations!.Any(l => l.State != null && states.Contains(l.State)));
                }
            }

            if (criteria.Cities != null && criteria.Cities.Count > 0)
            {
                var cities = criteria.Cities.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                if (cities.Count > 0)
                {
                    query = query.Where(s => s.Locations!.Any(l => l.City != null && cities.Contains(l.City)));
                }
            }

            if (criteria.Facilities != null && criteria.Facilities.Count > 0)
            {
                var facilities = criteria.Facilities.Where(f => !string.IsNullOrWhiteSpace(f)).ToList();
                if (facilities.Count > 0)
                {
                    query = query.Where(s => s.Locations!.Any(l => l.Facility != null && facilities.Contains(l.Facility)));
                }
            }

            // 6. Enrollment range filter
            if (criteria.EnrollmentMin.HasValue)
            {
                query = query.Where(s => s.EnrollmentCount >= criteria.EnrollmentMin.Value);
            }

            if (criteria.EnrollmentMax.HasValue)
            {
                query = query.Where(s => s.EnrollmentCount <= criteria.EnrollmentMax.Value);
            }

            // 7. Date range filter
            if (criteria.StartDateFrom.HasValue)
            {
                var fromDate = new DateOnly(criteria.StartDateFrom.Value.Year, criteria.StartDateFrom.Value.Month, criteria.StartDateFrom.Value.Day);
                query = query.Where(s => s.StartDate >= fromDate);
            }

            if (criteria.StartDateTo.HasValue)
            {
                var toDate = new DateOnly(criteria.StartDateTo.Value.Year, criteria.StartDateTo.Value.Month, criteria.StartDateTo.Value.Day);
                query = query.Where(s => s.StartDate <= toDate);
            }

            // Sort by StartDate DESC (newest first)
            query = query.OrderByDescending(s => s.StartDate);

            // Paginate
            var results = await query
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return results.AsReadOnly();
        }

        public async Task<int> CountStudiesFilteredAsync(
            StudySearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            using var context = CreateContext();

            var query = context.Studies
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .Include(s => s.Locations)
                .AsNoTracking();

            // Apply SAME filters as SearchStudiesAsync (copy filter logic)
            // This ensures pagination counts match results

            if (!string.IsNullOrWhiteSpace(criteria.Keyword))
            {
                var keyword = $"%{criteria.Keyword}%";
                query = query.Where(s =>
                    (s.BriefTitle != null && EF.Functions.ILike(s.BriefTitle, keyword)) ||
                    (s.OfficialTitle != null && EF.Functions.ILike(s.OfficialTitle, keyword)) ||
                    (s.BriefSummary != null && EF.Functions.ILike(s.BriefSummary, keyword)) ||
                    EF.Functions.ILike(s.NctId, keyword));
            }

            if (criteria.Statuses != null && criteria.Statuses.Count > 0)
            {
                var statuses = criteria.Statuses.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (statuses.Count > 0)
                {
                    query = query.Where(s => s.OverallStatus != null && statuses.Contains(s.OverallStatus));
                }
            }

            if (criteria.Phases != null && criteria.Phases.Count > 0)
            {
                var phases = criteria.Phases.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                if (phases.Count > 0)
                {
                    query = query.Where(s => s.Phases!.Any(p => phases.Contains(p.Phase)));
                }
            }

            if (criteria.Conditions != null && criteria.Conditions.Count > 0)
            {
                var conditions = criteria.Conditions.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                if (conditions.Count > 0)
                {
                    query = query.Where(s => s.Conditions!.Any(c => conditions.Contains(c.Condition)));
                }
            }

            if (criteria.Countries != null && criteria.Countries.Count > 0)
            {
                var countries = criteria.Countries.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                if (countries.Count > 0)
                {
                    query = query.Where(s => s.Locations!.Any(l => l.Country != null && countries.Contains(l.Country)));
                }
            }

            if (criteria.States != null && criteria.States.Count > 0)
            {
                var states = criteria.States.Where(st => !string.IsNullOrWhiteSpace(st)).ToList();
                if (states.Count > 0)
                {
                    query = query.Where(s => s.Locations!.Any(l => l.State != null && states.Contains(l.State)));
                }
            }

            if (criteria.Cities != null && criteria.Cities.Count > 0)
            {
                var cities = criteria.Cities.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                if (cities.Count > 0)
                {
                    query = query.Where(s => s.Locations!.Any(l => l.City != null && cities.Contains(l.City)));
                }
            }

            if (criteria.Facilities != null && criteria.Facilities.Count > 0)
            {
                var facilities = criteria.Facilities.Where(f => !string.IsNullOrWhiteSpace(f)).ToList();
                if (facilities.Count > 0)
                {
                    query = query.Where(s => s.Locations!.Any(l => l.Facility != null && facilities.Contains(l.Facility)));
                }
            }

            if (criteria.EnrollmentMin.HasValue)
            {
                query = query.Where(s => s.EnrollmentCount >= criteria.EnrollmentMin.Value);
            }

            if (criteria.EnrollmentMax.HasValue)
            {
                query = query.Where(s => s.EnrollmentCount <= criteria.EnrollmentMax.Value);
            }

            if (criteria.StartDateFrom.HasValue)
            {
                var fromDate = new DateOnly(criteria.StartDateFrom.Value.Year, criteria.StartDateFrom.Value.Month, criteria.StartDateFrom.Value.Day);
                query = query.Where(s => s.StartDate >= fromDate);
            }

            if (criteria.StartDateTo.HasValue)
            {
                var toDate = new DateOnly(criteria.StartDateTo.Value.Year, criteria.StartDateTo.Value.Month, criteria.StartDateTo.Value.Day);
                query = query.Where(s => s.StartDate <= toDate);
            }

            return await query.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get all distinct condition values for filter UI.
        /// </summary>
        public async Task<List<string>> GetDistinctConditionsAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.StudyConditions
                .Select(c => c.Condition)
                .Where(c => c != null)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Get all distinct location values grouped by type for filter UI.
        /// </summary>
        public async Task<(List<string> Countries, List<string> States, List<string> Cities, List<string> Facilities)> GetDistinctLocationsAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();

            var countries = (await context.StudyLocations
                .Select(l => l.Country)
                .Where(c => c != null)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)).Cast<string>().ToList();

            var states = (await context.StudyLocations
                .Select(l => l.State)
                .Where(s => s != null)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)).Cast<string>().ToList();

            var cities = (await context.StudyLocations
                .Select(l => l.City)
                .Where(c => c != null)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)).Cast<string>().ToList();

            var facilities = (await context.StudyLocations
                .Select(l => l.Facility)
                .Where(f => f != null)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)).Cast<string>().ToList();

            return (countries, states, cities, facilities);
        }

        /// <summary>
        /// Get distinct location values filtered by country/state/city.
        /// Used for dependent dropdowns in frontend advanced search.
        /// </summary>
        public async Task<(List<string> Countries, List<string> States, List<string> Cities, List<string> Facilities)> GetDistinctLocationsAsync(
            string? country = null,
            string? state = null,
            string? city = null,
            CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();

            // Start with all locations
            var query = context.StudyLocations.AsQueryable();

            // Apply filters if provided
            if (!string.IsNullOrEmpty(country))
                query = query.Where(l => l.Country == country);
            if (!string.IsNullOrEmpty(state))
                query = query.Where(l => l.State == state);
            if (!string.IsNullOrEmpty(city))
                query = query.Where(l => l.City == city);

            var countries = (await query
                .Select(l => l.Country)
                .Where(c => c != null)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)).Cast<string>().ToList();

            var states = (await query
                .Select(l => l.State)
                .Where(s => s != null)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)).Cast<string>().ToList();

            var cities = (await query
                .Select(l => l.City)
                .Where(c => c != null)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)).Cast<string>().ToList();

            var facilities = (await query
                .Select(l => l.Facility)
                .Where(f => f != null)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)).Cast<string>().ToList();

            return (countries, states, cities, facilities);
        }

        public async Task<StudyEntity?> GetStudyByNctIdAsync(string nctId, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.Studies
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .Include(s => s.StudyPapers!).ThenInclude(sp => sp.PubmedPaper)
                .Include(s => s.Outcomes)
                .Include(s => s.ArmGroups)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.NctId == nctId, cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountPiAggregationsAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.PiAggregations.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<CategoryTypeCount>> CountCategoryAggregationsByTypeAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            var raw = await context.CategoryAggregations
                .GroupBy(c => c.CategoryType)
                .Select(g => new { categoryType = g.Key, count = g.Count() })
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return raw.Select(r => new CategoryTypeCount(r.categoryType, r.count)).ToList();
        }

        public async Task ReplacePiAggregationsAsync(IReadOnlyList<PiAggregationEntity> aggregations, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            context.PiAggregations.RemoveRange(context.PiAggregations);
            context.PiAggregations.AddRange(aggregations);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task ReplaceCategoryAggregationsAsync(IReadOnlyList<CategoryAggregationEntity> aggregations, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            context.CategoryAggregations.RemoveRange(context.CategoryAggregations);
            context.CategoryAggregations.AddRange(aggregations);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task AddScrapeEventAsync(ScrapeEventEntity evt, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(evt);
            using ClinicalTrialsContext context = CreateContext();
            context.ScrapeEvents.Add(evt);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task AddScrapeEventsAsync(IEnumerable<ScrapeEventEntity> events, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            context.ScrapeEvents.AddRange(events);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ScrapeEventEntity>> GetRecentScrapeEventsAsync(int limit = 50, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.ScrapeEvents
                .OrderByDescending(e => e.Timestamp)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<DateTime?> GetLastSuccessfulPipelineRunDateAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.PipelineRuns
                .Where(r => r.Status == "Completed" || r.Status == "CompletedWithErrors")
                .OrderByDescending(r => r.StartedAt)
                .Select(r => (DateTime?)r.StartedAt)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<InvestigatorPersonSummary>> GetInvestigatorPersonsPagedAsync(
            int page, int pageSize, string? search = null,
            CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();

            IQueryable<InvestigatorPersonEntity> query = context.InvestigatorPersons
                .Include(p => p.StudyInvestigators)
                .Include(p => p.Affiliations)
                .Include(p => p.InvestigatorPapers)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => EF.Functions.ILike(p.FullName, $"%{search}%"));
            }

            IQueryable<InvestigatorPersonSummary> result = query
                .Select(p => new InvestigatorPersonSummary
                {
                    Uuid = p.Id,
                    Name = p.FullName,
                    Orcid = p.Orcid,
                    NcbiId = p.NcbiId,
                    PrimaryAffiliation = p.Affiliations!
                        .Where(a => a.IsPrimary)
                        .Select(a => a.InstitutionName)
                        .FirstOrDefault(),
                    StudyCount = p.StudyInvestigators!.Count,
                    PaperCount = p.InvestigatorPapers!.Count
                })
                .OrderByDescending(x => x.StudyCount)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            return await result.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountInvestigatorPersonsFilteredAsync(string? search = null, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();

            IQueryable<InvestigatorPersonEntity> query = context.InvestigatorPersons.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => EF.Functions.ILike(p.FullName, $"%{search}%"));
            }

            return await query.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<InvestigatorPersonEntity?> GetInvestigatorPersonByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.InvestigatorPersons
                .Include(p => p.StudyInvestigators)
                .Include(p => p.Affiliations)
                .Include(p => p.InvestigatorPapers)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == uuid, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<int> GetRecentScrapeEventCountAsync(TimeSpan within, CancellationToken cancellationToken = default)
        {
            DateTime since = DateTime.UtcNow - within;
            using ClinicalTrialsContext context = CreateContext();
            return await context.ScrapeEvents
                .CountAsync(e => e.Timestamp >= since, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<InvestigatorPersonEntity> FindOrCreatePersonAsync(
            ClinicalTrialsContext context,
            Dictionary<string, InvestigatorPersonEntity> batchPersons,
            string rawName,
            CancellationToken cancellationToken)
        {
            var (prefix, fullName, _) = NameParser.Parse(rawName);
            var raw = rawName.Trim();

            // 1. Check batch-local cache by parsed fullName, then by raw name
            if (batchPersons.TryGetValue(fullName, out var cached) ||
                (fullName != raw && batchPersons.TryGetValue(raw, out cached)))
            {
                cached.UpdatedAt = DateTime.UtcNow;
                if (cached.Prefix == null && prefix != null)
                {
                    cached.Prefix = prefix;
                }
                return cached;
            }

            // 2. Check database by parsed fullName, then by raw name (legacy records)
            var existing = await context.InvestigatorPersons
                .FirstOrDefaultAsync(p => p.FullName == fullName, cancellationToken)
                .ConfigureAwait(false);

            if (existing == null && fullName != raw)
            {
                existing = await context.InvestigatorPersons
                    .FirstOrDefaultAsync(p => p.FullName == raw, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (existing != null)
            {
                existing.UpdatedAt = DateTime.UtcNow;
                if (existing.Prefix == null && prefix != null)
                {
                    existing.Prefix = prefix;
                }
                batchPersons[fullName] = existing;
                return existing;
            }

            // 3. Create new person record and enqueue a scrub event
            var person = new InvestigatorPersonEntity
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Prefix = prefix,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.InvestigatorPersons.Add(person);
            context.PipelineEvents.Add(new PipelineEventEntity
            {
                EventType = "investigator.discovered",
                Data = person.Id.ToString(),
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            batchPersons[fullName] = person;
            return person;
        }

        public static async Task<int> RequeueInvestigatorScrubEventsAsync(
            ClinicalTrialsContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var personIds = await context.InvestigatorPersons
                .Select(p => p.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var existingEventPersonIds = await context.PipelineEvents
                .Where(e => e.EventType == "investigator.discovered" && e.Status != "dead-letter")
                .Select(e => e.Data)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var count = 0;
            foreach (var personId in personIds)
            {
                if (existingEventPersonIds.Contains(personId.ToString()))
                {
                    continue;
                }

                context.PipelineEvents.Add(new PipelineEventEntity
                {
                    EventType = "investigator.discovered",
                    Data = personId.ToString(),
                    Status = "pending",
                    CreatedAt = now,
                    UpdatedAt = now
                });
                count++;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return count;
        }

        public static async Task ScrubInvestigatorPapersAsync(
            ClinicalTrialsContext context,
            Guid personId,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            var person = await context.InvestigatorPersons
                .FirstOrDefaultAsync(p => p.Id == personId, cancellationToken)
                .ConfigureAwait(false);

            if (person == null)
            {
                return;
            }

            var pmids = await context.StudyReferences
                .Where(r => !string.IsNullOrWhiteSpace(r.Pmid))
                .Select(r => r.Pmid!)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var pmid in pmids)
            {
                if (string.IsNullOrWhiteSpace(pmid))
                {
                    continue;
                }

                PubMedScraperService.PaperDetail? paperDetail = null;

                var paper = await context.PubmedPapers
                    .FirstOrDefaultAsync(p => p.Pmid == pmid, cancellationToken)
                    .ConfigureAwait(false);

                if (paper == null)
                {
                    paperDetail = await PubMedScraperService.FetchPaperDetailAsync(pmid, cancellationToken).ConfigureAwait(false);
                    if (paperDetail == null)
                    {
                        continue;
                    }

                    paper = new PubmedPaperEntity
                    {
                        Pmid = pmid,
                        Doi = paperDetail.Doi,
                        Title = paperDetail.Title,
                        Journal = paperDetail.Journal,
                        PublicationDate = paperDetail.PublicationDate,
                        Abstract = paperDetail.Abstract,
                        IsNonEnglish = paperDetail.IsNonEnglish,
                    };
                    context.PubmedPapers.Add(paper);
                }

                var existingLink = await context.InvestigatorPapers
                    .AnyAsync(ip => ip.InvestigatorPersonId == personId && ip.PubmedPaperId == paper.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (!existingLink)
                {
                    context.InvestigatorPapers.Add(new InvestigatorPaperEntity
                    {
                        InvestigatorPersonId = personId,
                        PubmedPaperId = paper.Id,
                    });
                }

                if (paperDetail == null && person.Orcid == null)
                {
                    paperDetail = await PubMedScraperService.FetchPaperDetailAsync(pmid, cancellationToken).ConfigureAwait(false);
                }

                if (paperDetail?.Authors != null && person.Orcid == null)
                {
                    var fullNameParts = person.FullName.Split(',')[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var lastName = fullNameParts.LastOrDefault();
                    var foreName = fullNameParts.FirstOrDefault();

                    foreach (var author in paperDetail.Authors)
                    {
                        if (author.Orcid != null
                            && string.Equals(author.LastName, lastName, StringComparison.OrdinalIgnoreCase)
                            && (author.ForeName == null || foreName == null || author.ForeName.StartsWith(foreName[0].ToString(), StringComparison.OrdinalIgnoreCase)))
                        {
                            person.Orcid = author.Orcid;
                            person.VerifiedAt = DateTime.UtcNow;
                            person.VerificationSource = "PubMed";
                            break;
                        }
                    }
                }
            }

            person.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        private ClinicalTrialsContext CreateContext()
        {
            return new(_options);
        }
    }

    public class CategoryTypeCount
    {
        public string CategoryType { get; }
        public int Count { get; }

        public CategoryTypeCount(string categoryType, int count)
        {
            CategoryType = categoryType;
            Count = count;
        }
    }

    public class InvestigatorPersonSummary
    {
        public Guid Uuid { get; set; }
        public string? Name { get; set; }
        public string? Orcid { get; set; }
        public string? NcbiId { get; set; }
        public string? PrimaryAffiliation { get; set; }
        public int StudyCount { get; set; }
        public int PaperCount { get; set; }
    }
}

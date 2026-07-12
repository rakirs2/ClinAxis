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
            using ClinicalTrialsContext context = CreateContext();
            // Create schema based on EF Core model (no migrations needed until production)
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
                        Investigators = new List<InvestigatorEntity>(),
                        Keywords = new List<StudyKeywordEntity>(),
                        Conditions = new List<StudyConditionEntity>(),
                        Phases = new List<StudyPhaseEntity>(),
                        Locations = new List<StudyLocationEntity>(),
                        References = new List<StudyReferenceEntity>()
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

                // Persist all references from the API response (prevents data loss)
                entity.References ??= new List<StudyReferenceEntity>();
                entity.References.Clear();
                if (record.References != null && record.References.Count > 0)
                {
                    foreach (var reference in record.References)
                    {
                        if (reference != null)
                        {
                            entity.References.Add(new StudyReferenceEntity
                            {
                                StudyNctId = record.NctId!,
                                Pmid = reference.Pmid,
                                Doi = reference.Doi,
                                Citation = reference.Citation,
                                Type = reference.Type,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return recordList.Count;
        }

        private static void MapRecordToEntity(ClinicalTrialRecord record, StudyEntity entity, bool incomplete)
        {
            entity.ClinicalTrialsUpdatedAt = DateTime.UtcNow;
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
            using ClinicalTrialsContext context = CreateContext();
            return await context.Studies.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountInvestigatorsAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.Investigators.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountPubmedStudiesAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.PubmedStudies.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountKeywordsAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.StudyKeywords.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountAuthorsAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.StudyAuthors.CountAsync(cancellationToken).ConfigureAwait(false);
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
            using ClinicalTrialsContext context = CreateContext();
            return await context.Studies
                .Include(s => s.Investigators)
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<PubmedStudyEntity>> GetPubmedStudiesAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.PubmedStudies.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<int>> GetInvestigatorIdsAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.Investigators.Select(i => i.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a single investigator by UUID with their basic information.
        /// </summary>
        public async Task<InvestigatorEntity?> GetInvestigatorByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.Investigators
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Uuid == uuid, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Get all studies for a specific investigator, with support for filtering and pagination.
        /// </summary>
        public async Task<IReadOnlyList<StudyEntity>> GetStudiesByInvestigatorUuidAsync(Guid uuid, StudySearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            
            using ClinicalTrialsContext context = CreateContext();
            
            // Start by filtering to only studies where this investigator is involved
            var query = context.Studies
                .Include(s => s.Investigators)
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .Include(s => s.PubmedStudies)
                .AsNoTracking()
                .Where(s => s.Investigators != null && s.Investigators.Any(i => i.Uuid == uuid));

            // Apply the same filtering logic as SearchStudiesAsync
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

            // Apply pagination
            var skip = (criteria.Page - 1) * criteria.PageSize;
            query = query
                .OrderBy(s => s.BriefTitle)
                .Skip(skip)
                .Take(criteria.PageSize);

            return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<PubmedStudyEntity>> GetPubmedStudiesWithAuthorsAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.PubmedStudies
                .Include(p => p.Study)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<StudyEntity>> GetAllStudiesWithFullDataAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.Studies
                .Include(s => s.Investigators)
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .Include(s => s.PubmedStudies)
                .AsNoTracking()
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<StudyEntity>> GetStudiesPagedAsync(int page, int pageSize, string? search = null, string? status = null, string? phase = null, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            IQueryable<StudyEntity> query = context.Studies
                .Include(s => s.Investigators)
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .Include(s => s.PubmedStudies)
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
                .Include(s => s.Investigators)
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .Include(s => s.Locations)
                .Include(s => s.PubmedStudies)
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

        public async Task<StudyEntity?> GetStudyByNctIdAsync(string nctId, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.Studies
                .Include(s => s.Investigators)
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .Include(s => s.PubmedStudies)
                .Include(s => s.Authors)
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

        public async Task<IReadOnlyList<InvestigatorSummary>> GetInvestigatorsPagedAsync(
            int page, int pageSize, string? search = null,
            CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();

            IQueryable<InvestigatorEntity> query = context.Investigators.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(i => EF.Functions.ILike(i.Name!, $"%{search}%"));
            }

            IQueryable<InvestigatorSummary> grouped = query
                .GroupBy(i => new { i.Name, i.Affiliation })
                .Select(g => new InvestigatorSummary
                {
                    Uuid = g.First().Uuid,  // Use the UUID from the first investigator in the group
                    Name = g.Key.Name,
                    Affiliation = g.Key.Affiliation,
                    StudyCount = g.Select(i => i.StudyNctId).Distinct().Count()
                })
                .OrderByDescending(x => x.StudyCount)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            return await grouped.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> CountInvestigatorsFilteredAsync(string? search = null, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();

            IQueryable<InvestigatorEntity> query = context.Investigators.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(i => EF.Functions.ILike(i.Name!, $"%{search}%"));
            }

            return await query.Select(i => new { i.Name, i.Affiliation }).Distinct().CountAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> GetRecentScrapeEventCountAsync(TimeSpan within, CancellationToken cancellationToken = default)
        {
            DateTime since = DateTime.UtcNow - within;
            using ClinicalTrialsContext context = CreateContext();
            return await context.ScrapeEvents
                .CountAsync(e => e.Timestamp >= since, cancellationToken).ConfigureAwait(false);
        }

        public async Task<SourceCrawlStateEntity?> GetSourceCrawlStateAsync(string sourceName, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.SourceCrawlStates
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SourceName == sourceName, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task UpsertSourceCrawlStateAsync(SourceCrawlStateEntity state, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);

            using ClinicalTrialsContext context = CreateContext();
            SourceCrawlStateEntity? existing = await context.SourceCrawlStates
                .FirstOrDefaultAsync(s => s.SourceName == state.SourceName, cancellationToken)
                .ConfigureAwait(false);

            if (existing != null)
            {
                existing.LastCursor = state.LastCursor;
                existing.LastStartedAt = state.LastStartedAt;
                existing.LastSuccessAt = state.LastSuccessAt;
                existing.TotalRecordsFetched = state.TotalRecordsFetched;
                existing.Status = state.Status;
                existing.ErrorMessage = state.ErrorMessage;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                state.CreatedAt = DateTime.UtcNow;
                state.UpdatedAt = DateTime.UtcNow;
                context.SourceCrawlStates.Add(state);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task UpdateStudyCrawlTimestampAsync(string nctId, string source, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            StudyEntity? study = await context.Studies.FindAsync(new object[] { nctId }, cancellationToken).ConfigureAwait(false);
            if (study == null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (source == "ClinicalTrials")
            {
                study.ClinicalTrialsUpdatedAt = now;
            }
            else if (source == "PubMed")
            {
                study.PubMedUpdatedAt = now;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<string>> GetStudyNctIdsNeedingCrawlAsync(string source, int limit, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            IQueryable<StudyEntity> query = context.Studies.AsNoTracking();

            if (source == "PubMed")
            {
                query = query.Where(s => s.PubMedUpdatedAt == null && !s.IsIncomplete);
            }
            else if (source == "ClinicalTrials")
            {
                query = query.Where(s => s.ClinicalTrialsUpdatedAt == null);
            }

            return await query
                .Select(s => s.NctId)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<long> EnqueueEventAsync(PipelineEventEntity evt, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(evt);

            using ClinicalTrialsContext context = CreateContext();
            context.PipelineEvents.Add(evt);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return evt.Id;
        }

        public async Task<PipelineEventEntity?> ClaimNextEventAsync(string[] eventTypes, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();

            var eventTypeList = eventTypes.ToList();
            PipelineEventEntity? evt = await context.PipelineEvents
                .Where(e => e.Status == "pending" && eventTypeList.Contains(e.EventType))
                .OrderBy(e => e.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (evt == null)
            {
                return null;
            }

            evt.Status = "processing";
            evt.PickedUpAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return evt;
        }

        public async Task CompleteEventAsync(long eventId, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            PipelineEventEntity? evt = await context.PipelineEvents.FindAsync(new object[] { eventId }, cancellationToken).ConfigureAwait(false);
            if (evt != null)
            {
                evt.Status = "completed";
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task FailEventAsync(long eventId, string error, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            PipelineEventEntity? evt = await context.PipelineEvents.FindAsync(new object[] { eventId }, cancellationToken).ConfigureAwait(false);
            if (evt != null)
            {
                evt.Status = "failed";
                evt.ErrorMessage = error;
                evt.RetryCount++;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<int> GetPendingEventCountAsync(string eventType, CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.PipelineEvents
                .CountAsync(e => e.Status == "pending" && e.EventType == eventType, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<SourceCrawlStateEntity>> GetAllCrawlStatesAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            return await context.SourceCrawlStates
                .AsNoTracking()
                .OrderBy(s => s.SourceName)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<object> GetEventQueueStatsAsync(CancellationToken cancellationToken = default)
        {
            using ClinicalTrialsContext context = CreateContext();
            var pending = await context.PipelineEvents
                .CountAsync(e => e.Status == "pending", cancellationToken).ConfigureAwait(false);
            var processing = await context.PipelineEvents
                .CountAsync(e => e.Status == "processing", cancellationToken).ConfigureAwait(false);
            var completed = await context.PipelineEvents
                .CountAsync(e => e.Status == "completed", cancellationToken).ConfigureAwait(false);
            var failed = await context.PipelineEvents
                .CountAsync(e => e.Status == "failed", cancellationToken).ConfigureAwait(false);
            return new { pending, processing, completed, failed };
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

    public class InvestigatorSummary
    {
        public Guid Uuid { get; set; }
        public string? Name { get; set; }
        public string? Affiliation { get; set; }
        public int StudyCount { get; set; }
    }
}

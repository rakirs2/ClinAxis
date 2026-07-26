using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using static DataApi.Endpoints.EndpointHelpers;

namespace DataApi.Endpoints;

internal static class InvestigatorsEndpoints
{
    internal static void MapInvestigatorsEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/api/investigators", async (int? page, int? pageSize, string? search, bool? hasNpi) =>
        {
            var repo = new StudyRepository(connectionString);
            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 10, 1, 100);

            var persons = await repo.GetInvestigatorPersonsPagedAsync(p, ps, search, hasNpi);
            var total = await repo.CountInvestigatorPersonsFilteredAsync(search, hasNpi);

            return Results.Ok(new
            {
                data = persons.Select(p => new
                {
                    uuid = p.Uuid,
                    name = p.Name,
                    orcid = p.Orcid,
                    ncbiId = p.NcbiId,
                    npi = p.Npi,
                    studyCount = p.StudyCount,
                    paperCount = p.PaperCount,
                    primaryAffiliation = p.PrimaryAffiliation
                }),
                total,
                page = p,
                pageSize = ps,
                totalPages = (int)Math.Ceiling((double)total / ps)
            });
        });

        app.MapGet("/api/investigators/{uuid}", async (Guid uuid) =>
        {
            var repo = new StudyRepository(connectionString);
            var person = await repo.GetInvestigatorPersonByUuidAsync(uuid);

            if (person == null)
            {
                return Results.NotFound(new { message = "Investigator not found" });
            }

            var criteria = new StudySearchCriteria
            {
                Page = 1,
                PageSize = int.MaxValue
            };
            var studies = await repo.GetStudiesByInvestigatorPersonIdAsync(uuid, criteria);

            var detail = InvestigatorMapper.ToDetail(person, studies);
            return Results.Ok(detail);
        });

        app.MapGet("/api/investigators/{uuid}/studies", async (
            Guid uuid,
            int? page, int? pageSize,
            string? search, string? status, string? phase,
            string? condition,
            int? enrollmentMin, int? enrollmentMax,
            DateTime? startDateFrom, DateTime? startDateTo,
            string? sort, string? order) =>
        {
            var repo = new StudyRepository(connectionString);
            var person = await repo.GetInvestigatorPersonByUuidAsync(uuid);

            if (person == null)
            {
                return Results.NotFound(new { message = "Investigator not found" });
            }

            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 10, 1, 100);

            var criteria = new StudySearchCriteria
            {
                Keyword = search,
                Statuses = string.IsNullOrEmpty(status) ? null : status.Split(',').Select(s => s.Trim()).ToList(),
                Phases = string.IsNullOrEmpty(phase) ? null : phase.Split(',').Select(p => p.Trim()).ToList(),
                Conditions = string.IsNullOrEmpty(condition) ? null : condition.Split(',').Select(c => c.Trim()).ToList(),
                EnrollmentMin = enrollmentMin,
                EnrollmentMax = enrollmentMax,
                StartDateFrom = startDateFrom,
                StartDateTo = startDateTo,
                Page = p,
                PageSize = ps
            };

            var total = await repo.CountStudiesByInvestigatorPersonIdAsync(uuid, criteria);
            var studies = await repo.GetStudiesByInvestigatorPersonIdAsync(uuid, criteria);

            if (!string.IsNullOrEmpty(sort))
            {
                var isAscending = !order?.Equals("desc", StringComparison.OrdinalIgnoreCase) ?? true;
                studies = sort.Equals("title", StringComparison.OrdinalIgnoreCase) ?
                    (isAscending ? studies.OrderBy(s => s.BriefTitle).ToList() : studies.OrderByDescending(s => s.BriefTitle).ToList()) :
                    sort.Equals("status", StringComparison.OrdinalIgnoreCase) ?
                    (isAscending ? studies.OrderBy(s => s.OverallStatus).ToList() : studies.OrderByDescending(s => s.OverallStatus).ToList()) :
                    sort.Equals("phase", StringComparison.OrdinalIgnoreCase) ?
                    (isAscending ? studies.OrderBy(s => string.Join(",", s.Phases?.Select(p => p.Phase) ?? [])).ToList() : studies.OrderByDescending(s => string.Join(",", s.Phases?.Select(p => p.Phase) ?? [])).ToList()) :
                    sort.Equals("enrollment", StringComparison.OrdinalIgnoreCase) ?
                    (isAscending ? studies.OrderBy(s => s.EnrollmentCount ?? 0).ToList() : studies.OrderByDescending(s => s.EnrollmentCount ?? 0).ToList()) :
                    sort.Equals("startDate", StringComparison.OrdinalIgnoreCase) ?
                    (isAscending ? studies.OrderBy(s => s.StartDate.HasValue ? s.StartDate.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue).ToList() : studies.OrderByDescending(s => s.StartDate.HasValue ? s.StartDate.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue).ToList()) :
                    studies;
            }

            return Results.Ok(new
            {
                data = studies.Select(s => StudyMapper.ToSummary(s)),
                total,
                page = p,
                pageSize = ps,
                totalPages = (int)Math.Ceiling((double)total / ps)
            });
        });
    }
}

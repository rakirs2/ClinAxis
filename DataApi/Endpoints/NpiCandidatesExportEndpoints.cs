using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Utilities;

namespace DataApi.Endpoints;

/// <summary>
/// Exports the NPI disambiguation training corpus as CSV, one row per NPPES
/// candidate stored in <c>person_identifier_candidates</c>.
///
/// Feature parity: match features are recomputed with <see cref="NpiFeatureExtractor"/>
/// from the persisted NPPES fields (<see cref="NpiCandidateReconstructor"/>), so the
/// training frame has the exact same semantics the enrichment service computes at
/// serve time.
///
/// Labels (resolved batches only — ambiguous/not_found/error persons have no ground
/// truth and are excluded):
///   label = 1  the candidate that was auto-approved (IsAutoApproved)
///   label = 0  every other candidate in that person's NPPES batch
///
/// Batches without an approved candidate are skipped (fully-negative batch means the
/// assignment row was lost, e.g. a partially failed enrichment run).
///
/// Coverage convention (mirrors the pi-features export): every tri-state match
/// feature is exported as a value column (0/1) plus an explicit has_* column —
/// missing data is never silently imputed.
/// </summary>
internal static class NpiCandidatesExportEndpoints
{
    private static readonly string[] Header =
    [
        "person_id", "full_name", "batch_size",
        "exact_name_match",
        "middle_name_match", "has_middle_name_match",
        "credential_match", "has_credential_match",
        "state_match", "has_state_match",
        "city_match", "has_city_match",
        "org_match", "has_org_match",
        "other_name_match", "has_other_name_match",
        "specialty_match", "has_specialty_match",
        "license_state_match", "has_license_state_match",
        "department_match", "has_department_match",
        "orcid_match", "deactivated",
        "rule_score", "label",
    ];

    internal static void MapNpiCandidatesExportEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/api/export/training/npi-candidates", async (HttpResponse response, string? from, string? to) =>
        {
            var validationError = PiFeaturesExportEndpoints.ValidateWindow(from, to, out var windowStart, out var windowEnd);
            if (validationError is not null)
            {
                response.StatusCode = 400;
                await response.WriteAsJsonAsync(new { error = validationError });
                return;
            }

            var start = windowStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var endExclusive = windowEnd.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            using var ctx = new ClinicalTrialsContext(new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(connectionString).Options);

            var assignedPersonIds = await ctx.InvestigatorPersons
                .Where(p => p.NpiEnrichmentResult == "assigned")
                .Select(p => p.Id)
                .ToListAsync();

            var candidates = await ctx.PersonIdentifierCandidates
                .Where(c => c.IdentifierType == "NPI"
                    && assignedPersonIds.Contains(c.PersonId)
                    && c.CreatedAt >= start
                    && c.CreatedAt < endExclusive)
                .OrderBy(c => c.PersonId)
                .ThenBy(c => c.IdentifierValue)
                .AsNoTracking()
                .ToListAsync();

            response.ContentType = "text/csv";
            response.Headers["Content-Disposition"] = "attachment; filename=\"training-npi-candidates.csv\"";
            await response.WriteAsync(string.Join(",", Header) + "\n");

            if (candidates.Count == 0)
            {
                return;
            }

            var personIds = candidates.Select(c => c.PersonId).Distinct().ToList();

            var persons = await ctx.InvestigatorPersons
                .Where(p => personIds.Contains(p.Id))
                .Select(p => new { p.Id, p.FullName, p.Orcid })
                .AsNoTracking()
                .ToListAsync();

            var affiliations = await ctx.InvestigatorAffiliations
                .Where(a => personIds.Contains(a.InvestigatorPersonId) && a.IsPrimary)
                .Select(a => new { a.InvestigatorPersonId, a.InstitutionName, a.Department, a.City, a.State })
                .AsNoTracking()
                .ToListAsync();

            var meshDescriptorNames = await ctx.StudyInvestigators
                .Where(si => personIds.Contains(si.InvestigatorPersonId) && si.Study != null)
                .SelectMany(si => si.Study!.Conditions!, (si, c) => new { si.InvestigatorPersonId, Name = c.MeshDescriptor!.Name })
                .Where(x => x.Name != null)
                .Distinct()
                .AsNoTracking()
                .ToListAsync();

            var personsById = persons.ToDictionary(p => p.Id);
            var affiliationByPerson = affiliations
                .GroupBy(a => a.InvestigatorPersonId)
                .ToDictionary(g => g.Key, g => g.First());
            var descriptorsByPerson = meshDescriptorNames
                .GroupBy(m => m.InvestigatorPersonId)
                .ToDictionary(g => g.Key, g => g.Select(m => m.Name));

            foreach (var personBatch in candidates.GroupBy(c => c.PersonId))
            {
                var batch = personBatch.ToList();

                // Resolved batches only: a batch without an approved candidate has no
                // ground truth and is excluded from the training corpus.
                if (!batch.Any(c => c.IsAutoApproved))
                {
                    continue;
                }

                var person = personsById[personBatch.Key];
                var affiliation = affiliationByPerson.GetValueOrDefault(person.Id);
                var profile = NpiFeatureExtractor.BuildProfile(
                    person.FullName ?? string.Empty,
                    person.Orcid,
                    affiliation?.InstitutionName,
                    affiliation?.Department,
                    affiliation?.City,
                    affiliation?.State,
                    descriptorsByPerson.GetValueOrDefault(person.Id, []));

                foreach (var candidate in batch)
                {
                    var features = NpiFeatureExtractor.Extract(profile, NpiCandidateReconstructor.Reconstruct(candidate));

                    var cols = new string[]
                    {
                        candidate.PersonId.ToString(),
                        Quote(person.FullName ?? string.Empty),
                        batch.Count.ToString(CultureInfo.InvariantCulture),
                        Bool(features.ExactNameMatch),
                        Bool(features.MiddleNameMatch),
                        Has(features.MiddleNameMatch),
                        Bool(features.CredentialMatch),
                        Has(features.CredentialMatch),
                        Bool(features.StateMatch),
                        Has(features.StateMatch),
                        Bool(features.CityMatch),
                        Has(features.CityMatch),
                        Bool(features.OrgMatch),
                        Has(features.OrgMatch),
                        Bool(features.OtherNameMatch),
                        Has(features.OtherNameMatch),
                        Bool(features.SpecialtyMatch),
                        Has(features.SpecialtyMatch),
                        Bool(features.LicenseStateMatch),
                        Has(features.LicenseStateMatch),
                        Bool(features.DepartmentMatch),
                        Has(features.DepartmentMatch),
                        Bool(features.OrcidMatch),
                        Bool(features.IsDeactivated),
                        Fmt(candidate.RuleScore),
                        Bool(candidate.IsAutoApproved),
                    };
                    await response.WriteAsync(string.Join(",", cols) + "\n");
                }
            }
        });
    }

    private static string Bool(bool? value) => value == true ? "1" : "0";

    private static string Has(bool? value) => value.HasValue ? "1" : "0";

    private static string Fmt(double? value) => value?.ToString("0.######", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

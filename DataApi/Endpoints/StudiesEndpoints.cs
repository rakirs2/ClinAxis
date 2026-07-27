using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using static DataApi.Endpoints.EndpointHelpers;

namespace DataApi.Endpoints;

internal static class StudiesEndpoints
{
    internal static void MapStudiesEndpoints(this WebApplication app, string connectionString, MeshTreeStore meshTreeStore)
    {
        app.MapGet("/api/distinct-conditions", async (IMemoryCache cache) =>
        {
            var cacheKey = "conditions_list";
            if (cache.TryGetValue(cacheKey, out List<string>? conditions) && conditions is not null)
            {
                return Results.Ok(conditions);
            }

            var repo = new StudyRepository(connectionString);
            conditions = await repo.GetDistinctConditionsAsync();
            var ttl = TimeSpan.FromMinutes(app.Configuration.GetValue<int>("CacheSettings:ConditionsCacheDurationMinutes", 5));
            cache.Set(cacheKey, conditions, ttl);
            return Results.Ok(conditions);
        });

        app.MapGet("/api/distinct-locations", async (IMemoryCache cache, string? country, string? state, string? city) =>
        {
            var cacheKey = $"locations_{country ?? ""}_{state ?? ""}_{city ?? ""}";
            if (cache.TryGetValue(cacheKey, out object? cached) && cached is not null)
            {
                return Results.Ok(cached);
            }

            var repo = new StudyRepository(connectionString);
            var (countries, states, cities, facilities) = await repo.GetDistinctLocationsAsync(country, state, city);
            var result = new { countries, states, cities, facilities };
            var ttl = TimeSpan.FromMinutes(app.Configuration.GetValue<int>("CacheSettings:LocationsCacheDurationMinutes", 5));
            cache.Set(cacheKey, result, ttl);
            return Results.Ok(result);
        });

        app.MapGet("/api/mesh-tree", (string? branch, int minStudyCount = 0, int depth = 1) =>
        {
            var (descriptors, counts) = meshTreeStore.Snapshot();

            object result;

            if (string.IsNullOrEmpty(branch))
            {
                var categories = new (string Prefix, string Name)[]
                {
                    ("A", "Anatomy"),
                    ("B", "Organisms"),
                    ("C", "Diseases"),
                    ("D", "Chemicals and Drugs"),
                    ("E", "Analytical, Diagnostic and Therapeutic Techniques and Equipment"),
                    ("F", "Psychiatry and Psychology"),
                    ("G", "Phenomena and Processes"),
                    ("H", "Disciplines and Occupations"),
                    ("I", "Anthropology, Education, Sociology and Social Phenomena"),
                    ("J", "Technology, Industry, Agriculture"),
                    ("K", "Humanities"),
                    ("L", "Information Science"),
                    ("M", "Named Groups"),
                    ("N", "Health Care"),
                    ("V", "Publication Characteristics"),
                    ("Z", "Geographicals"),
                };

                var rootNodes = categories
                    .Select(c =>
                    {
                        var studyCount = descriptors
                            .Where(d => d.TreeNumbers.Any(tn => tn.StartsWith(c.Prefix, StringComparison.Ordinal)))
                            .Sum(d => counts.TryGetValue(d.Id, out var cnt) ? cnt : 0);
                        var hasChildren = descriptors
                            .Any(d => d.TreeNumbers.Any(tn => tn.StartsWith(c.Prefix, StringComparison.Ordinal) && tn.Length > 1));
                        var children = depth > 1 && hasChildren ? GetBranchNodes(c.Prefix, depth - 1).ToArray() : null;
                        return new
                        {
                            treeNumber = c.Prefix,
                            name = c.Name,
                            studyCount,
                            hasChildren,
                            children,
                        };
                    })
                    .Where(n => n.studyCount >= minStudyCount)
                    .ToList();

                result = new { branch = "__root__", nodes = rootNodes };
            }
            else
            {
                var nodes = GetBranchNodes(branch!, depth);
                result = new { branch, nodes };
            }

            return Results.Ok(result);

            List<object> GetBranchNodes(string currentBranch, int remainingDepth)
            {
                var isTopLevel = currentBranch.Length == 1;
                var prefix = isTopLevel ? currentBranch : currentBranch + ".";
                var childBranches = descriptors
                    .SelectMany(d => d.TreeNumbers)
                    .Where(tn => tn.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(tn =>
                    {
                        var remainder = tn[prefix.Length..];
                        var dotIdx = remainder.IndexOf('.', StringComparison.Ordinal);
                        return dotIdx > 0 ? tn[..(prefix.Length + dotIdx)] : tn;
                    })
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x)
                    .ToList();

                var results = new List<object>();
                foreach (var childTn in childBranches)
                {
                    var matchingDescriptors = descriptors
                        .Where(d => d.TreeNumbers.Any(tn =>
                            tn.Equals(childTn, StringComparison.Ordinal) ||
                            tn.StartsWith(childTn + ".", StringComparison.Ordinal)))
                        .ToList();

                    var studyCount = matchingDescriptors.Sum(d => counts.TryGetValue(d.Id, out var cnt) ? cnt : 0);
                    if (studyCount < minStudyCount)
                        continue;

                    var desc = matchingDescriptors.OrderByDescending(d => counts.TryGetValue(d.Id, out var cnt) ? cnt : 0).FirstOrDefault();
                    var hasChildren = descriptors.Any(d => d.TreeNumbers.Any(tn =>
                        tn.StartsWith(childTn + ".", StringComparison.Ordinal) && (counts.TryGetValue(d.Id, out var cnt) ? cnt : 0) > 0));

                    object[]? children = null;
                    if (remainingDepth > 1 && hasChildren)
                    {
                        children = GetBranchNodes(childTn, remainingDepth - 1).ToArray();
                    }

                    results.Add(new
                    {
                        treeNumber = childTn,
                        name = desc?.Name ?? childTn,
                        studyCount,
                        hasChildren,
                        children,
                    });
                }
                return results;
            }
        });

        app.MapGet("/api/mesh-tree/search", (string q, string? branch, int maxResults = 20) =>
        {
            var (descriptors, counts) = meshTreeStore.Snapshot();

            var queryWords = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var results = descriptors
                .Where(d => d.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || WordMatch(d.Name, queryWords))
                .Where(d => string.IsNullOrEmpty(branch) || d.TreeNumbers.Any(tn => tn.StartsWith(branch, StringComparison.Ordinal)))
                .OrderByDescending(d => counts.TryGetValue(d.Id, out var cnt) ? cnt : 0)
                .ThenBy(d => d.Name)
                .Take(maxResults)
                .Select(d => new
                {
                    descriptorId = d.Id,
                    name = d.Name,
                    treeNumber = d.TreeNumbers.FirstOrDefault() ?? "",
                    studyCount = counts.TryGetValue(d.Id, out var cnt) ? cnt : 0,
                })
                .ToList();

            return Results.Ok(results);
        });

        app.MapGet("/api/studies", async (
            int? page, int? pageSize,
            string? keyword,
            string? status, string? phase,
            string? condition, string? meshTree,
            string? country, string? state, string? city, string? facility,
            int? enrollmentMin, int? enrollmentMax,
            DateTime? startDateFrom, DateTime? startDateTo) =>
        {
            var repo = new StudyRepository(connectionString);
            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 10, 1, 100);

            var criteria = new StudySearchCriteria
            {
                Keyword = keyword,
                Statuses = ParseCsvParam(status),
                Phases = ParseCsvParam(phase),
                Conditions = ParseCsvParam(condition),
                MeshTreePrefixes = ParseCsvParam(meshTree),
                Countries = ParseCsvParam(country),
                States = ParseCsvParam(state),
                Cities = ParseCsvParam(city),
                Facilities = ParseCsvParam(facility),
                EnrollmentMin = enrollmentMin,
                EnrollmentMax = enrollmentMax,
                StartDateFrom = startDateFrom,
                StartDateTo = startDateTo,
                Page = p,
                PageSize = ps
            };

            var studies = await repo.SearchStudiesAsync(criteria);
            var total = await repo.CountStudiesFilteredAsync(criteria);

            return Results.Ok(new
            {
                data = studies.Select(s => StudyMapper.ToSummary(s)),
                total,
                page = p,
                pageSize = ps,
                totalPages = (int)Math.Ceiling((double)total / ps)
            });
        });

        app.MapGet("/api/studies/{nctId}", async (string nctId) =>
        {
            var repo = new StudyRepository(connectionString);
            StudyEntity? study = await repo.GetStudyByNctIdAsync(nctId);
            return study is null ? Results.NotFound(new { error = "Study not found" }) : Results.Ok(StudyMapper.ToDetail(study));
        });
    }

    private static bool WordMatch(string name, string[] queryWords)
    {
        var nameWords = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return queryWords.All(qw => nameWords.Any(nw => WordMatches(qw, nw)));
    }

    private static bool WordMatches(string queryWord, string nameWord)
    {
        if (nameWord.StartsWith(queryWord, StringComparison.OrdinalIgnoreCase))
            return true;
        if (queryWord.Length > 3 && queryWord.EndsWith('s'))
            return nameWord.StartsWith(queryWord[..^1], StringComparison.OrdinalIgnoreCase);
        if (queryWord.Length > 4 && queryWord.EndsWith("es", StringComparison.OrdinalIgnoreCase))
            return nameWord.StartsWith(queryWord[..^2], StringComparison.OrdinalIgnoreCase);
        return false;
    }
}

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
            var key = $"mesh|{branch ?? ""}|{minStudyCount}|{depth}";
            var result = meshTreeStore.GetOrBuildTree(key, () => meshTreeStore.BuildTree(branch, minStudyCount, depth));
            return Results.Ok(result);
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

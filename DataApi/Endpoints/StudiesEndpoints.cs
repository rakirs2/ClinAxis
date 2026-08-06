using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Utilities;
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
            // MeSH location mode sends descriptor NAMES ("California") while the DB stores
            // normalized values ("CA") — normalize before matching (LocationNormalizer is
            // identity for already-canonical values, so raw mode is unaffected).
            var normalizedCountry = LocationNormalizer.NormalizeCountry(country);
            var normalizedState = LocationNormalizer.NormalizeState(state);

            var cacheKey = $"locations_{normalizedCountry ?? ""}_{normalizedState ?? ""}_{city ?? ""}";
            if (cache.TryGetValue(cacheKey, out object? cached) && cached is not null)
            {
                return Results.Ok(cached);
            }

            var repo = new StudyRepository(connectionString);
            var (countries, states, cities, facilities) = await repo.GetDistinctLocationsAsync(normalizedCountry, normalizedState, city);
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

        app.MapGet("/api/mesh-tree/search", (string q, string? branch, int maxResults = 200) =>
        {
            var results = meshTreeStore.SearchDescriptors(q, branch, maxResults);
            return Results.Ok(results);
        });

        app.MapGet("/api/studies", async (
            int? page, int? pageSize,
            string? keyword,
            string? status, string? phase,
            string? condition, string? meshTree,
            string? country, string? state, string? city, string? facility,
            string? locationMeshTree,
            int? enrollmentMin, int? enrollmentMax,
            DateTime? startDateFrom, DateTime? startDateTo,
            bool? includeRemoved) =>
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
                LocationMeshTreePrefixes = ParseCsvParam(locationMeshTree),
                Countries = ParseCsvParam(country),
                States = ParseCsvParam(state),
                Cities = ParseCsvParam(city),
                Facilities = ParseCsvParam(facility),
                EnrollmentMin = enrollmentMin,
                EnrollmentMax = enrollmentMax,
                StartDateFrom = startDateFrom,
                StartDateTo = startDateTo,
                IncludeRemoved = includeRemoved ?? false,
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

}

using System;
using System.Linq;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Utilities
{
    /// <summary>
    /// Single source of truth for the /api/studies filter predicates, shared by
    /// StudyRepository.SearchStudiesAsync and CountStudiesFilteredAsync so the
    /// result and count queries can never drift (the count query previously
    /// omitted the location MeSH prefix filter, producing wrong total/totalPages).
    /// Predicates use ToLower()/StartsWith() instead of EF.Functions.ILike so they
    /// translate to LOWER() LIKE / LIKE 'p%' in EF Core and also run against
    /// in-memory IQueryable in unit tests. All filters combine with AND across
    /// dimensions and OR within each multi-select dimension.
    /// </summary>
    internal static class StudySearchFilter
    {
        public static IQueryable<StudyEntity> ApplyFilters(IQueryable<StudyEntity> query, StudySearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(criteria);

            if (!criteria.IncludeRemoved)
            {
                query = query.Where(s => s.RemovedFromSourceAt == null);
            }

            // 1. Keyword search (case-insensitive substring) - title, summary, NCT ID, study keywords
            if (!string.IsNullOrWhiteSpace(criteria.Keyword))
            {
                var keyword = criteria.Keyword.ToLower();
                query = query.Where(s =>
                    (s.BriefTitle != null && s.BriefTitle.ToLower().Contains(keyword)) ||
                    (s.OfficialTitle != null && s.OfficialTitle.ToLower().Contains(keyword)) ||
                    (s.BriefSummary != null && s.BriefSummary.ToLower().Contains(keyword)) ||
                    s.NctId.ToLower().Contains(keyword) ||
                    (s.Keywords != null && s.Keywords.Any(k => k.Keyword.ToLower().Contains(keyword))));
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
                    query = query.Where(s => s.Phases != null && s.Phases.Any(p => phases.Contains(p.Phase)));
                }
            }

            // 4. Condition filter (multi-select, exact MeSH descriptor name)
            if (criteria.Conditions != null && criteria.Conditions.Count > 0)
            {
                var conditions = criteria.Conditions.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                if (conditions.Count > 0)
                {
                    query = query.Where(s => s.Conditions != null && s.Conditions.Any(c =>
                        c.MeshDescriptor != null && conditions.Contains(c.MeshDescriptor.Name)));
                }
            }

            // 4b. MeSH tree prefix filter (hierarchical via mesh_tree_paths)
            if (criteria.MeshTreePrefixes != null && criteria.MeshTreePrefixes.Count > 0)
            {
                var prefixes = criteria.MeshTreePrefixes.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                if (prefixes.Count > 0)
                {
                    query = query.Where(s => s.Conditions != null && s.Conditions.Any(c =>
                        c.MeshDescriptor != null &&
                        c.MeshDescriptor.TreeNumberPaths != null &&
                        c.MeshDescriptor.TreeNumberPaths.Any(tnp => prefixes.Any(p => tnp.TreeNumber.StartsWith(p)))));
                }
            }

            // 5. Location filters (independent OR logic within each dimension)
            if (criteria.Countries != null && criteria.Countries.Count > 0)
            {
                var countries = criteria.Countries.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                if (countries.Count > 0)
                {
                    query = query.Where(s => s.Locations != null && s.Locations.Any(l => l.Country != null && countries.Contains(l.Country)));
                }
            }

            if (criteria.States != null && criteria.States.Count > 0)
            {
                var states = criteria.States.Where(st => !string.IsNullOrWhiteSpace(st)).ToList();
                if (states.Count > 0)
                {
                    query = query.Where(s => s.Locations != null && s.Locations.Any(l => l.State != null && states.Contains(l.State)));
                }
            }

            if (criteria.Cities != null && criteria.Cities.Count > 0)
            {
                var cities = criteria.Cities.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                if (cities.Count > 0)
                {
                    query = query.Where(s => s.Locations != null && s.Locations.Any(l => l.City != null && cities.Contains(l.City)));
                }
            }

            if (criteria.Facilities != null && criteria.Facilities.Count > 0)
            {
                var facilities = criteria.Facilities.Where(f => !string.IsNullOrWhiteSpace(f)).ToList();
                if (facilities.Count > 0)
                {
                    query = query.Where(s => s.Locations != null && s.Locations.Any(l => l.Facility != null && facilities.Contains(l.Facility)));
                }
            }

            // 5b. Location MeSH tree prefix filter (hierarchical via Z-Geographicals)
            if (criteria.LocationMeshTreePrefixes != null && criteria.LocationMeshTreePrefixes.Count > 0)
            {
                var prefixes = criteria.LocationMeshTreePrefixes.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                if (prefixes.Count > 0)
                {
                    query = query.Where(s => s.Locations != null && s.Locations.Any(l =>
                        l.MeshDescriptor != null &&
                        l.MeshDescriptor.TreeNumbers.Any(tn => prefixes.Any(p => tn.StartsWith(p)))));
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

            return query;
        }
    }
}

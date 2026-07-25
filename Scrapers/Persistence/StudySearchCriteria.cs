using System;
using System.Collections.Generic;

namespace Scrapers.Persistence
{
    /// <summary>
    /// Encapsulates all filter criteria for searching studies.
    /// Silent validation: non-existent filter values return empty results, not errors.
    /// </summary>
    public class StudySearchCriteria
    {
        // Text search
        public string? Keyword { get; set; }

        // Multi-select filters
        public IReadOnlyList<string>? Statuses { get; set; }       // e.g., ["RECRUITING", "ACTIVE"]
        public IReadOnlyList<string>? Phases { get; set; }         // e.g., ["PHASE1", "PHASE2"]
        public IReadOnlyList<string>? Conditions { get; set; }     // e.g., ["Diabetes", "Hypertension"]

        // MeSH tree prefix filter
        public IReadOnlyList<string>? MeshTreePrefixes { get; set; } // e.g., ["C19.246", "C08.127"]

        // Location filters (uses StudyLocationEntity from PR 1)
        public IReadOnlyList<string>? Countries { get; set; }      // e.g., ["USA", "Canada"]
        public IReadOnlyList<string>? States { get; set; }         // e.g., ["CA", "NY"]
        public IReadOnlyList<string>? Cities { get; set; }         // e.g., ["San Francisco"]
        public IReadOnlyList<string>? Facilities { get; set; }     // e.g., ["Stanford Medical Center"]

        // Range filters
        public int? EnrollmentMin { get; set; }
        public int? EnrollmentMax { get; set; }
        public DateTime? StartDateFrom { get; set; }
        public DateTime? StartDateTo { get; set; }

        // Pagination
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}

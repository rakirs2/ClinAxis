using System;
using System.Collections.Generic;
using System.Linq;

namespace Scrapers.Utilities
{
    /// <summary>
    /// Assembles <see cref="BestPiScorer.CandidateSignals"/> from repository-shaped rows —
    /// pure and DB-free so the aggregation (RecEngineSignals rollup, SpecialtyCategoryMapper
    /// categories, distinct regions) is unit-testable. The POST /api/recommend/investigators
    /// service maps query results onto these shapes and calls <see cref="Assemble"/>.
    /// </summary>
    internal static class RecCandidateAssembler
    {
        /// <summary>Person-level scalars used by the Best-PI candidate row.</summary>
        public readonly record struct CandidateScalars(
            Guid Uuid,
            string? Name,
            string? PrimaryAffiliation,
            int StudyCount,
            int? HIndex,
            int PaperCount);

        public static IReadOnlyList<BestPiScorer.CandidateSignals> Assemble(
            IEnumerable<CandidateScalars> candidates,
            IEnumerable<Scrapers.Persistence.RecommendationStudyRow> studyRows,
            DateOnly asOf)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            ArgumentNullException.ThrowIfNull(studyRows);

            var rowsByPerson = studyRows
                .GroupBy(r => r.PersonId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return candidates
                .Select(c =>
                {
                    var rows = rowsByPerson.GetValueOrDefault(c.Uuid) ?? [];

                    var signals = rows.Select(r => new RecEngineSignals.StudySignalInput(
                        r.OverallStatus, r.EnrollmentCount, r.StartDate, r.CompletionDate));
                    var aggregate = RecEngineSignals.Aggregate(signals, asOf);

                    var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var conditionName in rows.SelectMany(r => r.ConditionNames))
                    {
                        categories.UnionWith(SpecialtyCategoryMapper.MapToCategories(conditionName));
                    }

                    var regions = rows.SelectMany(r => r.Countries)
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    return new BestPiScorer.CandidateSignals(
                        c.Uuid,
                        c.Name,
                        c.PrimaryAffiliation,
                        c.StudyCount,
                        aggregate.CompletionRate,
                        aggregate.EnrollmentVelocity,
                        c.HIndex,
                        c.PaperCount,
                        categories,
                        regions);
                })
                .ToList();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Services
{
    public class AggregationService
    {
        private readonly StudyRepository _repository;

        public AggregationService(StudyRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task AggregateAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<StudyEntity> studies = await _repository.GetAllStudiesWithFullDataAsync(cancellationToken);

            await AggregatePiCountsAsync(studies, cancellationToken);
            await AggregateCategoryCountsAsync(studies, cancellationToken);
        }

        private async Task AggregatePiCountsAsync(IReadOnlyList<StudyEntity> studies, CancellationToken cancellationToken)
        {
            var piMap = new Dictionary<string, (HashSet<string> StudyIds, HashSet<string> Affiliations, int PubmedCount)>(
                StringComparer.OrdinalIgnoreCase);

            foreach (StudyEntity study in studies)
            {
                if (study.Investigators == null)
                {
                    continue;
                }

                var pubmedCount = study.PubmedStudies?.Count ?? 0;

                foreach (InvestigatorEntity investigator in study.Investigators)
                {
                    if (string.IsNullOrWhiteSpace(investigator.Name))
                    {
                        continue;
                    }

                    if (!piMap.TryGetValue(investigator.Name, out (HashSet<string> StudyIds, HashSet<string> Affiliations, int PubmedCount) entry))
                    {
                        entry = (new HashSet<string>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
                    }

                    entry.StudyIds.Add(study.NctId);
                    if (!string.IsNullOrWhiteSpace(investigator.Affiliation))
                    {
                        entry.Affiliations.Add(investigator.Affiliation);
                    }
                    entry.PubmedCount += pubmedCount;
                    piMap[investigator.Name] = entry;
                }
            }

            var aggregations = piMap
                .Select(kvp => new PiAggregationEntity
                {
                    InvestigatorName = kvp.Key,
                    Affiliation = kvp.Value.Affiliations.Count > 0
                        ? string.Join("; ", kvp.Value.Affiliations.OrderBy(a => a))
                        : null,
                    StudyCount = kvp.Value.StudyIds.Count,
                    PubmedPaperCount = kvp.Value.PubmedCount,
                    StudyNctIds = string.Join(",", kvp.Value.StudyIds.OrderBy(id => id)),
                    ComputedAt = DateTime.UtcNow
                })
                .OrderByDescending(a => a.StudyCount)
                .ToList();

            await _repository.ReplacePiAggregationsAsync(aggregations, cancellationToken);
        }

        private async Task AggregateCategoryCountsAsync(IReadOnlyList<StudyEntity> studies, CancellationToken cancellationToken)
        {
            var catMap = new Dictionary<string, (string Type, HashSet<string> StudyIds, int PubmedCount)>(
                StringComparer.OrdinalIgnoreCase);

            foreach (StudyEntity study in studies)
            {
                var pubmedCount = study.PubmedStudies?.Count ?? 0;

                if (study.Conditions != null)
                {
                    foreach (StudyConditionEntity condition in study.Conditions)
                    {
                        if (string.IsNullOrWhiteSpace(condition.Condition))
                        {
                            continue;
                        }

                        if (!catMap.TryGetValue(condition.Condition, out (string Type, HashSet<string> StudyIds, int PubmedCount) catEntry))
                        {
                            catEntry = ("condition", new HashSet<string>(), 0);
                        }

                        catEntry.StudyIds.Add(study.NctId);
                        catEntry.PubmedCount += pubmedCount;
                        catMap[condition.Condition] = catEntry;
                    }
                }

                if (study.Keywords != null)
                {
                    foreach (StudyKeywordEntity keyword in study.Keywords)
                    {
                        if (string.IsNullOrWhiteSpace(keyword.Keyword))
                        {
                            continue;
                        }

                        if (!catMap.TryGetValue(keyword.Keyword, out (string Type, HashSet<string> StudyIds, int PubmedCount) kwEntry))
                        {
                            kwEntry = ("keyword", new HashSet<string>(), 0);
                        }

                        kwEntry.StudyIds.Add(study.NctId);
                        kwEntry.PubmedCount += pubmedCount;
                        catMap[keyword.Keyword] = kwEntry;
                    }
                }

                if (study.Phases != null)
                {
                    foreach (StudyPhaseEntity phase in study.Phases)
                    {
                        if (string.IsNullOrWhiteSpace(phase.Phase))
                        {
                            continue;
                        }

                        if (!catMap.TryGetValue(phase.Phase, out (string Type, HashSet<string> StudyIds, int PubmedCount) phEntry))
                        {
                            phEntry = ("phase", new HashSet<string>(), 0);
                        }

                        phEntry.StudyIds.Add(study.NctId);
                        phEntry.PubmedCount += pubmedCount;
                        catMap[phase.Phase] = phEntry;
                    }
                }
            }

            var aggregations = catMap
                .Select(kvp => new CategoryAggregationEntity
                {
                    CategoryName = kvp.Key,
                    CategoryType = kvp.Value.Type,
                    StudyCount = kvp.Value.StudyIds.Count,
                    PubmedPaperCount = kvp.Value.PubmedCount,
                    StudyNctIds = string.Join(",", kvp.Value.StudyIds.OrderBy(id => id)),
                    ComputedAt = DateTime.UtcNow
                })
                .OrderByDescending(a => a.StudyCount)
                .ToList();

            await _repository.ReplaceCategoryAggregationsAsync(aggregations, cancellationToken);
        }
    }
}
using Scrapers.Persistence.Entities;

namespace DataApi;

internal static class InvestigatorMapper
{
    /// <summary>
    /// Maps an InvestigatorPersonEntity to a summary response for list views.
    /// </summary>
    internal static object ToSummary(InvestigatorPersonEntity person)
    {
        return new
        {
            uuid = person.Id,
            name = person.FullName,
            orcid = person.Orcid,
            ncbiId = person.NcbiId,
            studyCount = person.StudyInvestigators?.Count ?? 0,
            paperCount = person.InvestigatorPapers?.Count ?? 0,
            primaryAffiliation = person.Affiliations?.FirstOrDefault(a => a.IsPrimary)?.InstitutionName
        };
    }

    /// <summary>
    /// Maps an InvestigatorPersonEntity to a detailed response.
    /// </summary>
    internal static object ToDetail(InvestigatorPersonEntity person, IEnumerable<StudyEntity> studies)
    {
        var studyList = studies.ToList();

        var coInvestigators = new HashSet<string>();
        foreach (var study in studyList)
        {
            if (study.StudyInvestigators != null)
            {
                foreach (var si in study.StudyInvestigators)
                {
                    if (si.InvestigatorPerson?.FullName != null && si.InvestigatorPerson.FullName != person.FullName)
                    {
                        coInvestigators.Add(si.InvestigatorPerson.FullName);
                    }
                }
            }
        }

        var conditions = new HashSet<string>();
        foreach (var study in studyList)
        {
            if (study.Conditions != null)
            {
                foreach (var cond in study.Conditions)
                {
                    if (cond.Condition != null)
                        conditions.Add(cond.Condition);
                }
            }
        }

        var statuses = new Dictionary<string, int>();
        var phases = new Dictionary<string, int>();
        foreach (var study in studyList)
        {
            if (!string.IsNullOrEmpty(study.OverallStatus))
            {
                if (statuses.TryGetValue(study.OverallStatus, out var sc))
                    statuses[study.OverallStatus] = sc + 1;
                else
                    statuses[study.OverallStatus] = 1;
            }
            if (study.Phases != null)
            {
                foreach (var phase in study.Phases)
                {
                    if (!string.IsNullOrEmpty(phase.Phase))
                    {
                        if (phases.TryGetValue(phase.Phase, out var pc))
                            phases[phase.Phase] = pc + 1;
                        else
                            phases[phase.Phase] = 1;
                    }
                }
            }
        }

        return new
        {
            uuid = person.Id,
            name = person.FullName,
            orcid = person.Orcid,
            ncbiId = person.NcbiId,
            npi = person.Npi,
            paperCount = person.InvestigatorPapers?.Count ?? 0,
            primaryAffiliation = person.Affiliations?.FirstOrDefault(a => a.IsPrimary)?.InstitutionName,
            affiliations = person.Affiliations?.Select(a => new
            {
                institution = a.InstitutionName,
                department = a.Department,
                city = a.City,
                state = a.State,
                country = a.Country,
                role = a.Role,
                isPrimary = a.IsPrimary
            }).ToList(),
            studyCount = studyList.Count,
            coInvestigators = coInvestigators.OrderBy(x => x).ToList(),
            conditionsFocusAreas = conditions.OrderBy(x => x).ToList(),
            statistics = new
            {
                byStatus = statuses.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value),
                byPhase = phases.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
            }
        };
    }
}

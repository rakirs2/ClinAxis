using Scrapers.Persistence.Entities;

namespace DataApi;

internal static class StudyMapper
{
    internal static object ToSummary(StudyEntity s)
    {
        return new
        {
            nctId = s.NctId,
            briefTitle = s.BriefTitle,
            overallStatus = s.OverallStatus,
            studyType = s.StudyType,
            enrollmentCount = s.EnrollmentCount,
            startDate = s.StartDate,
            completionDate = s.CompletionDate,
            isIncomplete = s.IsIncomplete,
            investigatorCount = s.StudyInvestigators?.Count ?? s.Investigators?.Count ?? 0,
            pubmedPaperCount = s.PubmedStudies?.Count ?? 0,
            conditions = s.Conditions?.Select(c => c.Condition).ToList(),
            phases = s.Phases?.Select(p => p.Phase).ToList()
        };
    }

    internal static object ToDetail(StudyEntity s)
    {
        return new
        {
            nctId = s.NctId,
            briefTitle = s.BriefTitle,
            officialTitle = s.OfficialTitle,
            overallStatus = s.OverallStatus,
            studyType = s.StudyType,
            briefSummary = s.BriefSummary,
            primaryPurpose = s.PrimaryPurpose,
            interventionModel = s.InterventionModel,
            allocation = s.Allocation,
            enrollmentCount = s.EnrollmentCount,
            sex = s.Sex,
            minimumAge = s.MinimumAge,
            maximumAge = s.MaximumAge,
            startDate = s.StartDate,
            completionDate = s.CompletionDate,
            studyFirstPostDate = s.StudyFirstPostDate,
            isIncomplete = s.IsIncomplete,
            investigators = (s.StudyInvestigators?.Select(si => new
            {
                name = si.InvestigatorPerson?.FullName,
                role = si.RoleOnStudy,
                affiliation = si.InvestigatorPerson?.Affiliations?
                    .FirstOrDefault(a => a.IsPrimary)?.InstitutionName
            }).ToList()
                ?? s.Investigators?.Select(i => new
                {
                    name = i.Name,
                    role = i.Role,
                    affiliation = i.Affiliation
                }).ToList()),
            conditions = s.Conditions?.Select(c => c.Condition).ToList(),
            keywords = s.Keywords?.Select(k => k.Keyword).ToList(),
            phases = s.Phases?.Select(p => p.Phase).ToList(),
            pubmedPapers = s.PubmedStudies?.Select(p => new
            {
                pmid = p.Pmid,
                doi = p.Doi,
                title = p.Title,
                journal = p.Journal,
                publicationDate = p.PublicationDate,
                abstractText = p.Abstract,
                isNonEnglish = p.IsNonEnglish
            }).ToList()
        };
    }

    internal static object ToPipelineRun(PipelineRunEntity r)
    {
        return new
        {
            id = r.Id,
            startedAt = r.StartedAt,
            completedAt = r.CompletedAt,
            status = r.Status,
            totalStudies = r.TotalStudies,
            totalInvestigators = r.TotalInvestigators,
            totalPubmedPapers = r.TotalPubmedPapers,
            totalKeywords = r.TotalKeywords,
            totalAuthors = r.TotalAuthors,
            errorMessage = r.ErrorMessage
        };
    }
}
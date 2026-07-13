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
            pubmedPaperCount = s.StudyPapers?.Count ?? 0,
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
            masking = s.Masking,
            orgStudyId = s.OrgStudyId,
            leadSponsorName = s.LeadSponsorName,
            collaboratorNames = s.CollaboratorNames,
            eligibilityCriteria = s.EligibilityCriteria,
            healthyVolunteers = s.HealthyVolunteers,
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
            locations = s.Locations?.Select(l => new
            {
                facility = l.Facility,
                city = l.City,
                state = l.State,
                country = l.Country
            }).ToList(),
            references = s.References?.Select(r => new
            {
                pmid = r.Pmid,
                citation = r.Citation,
                type = r.Type
            }).ToList(),
            outcomes = s.Outcomes?.Select(o => new
            {
                outcomeType = o.OutcomeType,
                measure = o.Measure,
                description = o.Description,
                timeFrame = o.TimeFrame
            }).ToList(),
            armGroups = s.ArmGroups?.Select(a => new
            {
                label = a.Label,
                type = a.Type,
                description = a.Description
            }).ToList(),
            pubmedPapers = s.StudyPapers?.Select(sp => new
            {
                pmid = sp.PubmedPaper?.Pmid,
                doi = sp.PubmedPaper?.Doi,
                title = sp.PubmedPaper?.Title,
                journal = sp.PubmedPaper?.Journal,
                publicationDate = sp.PubmedPaper?.PublicationDate,
                abstractText = sp.PubmedPaper?.Abstract,
                isNonEnglish = sp.PubmedPaper?.IsNonEnglish ?? false
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
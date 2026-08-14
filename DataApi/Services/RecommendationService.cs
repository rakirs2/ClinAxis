using Scrapers.Persistence;
using Scrapers.Utilities;
using DataApi.Models;

namespace DataApi.Services;

internal class RecommendationService
{
    private readonly string _connectionString;

    public RecommendationService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<RecommendResponse> RecommendAsync(RecommendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var repo = new StudyRepository(_connectionString);
        var prefixes = request.GetAllPrefixes();

        var candidates = await repo.GetRecommendationCandidatesAsync(prefixes, request.TopN);
        var personIds = candidates.Select(c => c.Uuid).ToList();
        var studyRows = await repo.GetRecommendationStudyRowsAsync(personIds);
        var descriptorNames = await repo.GetDescriptorNamesForPrefixesAsync(prefixes);

        var requestedCategories = SpecialtyCategoryMapper.MapToCategories(string.Join(' ', descriptorNames));
        var recRequest = new BestPiScorer.RecommendationRequest(
            requestedCategories, request.Region, request.EnrollmentTarget, request.Population);

        var signals = RecCandidateAssembler.Assemble(
            candidates.Select(c => new RecCandidateAssembler.CandidateScalars(
                c.Uuid, c.Name, c.PrimaryAffiliation, c.StudyCount, c.HIndex, c.PaperCount ?? 0)),
            studyRows,
            DateOnly.FromDateTime(DateTime.UtcNow));

        var ranked = BestPiScorer.Rank(signals, recRequest);

        var response = new RecommendResponse
        {
            TotalCandidates = signals.Count,
            Request = new RecommendRequestSummary
            {
                TherapyTreePrefixes = request.TherapyTreePrefixes ?? [],
                ConditionTreePrefixes = request.ConditionTreePrefixes ?? [],
                Population = request.Population,
                Region = request.Region,
                EnrollmentTarget = request.EnrollmentTarget,
                Phase = request.Phase
            }
        };

        foreach (var r in ranked.Take(request.TopN))
        {
            response.Investigators.Add(new RecommendRank
            {
                Uuid = r.Candidate.Uuid,
                Name = r.Candidate.Name,
                PrimaryAffiliation = r.Candidate.PrimaryAffiliation,
                Score = Math.Round(r.Score, 3),
                Factors = new RecommendFactors
                {
                    Experience = Round(r.Factors.Experience),
                    Completion = Round(r.Factors.Completion),
                    ConditionFit = Round(r.Factors.ConditionFit),
                    Velocity = Round(r.Factors.Velocity),
                    Publication = Round(r.Factors.Publication),
                    Geographic = Round(r.Factors.Geographic)
                },
                Details = new RecommendDetails
                {
                    StudyCount = r.Candidate.StudyCount,
                    CompletionRate = r.Candidate.CompletionRate is null ? null : Math.Round(r.Candidate.CompletionRate.Value, 3),
                    EnrollmentVelocity = r.Candidate.EnrollmentVelocity is null ? null : Math.Round(r.Candidate.EnrollmentVelocity.Value, 3),
                    HIndex = r.Candidate.HIndex,
                    PaperCount = r.Candidate.PaperCount,
                    ConditionCategories = r.Candidate.ConditionCategories.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList(),
                    Regions = r.Candidate.Regions.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList()
                }
            });
        }

        return response;
    }

    private static double? Round(double? value)
    {
        return value is null ? null : Math.Round(value.Value, 3);
    }
}

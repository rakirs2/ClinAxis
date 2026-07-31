using System.Collections.ObjectModel;
using Scrapers.Persistence;
using DataApi.Models;

namespace DataApi.Services;

internal class InvestigatorFinderService
{
    private readonly string _connectionString;
    private readonly PiCompletionModel? _piCompletionModel;

    public InvestigatorFinderService(string connectionString, PiCompletionModel? piCompletionModel = null)
    {
        _connectionString = connectionString;
        _piCompletionModel = piCompletionModel;
    }

    public async Task<FinderResponse> FindAsync(FinderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var repo = new StudyRepository(_connectionString);

        var candidates = await repo.GetInvestigatorFinderCandidatesAsync(
            request.GetAllPrefixes(),
            topN: request.TopN);

        var ranked = candidates
            .Where(c => (!request.MinStudies.HasValue || c.StudyCount >= request.MinStudies.Value) &&
                        (!request.MinHIndex.HasValue || (c.HIndex ?? 0) >= request.MinHIndex.Value))
            .Select(c =>
            {
                var relevance = InvestigatorScorer.ComputeRelevance(c.StudyCount);
                var experience = InvestigatorScorer.ComputeExperience(c.StudyCount, c.CompletedStudies, c.EnrollmentTotal);
                var publication = InvestigatorScorer.ComputePublication(c.HIndex, c.PaperCount);
                var network = InvestigatorScorer.ComputeNetwork(0);
                var total = InvestigatorScorer.ComputeTotal(relevance, experience, publication, network);

                return new InvestigatorRank
                {
                    Uuid = c.Uuid,
                    Name = c.Name,
                    PrimaryAffiliation = c.PrimaryAffiliation,
                    Score = total,
                    ModelScore = _piCompletionModel?.Predict(c.StudyCount, c.CompletedStudies, c.EnrollmentTotal),
                    Factors = new ScoreFactors
                    {
                        Relevance = Math.Round(relevance, 2),
                        Experience = Math.Round(experience, 2),
                        Publication = Math.Round(publication, 2),
                        Network = Math.Round(network, 2),
                    },
                    Details = new InvestigatorDetails
                    {
                        StudyCount = c.StudyCount,
                        CompletedStudies = c.CompletedStudies,
                        EnrollmentTotal = c.EnrollmentTotal,
                        HIndex = c.HIndex,
                        PaperCount = c.PaperCount,
                    }
                };
            })
            .OrderByDescending(r => r.Score)
            .Take(request.TopN)
            .ToList();

        var response = new FinderResponse
        {
            TotalCandidates = candidates.Count,
        };
        foreach (var r in ranked) response.Investigators.Add(r);
        return response;
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Services.Enrichment;

public sealed class NpiBertDisagreementLogger
{
    private readonly BertNpiScorer? _scorer;
    private readonly string _connectionString;

    public NpiBertDisagreementLogger(BertNpiScorer? scorer, string connectionString)
    {
        _scorer = scorer;
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public bool CanScore => _scorer is { IsAvailable: true };

    public async Task LogDisagreementAsync(
        InvestigatorPersonEntity person,
        IReadOnlyList<NpiRegistryResult> candidates,
        IReadOnlyList<NpiRegistryResult> resolved,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(candidates);

        if (!CanScore || candidates.Count == 0)
            return;

        var investigatorContext = BuildInvestigatorContext(person);

        var candidateScores = new List<CandidateScore>();
        var investigatorEmb = _scorer!.ComputeEmbedding(investigatorContext);

        foreach (var candidate in candidates)
        {
            var candidateContext = BuildCandidateContext(candidate);
            var candidateEmb = _scorer.ComputeEmbedding(candidateContext);
            var score = BertNpiScorer.CosineSimilarity(investigatorEmb, candidateEmb);

            candidateScores.Add(new CandidateScore
            {
                Npi = candidate.Number ?? "",
                Organization = candidate.Basic?.OrganizationName,
                State = candidate.Addresses is { Count: > 0 } ? candidate.Addresses[0].State : null,
                Score = Math.Round(score, 4)
            });
        }

        candidateScores.Sort((a, b) => b.Score.CompareTo(a.Score));
        var topCandidate = candidateScores[0];

        var bertDecision = DetermineBertDecision(topCandidate, candidateScores);

        var ruleBasedResult = person.NpiEnrichmentResult ?? "pending";
        var ruleBasedNpi = person.Npi;
        var disagreementType = ClassifyDisagreement(
            ruleBasedResult, ruleBasedNpi,
            bertDecision, topCandidate.Npi);

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString).Options);

        context.PipelineModelDisagreements.Add(new PipelineModelDisagreementEntity
        {
            PersonId = person.Id,
            RuleBasedResult = ruleBasedResult,
            RuleBasedNpi = ruleBasedNpi,
            BertTopCandidate = topCandidate.Npi,
            BertTopScore = topCandidate.Score,
            BertRecommendedResult = bertDecision,
            DisagreementType = disagreementType,
            InvestigatorContext = investigatorContext,
            CandidatesJson = JsonSerializer.Serialize(candidateScores),
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static string BuildInvestigatorContext(InvestigatorPersonEntity person)
    {
        var parts = new List<string> { person.FullName };
        if (!string.IsNullOrWhiteSpace(person.Orcid))
            parts.Add($"ORCID:{person.Orcid}");
        return string.Join(" | ", parts);
    }

    private static string BuildCandidateContext(NpiRegistryResult candidate)
    {
        var parts = new List<string>();

        if (candidate.Basic != null)
        {
            var name = $"{candidate.Basic.FirstName ?? ""} {candidate.Basic.LastName ?? ""}".Trim();
            if (!string.IsNullOrWhiteSpace(name))
                parts.Add(name);
            if (!string.IsNullOrWhiteSpace(candidate.Basic.OrganizationName))
                parts.Add(candidate.Basic.OrganizationName);
        }

        if (candidate.Addresses is { Count: > 0 })
        {
            var addr = candidate.Addresses[0];
            var locParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(addr.City))
                locParts.Add(addr.City);
            if (!string.IsNullOrWhiteSpace(addr.State))
                locParts.Add(addr.State);
            if (locParts.Count > 0)
                parts.Add(string.Join(", ", locParts));
        }

        if (candidate.Taxonomies is { Count: > 0 })
        {
            var descs = candidate.Taxonomies
                .Where(t => !string.IsNullOrWhiteSpace(t.Desc))
                .Select(t => t.Desc!);
            parts.AddRange(descs);
        }

        return string.Join(" | ", parts);
    }

    private static string DetermineBertDecision(CandidateScore top, List<CandidateScore> all)
    {
        if (top.Score < 0.3)
            return "not_found";

        int countAboveThreshold = 0;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Score >= 0.3)
                countAboveThreshold++;
        }

        if (countAboveThreshold > 1)
            return "ambiguous";
        return "assigned";
    }

    private static string ClassifyDisagreement(
        string ruleResult, string? ruleNpi,
        string bertDecision, string bertNpi)
    {
        if (ruleResult == bertDecision && ruleNpi == bertNpi)
            return "match";

        if (ruleResult == "ambiguous" && bertDecision == "assigned")
            return "rule_ambiguous_bert_resolved";

        if (ruleResult == "not_found" && bertDecision == "assigned")
            return "rule_not_found_bert_found";

        if (ruleResult == "assigned" && ruleNpi != bertNpi)
            return "rule_assigned_bert_disagrees";

        if (ruleResult == "assigned" && bertDecision == "ambiguous")
            return "rule_assigned_bert_ambiguous";

        return $"mismatch_{ruleResult}_vs_{bertDecision}";
    }

    private sealed class CandidateScore
    {
        public string Npi { get; set; } = "";
        public string? Organization { get; set; }
        public string? State { get; set; }
        public double Score { get; set; }
    }
}

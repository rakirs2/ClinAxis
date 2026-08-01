using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class NpiCandidateScorerTests
{
    private static readonly PersonSignalProfile Profile = NpiFeatureExtractor.BuildProfile(
        "John A Smith",
        orcid: "0000-0001-2345-6789",
        institutionName: "Mayo Clinic",
        department: "Cardiology",
        city: "Rochester",
        state: "MN",
        meshDescriptorNames: ["Coronary Artery Disease"]);

    [TestMethod]
    public void Score_AllSignalsMatching_ScoresOne()
    {
        var features = Feature("1", first: "John", last: "Smith", middle: "Alan", credential: "MD",
            state: "MN", city: "Rochester", org: "Mayo Clinic", taxonomyDesc: "Cardiovascular Disease");

        Assert.AreEqual(1.0, NpiCandidateScorer.Score(features), 1e-9);
    }

    [TestMethod]
    public void Score_DeactivatedCandidate_Zero()
    {
        var features = Feature("1", first: "John", last: "Smith", status: "D");

        Assert.AreEqual(0.0, NpiCandidateScorer.Score(features));
    }

    [TestMethod]
    public void Score_NameOnlyMatch_DoesNotReachFullScore()
    {
        var features = Feature("1", first: "John", last: "Smith");

        Assert.AreEqual(1.0, NpiCandidateScorer.Score(features), 1e-9,
            "Name-only match scores 1.0 — missing signals abstain rather than penalize");
    }

    [TestMethod]
    public void Score_NameMismatch_Zero()
    {
        var features = Feature("1", first: "James", last: "Smith");

        Assert.AreEqual(0.0, NpiCandidateScorer.Score(features));
    }

    [TestMethod]
    public void Resolve_NoCandidates_NotFound()
    {
        var resolution = NpiCandidateScorer.Resolve([]);

        Assert.AreEqual(NpiCandidateScorer.NpiResolutionOutcome.NotFound, resolution.Outcome);
    }

    [TestMethod]
    public void Resolve_SingleMatchingCandidate_Assigned()
    {
        var features = Feature("1", first: "John", last: "Smith");
        var resolution = NpiCandidateScorer.Resolve([features]);

        Assert.AreEqual(NpiCandidateScorer.NpiResolutionOutcome.Assigned, resolution.Outcome);
        Assert.AreEqual("1", resolution.AssignedNumber);
        Assert.IsNotNull(features.Score);
    }

    [TestMethod]
    public void Resolve_SingleMismatchingCandidate_Ambiguous()
    {
        var resolution = NpiCandidateScorer.Resolve([Feature("1", first: "James", last: "Smith")]);

        Assert.AreEqual(NpiCandidateScorer.NpiResolutionOutcome.Ambiguous, resolution.Outcome);
    }

    [TestMethod]
    public void Resolve_MultipleCandidates_BestWithCorroboration_Assigned()
    {
        var maya = Feature("1", first: "John", last: "Smith", state: "MN");
        var cleveland = Feature("2", first: "John", last: "Smith", state: "OH");
        var resolution = NpiCandidateScorer.Resolve([maya, cleveland]);

        Assert.AreEqual(NpiCandidateScorer.NpiResolutionOutcome.Assigned, resolution.Outcome);
        Assert.AreEqual("1", resolution.AssignedNumber);
    }

    [TestMethod]
    public void Resolve_MultipleCandidates_NoCorroboration_Ambiguous()
    {
        var first = Feature("1", first: "John", last: "Smith");
        var second = Feature("2", first: "John", last: "Smith");
        var resolution = NpiCandidateScorer.Resolve([first, second]);

        Assert.AreEqual(NpiCandidateScorer.NpiResolutionOutcome.Ambiguous, resolution.Outcome);
    }

    [TestMethod]
    public void Resolve_MultipleCandidates_TiedScores_Ambiguous()
    {
        var first = Feature("1", first: "John", last: "Smith", state: "MN");
        var second = Feature("2", first: "John", last: "Smith", state: "MN");
        var resolution = NpiCandidateScorer.Resolve([first, second]);

        Assert.AreEqual(NpiCandidateScorer.NpiResolutionOutcome.Ambiguous, resolution.Outcome);
    }

    [TestMethod]
    public void Resolve_OrcidMatch_OverridesLackOfCorroboration()
    {
        var orcidCandidate = Feature("1", first: "John", last: "Smith", orcidIdentifier: "0000-0001-2345-6789");
        var other = Feature("2", first: "John", last: "Smith");
        var resolution = NpiCandidateScorer.Resolve([orcidCandidate, other]);

        Assert.AreEqual(NpiCandidateScorer.NpiResolutionOutcome.Assigned, resolution.Outcome);
        Assert.AreEqual("1", resolution.AssignedNumber);
        Assert.AreEqual(1.0, resolution.AssignedScore);
    }

    [TestMethod]
    public void Resolve_OrcidMatch_DeactivatedCandidate_NoOverride()
    {
        var deactivatedOrcid = Feature("1", first: "John", last: "Smith", orcidIdentifier: "0000-0001-2345-6789", status: "D");
        var other = Feature("2", first: "John", last: "Smith");
        var third = Feature("3", first: "John", last: "Smith");
        var resolution = NpiCandidateScorer.Resolve([deactivatedOrcid, other, third]);

        Assert.AreEqual(NpiCandidateScorer.NpiResolutionOutcome.Ambiguous, resolution.Outcome);
    }

    [TestMethod]
    public void Resolve_AllDeactivated_Ambiguous()
    {
        var resolution = NpiCandidateScorer.Resolve(
        [
            Feature("1", first: "John", last: "Smith", status: "D"),
            Feature("2", first: "John", last: "Smith", status: "D")
        ]);

        Assert.AreEqual(NpiCandidateScorer.NpiResolutionOutcome.Ambiguous, resolution.Outcome);
    }

    [TestMethod]
    public void Resolve_OneActiveOneDeactivated_SingleThresholdApplies()
    {
        var active = Feature("1", first: "John", last: "Smith");
        var deactivated = Feature("2", first: "John", last: "Smith", status: "D");
        var resolution = NpiCandidateScorer.Resolve([active, deactivated]);

        Assert.AreEqual(NpiCandidateScorer.NpiResolutionOutcome.Assigned, resolution.Outcome);
        Assert.AreEqual("1", resolution.AssignedNumber);
    }

    [TestMethod]
    public void Resolve_ScoresSetOnAllCandidates()
    {
        var first = Feature("1", first: "John", last: "Smith");
        var second = Feature("2", first: "James", last: "Smith");
        NpiCandidateScorer.Resolve([first, second]);

        Assert.IsNotNull(first.Score);
        Assert.IsNotNull(second.Score);
    }

    [TestMethod]
    public void Resolve_SpecialtyCorroborates()
    {
        var specialtyMatch = Feature("1", first: "John", last: "Smith", taxonomyDesc: "Cardiovascular Disease");
        var other = Feature("2", first: "John", last: "Smith", state: "OH");
        var resolution = NpiCandidateScorer.Resolve([specialtyMatch, other]);

        Assert.AreEqual(NpiCandidateScorer.NpiResolutionOutcome.Assigned, resolution.Outcome);
        Assert.AreEqual("1", resolution.AssignedNumber);
    }

    private static NpiCandidateFeatures Feature(
        string number,
        string? first,
        string? last,
        string? middle = null,
        string? credential = null,
        string? state = null,
        string? city = null,
        string? org = null,
        string? taxonomyDesc = null,
        string? orcidIdentifier = null,
        string status = "A") =>
        NpiFeatureExtractor.Extract(Profile, new Scrapers.Services.Enrichment.NpiRegistryResult
        {
            Number = number,
            Status = status,
            Basic = new Scrapers.Services.Enrichment.NpiBasic
            {
                FirstName = first,
                LastName = last,
                MiddleName = middle,
                Credential = credential,
                OrganizationName = org
            },
            Addresses = state != null || city != null
                ? [new Scrapers.Services.Enrichment.NpiAddress { AddressPurpose = "LOCATION", City = city, State = state }]
                : null,
            Taxonomies = taxonomyDesc != null
                ? [new Scrapers.Services.Enrichment.NpiTaxonomy { Desc = taxonomyDesc, Primary = true }]
                : null,
            Identifiers = orcidIdentifier != null
                ? [new Scrapers.Services.Enrichment.NpiIdentifier { Identifier = orcidIdentifier, IdentifierType = "17" }]
                : null
        });
}

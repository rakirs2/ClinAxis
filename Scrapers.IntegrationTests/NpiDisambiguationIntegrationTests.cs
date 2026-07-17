using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class NpiDisambiguationIntegrationTests : DbTestBase
{
    private StudyRepository _repo = null!;

    [TestInitialize]
    public void TestInit()
    {
        _repo = new StudyRepository(ConnectionString);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PersonIdentifierCandidates_PersistsAndQueriesCorrectly()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "John Smith",
            IsHuman = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        Context.PersonIdentifierCandidates.AddRange(
            new PersonIdentifierCandidateEntity
            {
                PersonId = person.Id,
                IdentifierType = "NPI",
                IdentifierValue = "1234567890",
                SourceName = "NPPES",
                MatchedFullName = "John Smith",
                MatchedAffiliation = "Mayo Clinic",
                MatchedState = "MN",
                SourceStatus = "A",
                IsAutoApproved = false,
                CreatedAt = DateTime.UtcNow
            },
            new PersonIdentifierCandidateEntity
            {
                PersonId = person.Id,
                IdentifierType = "NPI",
                IdentifierValue = "9876543210",
                SourceName = "NPPES",
                MatchedFullName = "John Smith",
                MatchedAffiliation = "Cleveland Clinic",
                MatchedState = "OH",
                SourceStatus = "A",
                IsAutoApproved = false,
                CreatedAt = DateTime.UtcNow
            });

        await Context.SaveChangesAsync();

        var candidates = await Context.PersonIdentifierCandidates
            .Where(c => c.PersonId == person.Id)
            .OrderBy(c => c.IdentifierValue)
            .ToListAsync();

        Assert.AreEqual(2, candidates.Count);
        Assert.AreEqual("1234567890", candidates[0].IdentifierValue);
        Assert.AreEqual("9876543210", candidates[1].IdentifierValue);
        Assert.IsFalse(candidates[0].IsAutoApproved);
        Assert.IsFalse(candidates[1].IsAutoApproved);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PersonIdentifierCandidates_StateFilter_SelectsCorrectCandidate()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Jane Doe",
            IsHuman = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var affilState = "MN";

        Context.PersonIdentifierCandidates.AddRange(
            new PersonIdentifierCandidateEntity
            {
                PersonId = person.Id,
                IdentifierType = "NPI",
                IdentifierValue = "1111111111",
                SourceName = "NPPES",
                MatchedFullName = "Jane Doe",
                MatchedAffiliation = "Mayo Clinic",
                MatchedState = "MN",
                SourceStatus = "A",
                IsAutoApproved = false,
                CreatedAt = DateTime.UtcNow
            },
            new PersonIdentifierCandidateEntity
            {
                PersonId = person.Id,
                IdentifierType = "NPI",
                IdentifierValue = "2222222222",
                SourceName = "NPPES",
                MatchedFullName = "Jane Doe",
                MatchedAffiliation = "Cleveland Clinic",
                MatchedState = "OH",
                SourceStatus = "A",
                IsAutoApproved = false,
                CreatedAt = DateTime.UtcNow
            },
            new PersonIdentifierCandidateEntity
            {
                PersonId = person.Id,
                IdentifierType = "NPI",
                IdentifierValue = "3333333333",
                SourceName = "NPPES",
                MatchedFullName = "Jane Doe",
                MatchedAffiliation = "UCSF Medical Center",
                MatchedState = "CA",
                SourceStatus = "A",
                IsAutoApproved = false,
                CreatedAt = DateTime.UtcNow
            });

        await Context.SaveChangesAsync();

        var candidates = await Context.PersonIdentifierCandidates
            .Where(c => c.PersonId == person.Id)
            .ToListAsync();

        Assert.AreEqual(3, candidates.Count);

        var stateMatch = candidates
            .Where(c => string.Equals(c.MatchedState, affilState, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.AreEqual(1, stateMatch.Count, "Only 1 candidate should match the state");
        Assert.AreEqual("1111111111", stateMatch[0].IdentifierValue);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PersonIdentifierCandidates_AutoApproval_MarksCandidate()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Alice Johnson",
            IsHuman = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var candidate = new PersonIdentifierCandidateEntity
        {
            PersonId = person.Id,
            IdentifierType = "NPI",
            IdentifierValue = "5555555555",
            SourceName = "NPPES",
            MatchedFullName = "Alice Johnson",
            MatchedAffiliation = "Boston Medical",
            MatchedState = "MA",
            SourceStatus = "A",
            IsAutoApproved = true,
            CreatedAt = DateTime.UtcNow
        };
        Context.PersonIdentifierCandidates.Add(candidate);
        await Context.SaveChangesAsync();

        var loaded = await Context.PersonIdentifierCandidates
            .Where(c => c.PersonId == person.Id)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(loaded);
        Assert.IsTrue(loaded!.IsAutoApproved);
        Assert.AreEqual("5555555555", loaded.IdentifierValue);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task NpiEnrichmentResult_ErrorStatus_PersistsCorrectly()
    {
        var person = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Bob Wilson",
            IsHuman = true,
            NpiEnrichmentResult = "error",
            NpiLookupAttemptedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Context.InvestigatorPersons.Add(person);
        await Context.SaveChangesAsync();

        var loaded = await Context.InvestigatorPersons
            .FirstOrDefaultAsync(p => p.Id == person.Id);

        Assert.IsNotNull(loaded);
        Assert.AreEqual("error", loaded!.NpiEnrichmentResult);
        Assert.IsNotNull(loaded.NpiLookupAttemptedAt);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CountInvestigatorsByEnrichmentResultAsync_ReturnsCorrectCount()
    {
        var person1 = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Person One",
            IsHuman = true,
            NpiEnrichmentResult = "assigned",
            Npi = "1111111111",
            NpiLookupAttemptedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var person2 = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Person Two",
            IsHuman = true,
            NpiEnrichmentResult = "ambiguous",
            NpiLookupAttemptedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var person3 = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Person Three",
            IsHuman = true,
            NpiEnrichmentResult = "error",
            NpiLookupAttemptedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Context.InvestigatorPersons.AddRange(person1, person2, person3);
        await Context.SaveChangesAsync();

        var assigned = await _repo.CountInvestigatorsByEnrichmentResultAsync("assigned");
        var ambiguous = await _repo.CountInvestigatorsByEnrichmentResultAsync("ambiguous");
        var error = await _repo.CountInvestigatorsByEnrichmentResultAsync("error");
        var notFound = await _repo.CountInvestigatorsByEnrichmentResultAsync("not_found");

        Assert.AreEqual(1, assigned);
        Assert.AreEqual(1, ambiguous);
        Assert.AreEqual(1, error);
        Assert.AreEqual(0, notFound);
    }
}

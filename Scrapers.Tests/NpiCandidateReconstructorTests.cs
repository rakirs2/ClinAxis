using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;
using Scrapers.Services.Enrichment;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class NpiCandidateReconstructorTests
{
    private static readonly PersonSignalProfile SmithProfile = NpiFeatureExtractor.BuildProfile(
        "Dr. John A Smith, MD",
        orcid: "0000-0001-2345-6789",
        institutionName: "Mayo Clinic",
        department: "Cardiology",
        city: "Rochester",
        state: "MN",
        meshDescriptorNames: ["Coronary Artery Disease"]);

    [TestMethod]
    public void Reconstruct_RoundTripsAllPersistedFields_FeaturesMatchOriginal()
    {
        var original = new NpiRegistryResult
        {
            Number = "1234567890",
            Status = "A",
            DeactivationDate = null,
            Basic = new NpiBasic
            {
                FirstName = "John",
                LastName = "Smith",
                MiddleName = "A",
                Credential = "MD",
                NamePrefix = "Dr.",
                Gender = "M",
                OrganizationName = "Mayo Clinic",
                OtherNames = [new NpiOtherName { LastName = "Smith", FirstName = "John" }]
            },
            Addresses = [new NpiAddress { AddressPurpose = "LOCATION", City = "Rochester", State = "MN" }],
            Taxonomies = [new NpiTaxonomy { Desc = "Cardiovascular Disease", State = "MN", License = "12345", Primary = true }],
            Identifiers = [new NpiIdentifier { Identifier = "0000-0001-2345-6789", IdentifierType = "17" }]
        };

        var entity = ToEntity(original);
        var reconstructed = NpiCandidateReconstructor.Reconstruct(entity);

        var expected = NpiFeatureExtractor.Extract(SmithProfile, original);
        var actual = NpiFeatureExtractor.Extract(SmithProfile, reconstructed);

        AssertFeaturesEqual(expected, actual);
    }

    [TestMethod]
    public void Reconstruct_MinimalEntity_OnlyNameFields()
    {
        var entity = new PersonIdentifierCandidateEntity
        {
            PersonId = Guid.NewGuid(),
            IdentifierType = "NPI",
            IdentifierValue = "1111111111",
            SourceName = "NPPES",
            MatchedFullName = "John Smith",
            SourceStatus = "A"
        };

        var result = NpiCandidateReconstructor.Reconstruct(entity);

        Assert.AreEqual("1111111111", result.Number);
        Assert.AreEqual("John", result.Basic!.FirstName);
        Assert.AreEqual("Smith", result.Basic!.LastName);
        Assert.IsNull(result.Addresses);
        Assert.IsNull(result.Taxonomies);
        Assert.IsNull(result.Identifiers);
        Assert.IsNull(result.DeactivationDate);
    }

    [TestMethod]
    public void Reconstruct_SingleTokenName_TreatedAsLastName()
    {
        var entity = new PersonIdentifierCandidateEntity
        {
            PersonId = Guid.NewGuid(),
            IdentifierType = "NPI",
            IdentifierValue = "1",
            SourceName = "NPPES",
            MatchedFullName = "Madonna",
            SourceStatus = "A"
        };

        var result = NpiCandidateReconstructor.Reconstruct(entity);

        Assert.IsNull(result.Basic!.FirstName);
        Assert.AreEqual("Madonna", result.Basic.LastName);
    }

    [TestMethod]
    public void Reconstruct_DeactivatedStatus_IsDeactivatedTrue()
    {
        var entity = new PersonIdentifierCandidateEntity
        {
            PersonId = Guid.NewGuid(),
            IdentifierType = "NPI",
            IdentifierValue = "1",
            SourceName = "NPPES",
            MatchedFullName = "John Smith",
            SourceStatus = "D",
            SourceDeactivatedAt = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = NpiCandidateReconstructor.Reconstruct(entity);
        var features = NpiFeatureExtractor.Extract(NpiFeatureExtractor.BuildProfile("John Smith", null, null, null, null, null, []), result);

        Assert.AreEqual("D", result.Status);
        Assert.AreEqual(entity.SourceDeactivatedAt, result.DeactivationDate);
        Assert.IsTrue(features.IsDeactivated);
    }

    /// <summary>
    /// Builds the persisted row exactly the way InvestigatorEnrichmentService does
    /// (extract features, then copy every field onto the entity).
    /// </summary>
    private static PersonIdentifierCandidateEntity ToEntity(NpiRegistryResult result)
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, result);
        return new PersonIdentifierCandidateEntity
        {
            PersonId = Guid.NewGuid(),
            IdentifierType = "NPI",
            IdentifierValue = features.Number,
            SourceName = "NPPES",
            MatchedFullName = features.MatchedFullName,
            MatchedAffiliation = features.MatchedAffiliation,
            MatchedState = features.MatchedState,
            MatchedCity = features.MatchedCity,
            MatchedMiddleName = features.MatchedMiddleName,
            MatchedCredential = features.MatchedCredential,
            MatchedNamePrefix = features.MatchedNamePrefix,
            MatchedGender = features.MatchedGender,
            MatchedTaxonomyDesc = features.MatchedTaxonomyDesc,
            MatchedTaxonomyState = features.MatchedTaxonomyState,
            MatchedTaxonomyLicense = features.MatchedTaxonomyLicense,
            MatchedOtherNamesJson = features.MatchedOtherNamesJson,
            MatchedIdentifiersJson = features.MatchedIdentifiersJson,
            SourceStatus = result.Status,
            SourceDeactivatedAt = result.DeactivationDate,
            IsAutoApproved = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static void AssertFeaturesEqual(NpiCandidateFeatures expected, NpiCandidateFeatures actual)
    {
        Assert.AreEqual(expected.Number, actual.Number);
        Assert.AreEqual(expected.MatchedFullName, actual.MatchedFullName);
        Assert.AreEqual(expected.MatchedAffiliation, actual.MatchedAffiliation);
        Assert.AreEqual(expected.MatchedState, actual.MatchedState);
        Assert.AreEqual(expected.MatchedCity, actual.MatchedCity);
        Assert.AreEqual(expected.MatchedMiddleName, actual.MatchedMiddleName);
        Assert.AreEqual(expected.MatchedCredential, actual.MatchedCredential);
        Assert.AreEqual(expected.MatchedNamePrefix, actual.MatchedNamePrefix);
        Assert.AreEqual(expected.MatchedGender, actual.MatchedGender);
        Assert.AreEqual(expected.MatchedTaxonomyDesc, actual.MatchedTaxonomyDesc);
        Assert.AreEqual(expected.MatchedTaxonomyState, actual.MatchedTaxonomyState);
        Assert.AreEqual(expected.MatchedTaxonomyLicense, actual.MatchedTaxonomyLicense);
        Assert.AreEqual(expected.MatchedOtherNamesJson, actual.MatchedOtherNamesJson);
        Assert.AreEqual(expected.MatchedIdentifiersJson, actual.MatchedIdentifiersJson);
        Assert.AreEqual(expected.HasPersonName, actual.HasPersonName);
        Assert.AreEqual(expected.ExactNameMatch, actual.ExactNameMatch);
        Assert.AreEqual(expected.MiddleNameMatch, actual.MiddleNameMatch);
        Assert.AreEqual(expected.CredentialMatch, actual.CredentialMatch);
        Assert.AreEqual(expected.StateMatch, actual.StateMatch);
        Assert.AreEqual(expected.CityMatch, actual.CityMatch);
        Assert.AreEqual(expected.OrgMatch, actual.OrgMatch);
        Assert.AreEqual(expected.OtherNameMatch, actual.OtherNameMatch);
        Assert.AreEqual(expected.SpecialtyMatch, actual.SpecialtyMatch);
        Assert.AreEqual(expected.LicenseStateMatch, actual.LicenseStateMatch);
        Assert.AreEqual(expected.DepartmentMatch, actual.DepartmentMatch);
        Assert.AreEqual(expected.OrcidMatch, actual.OrcidMatch);
        Assert.AreEqual(expected.IsDeactivated, actual.IsDeactivated);
    }
}

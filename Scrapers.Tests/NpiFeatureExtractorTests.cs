using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services.Enrichment;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class NpiFeatureExtractorTests
{
    private static readonly PersonSignalProfile SmithProfile = NpiFeatureExtractor.BuildProfile(
        "John A Smith",
        orcid: "0000-0001-2345-6789",
        institutionName: "Mayo Clinic",
        department: "Cardiology",
        city: "Rochester",
        state: "MN",
        meshDescriptorNames: ["Coronary Artery Disease", "Heart Failure"]);

    [TestMethod]
    public void BuildProfile_ParsesFirstMiddleLast()
    {
        var profile = NpiFeatureExtractor.BuildProfile("John A Smith", null, null, null, null, null, []);

        Assert.AreEqual("John", profile.FirstName);
        Assert.AreEqual("A", profile.MiddleName);
        Assert.AreEqual("Smith", profile.LastName);
    }

    [TestMethod]
    public void BuildProfile_TwoPartName_HasNoMiddle()
    {
        var profile = NpiFeatureExtractor.BuildProfile("Jane Doe", null, null, null, null, null, []);

        Assert.AreEqual("Jane", profile.FirstName);
        Assert.IsNull(profile.MiddleName);
        Assert.AreEqual("Doe", profile.LastName);
    }

    [TestMethod]
    public void BuildProfile_PrefixAndSuffix_AreStrippedAndCaptured()
    {
        var profile = NpiFeatureExtractor.BuildProfile("Dr. John A Smith, MD, PhD", null, null, null, null, null, []);

        Assert.AreEqual("John", profile.FirstName);
        Assert.AreEqual("Smith", profile.LastName);
        Assert.IsNotNull(profile.Suffix);
        StringAssert.Contains(profile.Suffix!, "MD", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void BuildProfile_MeshNames_DeriveSpecialtyCategories()
    {
        var profile = NpiFeatureExtractor.BuildProfile(
            "John Smith", null, null, null, null, null,
            ["Breast Neoplasms", "Lung Cancer"]);

        CollectionAssert.Contains(profile.SpecialtyCategories.ToList(), "oncology");
    }

    [TestMethod]
    public void Extract_ExactNameMatch_True()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith"));

        Assert.IsTrue(features.HasPersonName);
        Assert.IsTrue(features.ExactNameMatch);
    }

    [TestMethod]
    public void Extract_NameMismatch_False()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "James", last: "Smith"));

        Assert.IsTrue(features.HasPersonName);
        Assert.IsFalse(features.ExactNameMatch);
    }

    [TestMethod]
    public void Extract_OrgOnlyCandidate_NoPersonName()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: null, last: null, org: "Mayo Clinic"));

        Assert.IsFalse(features.HasPersonName);
        Assert.IsFalse(features.ExactNameMatch);
    }

    [TestMethod]
    public void Extract_MiddleName_MatchesInitialToFullName()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith", middle: "Alan"));

        Assert.AreEqual(true, features.MiddleNameMatch);
    }

    [TestMethod]
    public void Extract_MiddleName_Mismatch_False()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith", middle: "Robert"));

        Assert.AreEqual(false, features.MiddleNameMatch);
    }

    [TestMethod]
    public void Extract_MiddleName_MissingOnCandidate_Abstains()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith"));

        Assert.IsNull(features.MiddleNameMatch);
    }

    [TestMethod]
    public void Extract_Credential_NormalizesDotsAndCase()
    {
        var profile = NpiFeatureExtractor.BuildProfile("John Smith, MD", null, null, null, null, null, []);
        var features = NpiFeatureExtractor.Extract(profile, Candidate(number: "1", first: "John", last: "Smith", credential: "M.D."));

        Assert.AreEqual(true, features.CredentialMatch);
    }

    [TestMethod]
    public void Extract_Credential_NoPersonSuffix_Abstains()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith", credential: "M.D."));

        Assert.IsNull(features.CredentialMatch);
    }

    [TestMethod]
    public void Extract_State_MatchAndMismatch()
    {
        var matching = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith", state: "MN"));
        Assert.AreEqual(true, matching.StateMatch);

        var mismatching = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith", state: "OH"));
        Assert.AreEqual(false, mismatching.StateMatch);
    }

    [TestMethod]
    public void Extract_State_NoPersonState_Abstains()
    {
        var profile = NpiFeatureExtractor.BuildProfile("John Smith", null, null, null, null, null, []);
        var features = NpiFeatureExtractor.Extract(profile, Candidate(number: "1", first: "John", last: "Smith", state: "MN"));

        Assert.IsNull(features.StateMatch);
    }

    [TestMethod]
    public void Extract_City_Match()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith", city: "Rochester"));

        Assert.AreEqual(true, features.CityMatch);
    }

    [TestMethod]
    public void Extract_Org_SubstringMatch()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith", org: "Mayo Clinic Rochester"));

        Assert.AreEqual(true, features.OrgMatch);
    }

    [TestMethod]
    public void Extract_Org_NoPersonInstitution_Abstains()
    {
        var profile = NpiFeatureExtractor.BuildProfile("John Smith", null, null, null, null, null, []);
        var features = NpiFeatureExtractor.Extract(profile, Candidate(number: "1", first: "John", last: "Smith", org: "Mayo Clinic"));

        Assert.IsNull(features.OrgMatch);
    }

    [TestMethod]
    public void Extract_OtherName_LastNameMatch()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith",
            otherNames: [new NpiOtherName { LastName = "Smith", FirstName = "Johnathan" }]));

        Assert.AreEqual(true, features.OtherNameMatch);
    }

    [TestMethod]
    public void Extract_OtherName_NoOtherNames_Abstains()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith"));

        Assert.IsNull(features.OtherNameMatch);
    }

    [TestMethod]
    public void Extract_Specialty_MatchesTaxonomyDescription()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith",
            taxonomyDesc: "Cardiovascular Disease"));

        Assert.AreEqual(true, features.SpecialtyMatch);
    }

    [TestMethod]
    public void Extract_Specialty_NoPersonCategories_Abstains()
    {
        var profile = NpiFeatureExtractor.BuildProfile("John Smith", null, null, null, null, null, []);
        var features = NpiFeatureExtractor.Extract(profile, Candidate(number: "1", first: "John", last: "Smith",
            taxonomyDesc: "Cardiovascular Disease"));

        Assert.IsNull(features.SpecialtyMatch);
    }

    [TestMethod]
    public void Extract_LicenseState_Match()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith",
            taxonomyState: "MN", taxonomyLicense: "12345"));

        Assert.AreEqual(true, features.LicenseStateMatch);
        Assert.AreEqual("12345", features.MatchedTaxonomyLicense);
    }

    [TestMethod]
    public void Extract_Department_MatchesTaxonomy()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith",
            taxonomyDesc: "Interventional Cardiology"));

        Assert.AreEqual(true, features.DepartmentMatch);
    }

    [TestMethod]
    public void Extract_Orcid_MatchViaIdentifierType17()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith",
            orcidIdentifier: "0000-0001-2345-6789"));

        Assert.IsTrue(features.OrcidMatch);
    }

    [TestMethod]
    public void Extract_Orcid_NoPersonOrcid_NotMatched()
    {
        var profile = NpiFeatureExtractor.BuildProfile("John Smith", null, null, null, null, null, []);
        var features = NpiFeatureExtractor.Extract(profile, Candidate(number: "1", first: "John", last: "Smith",
            orcidIdentifier: "0000-0001-2345-6789"));

        Assert.IsFalse(features.OrcidMatch);
    }

    [TestMethod]
    public void Extract_DeactivatedStatus_Flagged()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith", status: "D"));

        Assert.IsTrue(features.IsDeactivated);
    }

    [TestMethod]
    public void Extract_PersistedFields_Carried()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith",
            middle: "Alan", credential: "MD", state: "MN", city: "Rochester", org: "Mayo Clinic",
            taxonomyDesc: "Cardiovascular Disease"));

        Assert.AreEqual("1", features.Number);
        Assert.AreEqual("John Smith", features.MatchedFullName);
        Assert.AreEqual("Mayo Clinic", features.MatchedAffiliation);
        Assert.AreEqual("MN", features.MatchedState);
        Assert.AreEqual("Rochester", features.MatchedCity);
        Assert.AreEqual("Alan", features.MatchedMiddleName);
        Assert.AreEqual("MD", features.MatchedCredential);
        Assert.AreEqual("Cardiovascular Disease", features.MatchedTaxonomyDesc);
    }

    [TestMethod]
    public void Extract_IdentifiersAndOtherNames_Serialized()
    {
        var features = NpiFeatureExtractor.Extract(SmithProfile, Candidate(number: "1", first: "John", last: "Smith",
            orcidIdentifier: "0000-0001-2345-6789",
            otherNames: [new NpiOtherName { LastName = "Smith" }]));

        Assert.IsNotNull(features.MatchedIdentifiersJson);
        StringAssert.Contains(features.MatchedIdentifiersJson!, "0000-0001-2345-6789", StringComparison.Ordinal);
        Assert.IsNotNull(features.MatchedOtherNamesJson);
        StringAssert.Contains(features.MatchedOtherNamesJson!, "Smith", StringComparison.Ordinal);
    }

    private static NpiRegistryResult Candidate(
        string number,
        string? first,
        string? last,
        string? middle = null,
        string? credential = null,
        string? state = null,
        string? city = null,
        string? org = null,
        string? taxonomyDesc = null,
        string? taxonomyState = null,
        string? taxonomyLicense = null,
        string? orcidIdentifier = null,
        string status = "A",
        List<NpiOtherName>? otherNames = null)
    {
        var result = new NpiRegistryResult
        {
            Number = number,
            Status = status,
            Basic = new NpiBasic
            {
                FirstName = first,
                LastName = last,
                MiddleName = middle,
                Credential = credential,
                OrganizationName = org,
                OtherNames = otherNames
            }
        };

        if (state != null || city != null)
        {
            result.Addresses = [new NpiAddress { AddressPurpose = "LOCATION", City = city, State = state }];
        }

        if (taxonomyDesc != null || taxonomyState != null || taxonomyLicense != null)
        {
            result.Taxonomies = [new NpiTaxonomy
            {
                Desc = taxonomyDesc,
                State = taxonomyState,
                License = taxonomyLicense,
                Primary = true
            }];
        }

        if (orcidIdentifier != null)
        {
            result.Identifiers = [new NpiIdentifier { Identifier = orcidIdentifier, IdentifierType = "17" }];
        }

        return result;
    }
}

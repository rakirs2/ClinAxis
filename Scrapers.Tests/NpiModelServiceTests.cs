using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class NpiModelServiceTests
{
    private static readonly string[] SchemaFeatureOrder =
    [
        "exact_name_match",
        "middle_name_match", "has_middle_name_match",
        "credential_match", "has_credential_match",
        "state_match", "has_state_match",
        "city_match", "has_city_match",
        "org_match", "has_org_match",
        "other_name_match", "has_other_name_match",
        "specialty_match", "has_specialty_match",
        "license_state_match", "has_license_state_match",
        "department_match", "has_department_match",
        "orcid_match",
        "deactivated",
    ];

    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Data", "NpiModel");

    [TestMethod]
    public void BuildVector_FollowsSchemaOrder_WithCoverageFlags()
    {
        var features = new NpiCandidateFeatures
        {
            ExactNameMatch = true,
            MiddleNameMatch = null,
            CredentialMatch = false,
            StateMatch = true,
            OrcidMatch = false,
            IsDeactivated = false
        };

        var vector = NpiModelService.BuildVector(features, SchemaFeatureOrder);

        Assert.AreEqual(21, vector.Length);
        Assert.AreEqual(1f, vector[0], "exact_name_match");
        Assert.AreEqual(0f, vector[1], "middle_name_match (missing)");
        Assert.AreEqual(0f, vector[2], "has_middle_name_match (missing)");
        Assert.AreEqual(0f, vector[3], "credential_match (present, no match)");
        Assert.AreEqual(1f, vector[4], "has_credential_match");
        Assert.AreEqual(1f, vector[5], "state_match");
        Assert.AreEqual(1f, vector[6], "has_state_match");
        Assert.AreEqual(0f, vector[19], "orcid_match");
        Assert.AreEqual(0f, vector[20], "deactivated");
    }

    [TestMethod]
    public void BuildVector_MissingTriState_IsZeroWithZeroCoverage()
    {
        var features = new NpiCandidateFeatures
        {
            SpecialtyMatch = null,
            LicenseStateMatch = null
        };

        var vector = NpiModelService.BuildVector(features, SchemaFeatureOrder);

        Assert.AreEqual(0f, vector[14], "specialty_match");
        Assert.AreEqual(0f, vector[15], "has_specialty_match");
        Assert.AreEqual(0f, vector[16], "license_state_match");
        Assert.AreEqual(0f, vector[17], "has_license_state_match");
    }

    [TestMethod]
    public void TryLoad_ValidFixture_LoadsAndPredictsInRange()
    {
        var service = NpiModelService.TryLoad(FixturePath);
        Assert.IsNotNull(service, "fixture model.onnx + feature_schema.json must load");
        try
        {
            var allMatch = new NpiCandidateFeatures
            {
                ExactNameMatch = true,
                MiddleNameMatch = true,
                CredentialMatch = true,
                StateMatch = true,
                CityMatch = true,
                OrgMatch = true,
                OtherNameMatch = true,
                SpecialtyMatch = true,
                LicenseStateMatch = true,
                DepartmentMatch = true,
                OrcidMatch = false,
                IsDeactivated = false
            };
            var allMiss = new NpiCandidateFeatures
            {
                ExactNameMatch = false,
                MiddleNameMatch = false,
                CredentialMatch = false,
                StateMatch = false,
                CityMatch = false,
                OrgMatch = false,
                OtherNameMatch = false,
                SpecialtyMatch = false,
                LicenseStateMatch = false,
                DepartmentMatch = false,
                OrcidMatch = false,
                IsDeactivated = false
            };

            var hit = service.Predict(allMatch);
            var miss = service.Predict(allMiss);

            Assert.IsTrue(hit >= 0f && hit <= 1f, $"hit score {hit} outside [0,1]");
            Assert.IsTrue(miss >= 0f && miss <= 1f, $"miss score {miss} outside [0,1]");
            Assert.IsTrue(hit > miss, $"full-match candidate ({hit}) must score above full-miss ({miss})");
        }
        finally
        {
            service.Dispose();
        }
    }

    [TestMethod]
    public void TryLoad_MissingArtifacts_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), "npi-model-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var service = NpiModelService.TryLoad(dir);
            Assert.IsNull(service);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void TryLoad_CorruptModel_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), "npi-model-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "model.onnx"), "not an onnx model");
        File.WriteAllText(Path.Combine(dir, "feature_schema.json"), """
            { "features": ["exact_name_match"], "inputName": "features", "outputName": "probabilities", "positiveClassIndex": 1 }
            """);

        try
        {
            var service = NpiModelService.TryLoad(dir);
            Assert.IsNull(service, "corrupt model.onnx must degrade to null, not throw");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void TryLoad_SchemaWithUnknownFeature_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), "npi-model-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "model.onnx"), "x");
        File.WriteAllText(Path.Combine(dir, "feature_schema.json"),
            """{ "features": ["not_a_feature"], "inputName": "features", "outputName": "probabilities", "positiveClassIndex": 1 }""");

        try
        {
            // Schema is validated before the model loads; an unbindable feature
            // name must degrade to null, never throw at enrichment time.
            var service = NpiModelService.TryLoad(dir);
            Assert.IsNull(service, "unknown schema feature must degrade to null");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

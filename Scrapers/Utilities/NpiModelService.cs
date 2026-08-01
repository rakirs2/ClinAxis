using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Scrapers.Utilities
{
    /// <summary>
    /// ONNX NPI-disambiguation model service (ML track, PR #337 follow-up).
    /// Loads <c>model.onnx</c> + <c>feature_schema.json</c> from the resources dir
    /// (MeSHMatcher/PiCompletionModel pattern). <see cref="TryLoad"/> returns null
    /// when artifacts are missing or invalid — enrichment must degrade gracefully,
    /// never crash (PR #296 convention). The rule scorer stays authoritative;
    /// <c>ModelScore</c> is recorded per candidate for A/B comparison only.
    /// </summary>
    internal sealed class NpiModelService : IDisposable
    {
        // Schema feature name -> value extractor. The schema (written by
        // experiments/npi-disambiguation-model/train.py) must use exactly these
        // names; unknown names fail the load so a version mismatch never
        // surfaces at predict time.
        private static readonly Dictionary<string, Func<NpiCandidateFeatures, float>> Extractors = new(StringComparer.Ordinal)
        {
            ["exact_name_match"] = f => f.ExactNameMatch ? 1f : 0f,
            ["middle_name_match"] = f => Bool(f.MiddleNameMatch),
            ["has_middle_name_match"] = f => Has(f.MiddleNameMatch),
            ["credential_match"] = f => Bool(f.CredentialMatch),
            ["has_credential_match"] = f => Has(f.CredentialMatch),
            ["state_match"] = f => Bool(f.StateMatch),
            ["has_state_match"] = f => Has(f.StateMatch),
            ["city_match"] = f => Bool(f.CityMatch),
            ["has_city_match"] = f => Has(f.CityMatch),
            ["org_match"] = f => Bool(f.OrgMatch),
            ["has_org_match"] = f => Has(f.OrgMatch),
            ["other_name_match"] = f => Bool(f.OtherNameMatch),
            ["has_other_name_match"] = f => Has(f.OtherNameMatch),
            ["specialty_match"] = f => Bool(f.SpecialtyMatch),
            ["has_specialty_match"] = f => Has(f.SpecialtyMatch),
            ["license_state_match"] = f => Bool(f.LicenseStateMatch),
            ["has_license_state_match"] = f => Has(f.LicenseStateMatch),
            ["department_match"] = f => Bool(f.DepartmentMatch),
            ["has_department_match"] = f => Has(f.DepartmentMatch),
            ["orcid_match"] = f => f.OrcidMatch ? 1f : 0f,
            ["deactivated"] = f => f.IsDeactivated ? 1f : 0f,
        };

        private readonly InferenceSession _session;
        private readonly string _inputName;
        private readonly string _outputName;
        private readonly int _positiveClassIndex;
        private readonly string[] _featureNames;

        public static NpiModelService? TryLoad(string resourcesPath)
        {
            ArgumentNullException.ThrowIfNull(resourcesPath);
            var modelPath = Path.Combine(resourcesPath, "model.onnx");
            var schemaPath = Path.Combine(resourcesPath, "feature_schema.json");
            if (!File.Exists(modelPath) || !File.Exists(schemaPath))
            {
                return null;
            }

            try
            {
                return new NpiModelService(modelPath, schemaPath);
            }
            catch (OnnxRuntimeException)
            {
                // Corrupt or incompatible artifacts must never crash enrichment.
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private NpiModelService(string modelPath, string schemaPath)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(schemaPath));
            var root = doc.RootElement;
            _inputName = root.GetProperty("inputName").GetString() ?? "features";
            _outputName = root.GetProperty("outputName").GetString() ?? "probabilities";
            _positiveClassIndex = root.GetProperty("positiveClassIndex").GetInt32();
            _featureNames = root.GetProperty("features")
                .EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty)
                .ToArray();

            // Fail fast on schema/version mismatch before loading the model.
            if (_featureNames.Length == 0 || _featureNames.Any(name => !Extractors.ContainsKey(name)))
            {
                throw new InvalidOperationException("feature_schema.json references features this build cannot bind");
            }

            _session = new InferenceSession(modelPath);
        }

        /// <summary>
        /// Predicts P(candidate is the correct match) for one candidate, using the
        /// exact same features the export endpoint emits for training.
        /// </summary>
        public float Predict(NpiCandidateFeatures features)
        {
            var vector = BuildVector(features, _featureNames);
            var input = new DenseTensor<float>(vector, [1, _featureNames.Length]);

            using var results = _session.Run(
                [NamedOnnxValue.CreateFromTensor(_inputName, input)]);
            var probabilities = results.First(r => r.Name == _outputName).AsTensor<float>();
            return probabilities[0, _positiveClassIndex];
        }

        /// <summary>
        /// Builds the ordered model input vector for one candidate. Feature order
        /// follows the schema, which is the training-time feature order.
        /// </summary>
        internal static float[] BuildVector(NpiCandidateFeatures features, IReadOnlyList<string> featureNames)
        {
            var vector = new float[featureNames.Count];
            for (var i = 0; i < featureNames.Count; i++)
            {
                vector[i] = Extractors[featureNames[i]](features);
            }

            return vector;
        }

        private static float Bool(bool? value) => value == true ? 1f : 0f;

        private static float Has(bool? value) => value.HasValue ? 1f : 0f;

        public void Dispose()
        {
            _session.Dispose();
        }
    }
}

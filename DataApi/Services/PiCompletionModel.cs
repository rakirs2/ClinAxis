using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DataApi.Services;

/// <summary>
/// Loads the ONNX PI-completion model and feature schema from disk (mirrors the
/// MeSHMatcher resource pattern). When the model files are absent the service is
/// simply unavailable and the finder returns a null ModelScore.
/// </summary>
internal sealed class PiCompletionModel : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;
    private readonly int _positiveClassIndex;

    public static PiCompletionModel? TryLoad(string resourcesPath)
    {
        ArgumentNullException.ThrowIfNull(resourcesPath);
        var modelPath = Path.Combine(resourcesPath, "model.onnx");
        var schemaPath = Path.Combine(resourcesPath, "feature_schema.json");
        if (!File.Exists(modelPath) || !File.Exists(schemaPath))
            return null;

        return new PiCompletionModel(modelPath, schemaPath);
    }

    private PiCompletionModel(string modelPath, string schemaPath)
    {
        _session = new InferenceSession(modelPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var root = doc.RootElement;
        _inputName = root.GetProperty("inputName").GetString() ?? "features";
        _outputName = root.GetProperty("outputName").GetString() ?? "probabilities";
        _positiveClassIndex = root.GetProperty("positiveClassIndex").GetInt32();
    }

    /// <summary>
    /// Predicts P(completed) for a candidate using the same feature semantics as
    /// training: [priorStudyCount, priorCompletedCount, priorEnrollmentTotal,
    /// priorCompletionRate].
    /// </summary>
    public float Predict(int studyCount, int completedStudies, int? enrollmentTotal)
    {
        float completionRate = studyCount > 0 ? (float)completedStudies / studyCount : 0f;
        var input = new DenseTensor<float>(
            new float[] { studyCount, completedStudies, enrollmentTotal ?? 0, completionRate },
            [1, 4]);

        using var results = _session.Run(
            [NamedOnnxValue.CreateFromTensor(_inputName, input)]);
        var probabilities = results.First(r => r.Name == _outputName).AsTensor<float>();
        return probabilities[0, _positiveClassIndex];
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}

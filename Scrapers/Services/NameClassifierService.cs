using Microsoft.ML;

namespace Scrapers.Services;

public sealed class NameClassifierService : IDisposable
{
    private readonly MLContext _mlContext;
    private PredictionEngine<NameInput, NamePrediction>? _engine;
    private readonly string _modelPath;
    private bool _loaded;

    public NameClassifierService(string? modelPath = null)
    {
        _mlContext = new MLContext(seed: 42);
        _modelPath = modelPath ?? ResolveModelPath();
    }

    private static string ResolveModelPath()
    {
        var probePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "name-classifier.zip"),
            Path.Combine(AppContext.BaseDirectory, "name-classifier.zip"),
            Path.Combine(Path.GetDirectoryName(typeof(NameClassifierService).Assembly.Location)!, "name-classifier.zip"),
            Path.Combine(Directory.GetCurrentDirectory(), "name-classifier.zip"),
        };

        foreach (var path in probePaths)
        {
            if (File.Exists(path))
                return path;
        }

        return probePaths[0];
    }

    public void LoadModel()
    {
        if (!File.Exists(_modelPath))
        {
            _loaded = false;
            return;
        }

        var model = _mlContext.Model.Load(_modelPath, out _);
        _engine = _mlContext.Model.CreatePredictionEngine<NameInput, NamePrediction>(model);
        _loaded = true;
    }

    public NameClassificationResult Predict(string name, string? role = null)
    {
        if (!_loaded || _engine == null)
        {
            return new NameClassificationResult
            {
                IsHuman = true,
                Confidence = 0.5,
                ModelAvailable = false
            };
        }

        var input = new NameInput { Name = name };
        var prediction = _engine.Predict(input);

        return new NameClassificationResult
        {
            IsHuman = prediction.PredictedLabel,
            Confidence = prediction.PredictedLabel
                ? prediction.Probability
                : 1.0 - prediction.Probability,
            ModelAvailable = true
        };
    }

    public bool ModelAvailable => _loaded;

    public static NameClassifierService Train(IEnumerable<NameInput> samples, string? modelPath = null)
    {
        var path = modelPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "name-classifier.zip");
        var mlContext = new MLContext(seed: 42);
        var data = mlContext.Data.LoadFromEnumerable(samples);
        var pipeline = mlContext.Transforms.Text.FeaturizeText("Features", nameof(NameInput.Name))
            .Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression("Label", "Features"));
        var model = pipeline.Fit(data);
        mlContext.Model.Save(model, data.Schema, path);

        var svc = new NameClassifierService(path);
        svc.LoadModel();
        return svc;
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }
}

public sealed class NameInput
{
    public string Name { get; set; } = string.Empty;
    public bool Label { get; set; }
}

public sealed class NamePrediction
{
    public bool PredictedLabel { get; set; }
    public float Score { get; set; }
    public float Probability { get; set; }
}

public sealed class NameClassificationResult
{
    public bool IsHuman { get; set; }
    public double Confidence { get; set; }
    public bool ModelAvailable { get; set; }
}

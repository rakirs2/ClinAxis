using DataApi.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataApi.Tests;

[TestClass]
public sealed class PiCompletionModelTests
{
    private static string? ResolveModelResources()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Resources", "pi-model"),
            Path.Combine(baseDir, "..", "..", "..", "..", "Scrapers", "Resources", "pi-model"),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "Scrapers", "Resources", "pi-model"),
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    [TestMethod]
    public void TryLoad_ReturnsModel_WhenArtifactsPresent()
    {
        var resourcesPath = ResolveModelResources()!;
        Assert.IsNotNull(resourcesPath, "pi-model resources directory not found");

        using var model = PiCompletionModel.TryLoad(resourcesPath);
        Assert.IsNotNull(model);
    }

    [TestMethod]
    public void TryLoad_ReturnsNull_WhenArtifactsMissing()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.IsNull(PiCompletionModel.TryLoad(missing));
    }

    [TestMethod]
    public void Predict_ReturnsProbabilityInRange()
    {
        using var model = PiCompletionModel.TryLoad(ResolveModelResources()!);
        Assert.IsNotNull(model);

        var coldStart = model.Predict(0, 0, 0);
        Assert.IsTrue(coldStart >= 0f && coldStart <= 1f);

        var experienced = model.Predict(10, 9, 15000);
        Assert.IsTrue(experienced >= 0f && experienced <= 1f);
    }

    [TestMethod]
    public void Predict_ColdStartMatchesTrainedBaseRate()
    {
        using var model = PiCompletionModel.TryLoad(ResolveModelResources()!);
        Assert.IsNotNull(model);

        var coldStart = model.Predict(0, 0, 0);
        Assert.IsTrue(Math.Abs(coldStart - 0.889f) < 0.05f,
            $"cold-start prediction {coldStart:F4} should be near trained base rate 0.889");
    }

    [TestMethod]
    public void Predict_HigherCompletionHistoryYieldsHigherScore()
    {
        using var model = PiCompletionModel.TryLoad(ResolveModelResources()!);
        Assert.IsNotNull(model);

        var strong = model.Predict(10, 9, 15000);
        var weak = model.Predict(10, 3, 15000);
        Assert.IsTrue(strong > weak, $"expected {strong:F4} > {weak:F4}");
    }
}

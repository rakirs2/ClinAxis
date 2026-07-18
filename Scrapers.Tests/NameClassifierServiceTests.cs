using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services;

namespace Scrapers.Tests;

[TestClass]
public sealed class NameClassifierServiceTests
{
    [TestMethod]
    public void Predict_WithNoModelLoaded_ReturnsDefaultAccept()
    {
        using var svc = new NameClassifierService("/nonexistent/path.zip");
        var result = svc.Predict("John Smith");

        Assert.IsTrue(result.IsHuman);
        Assert.AreEqual(0.5, result.Confidence);
        Assert.IsFalse(result.ModelAvailable);
    }

    [TestMethod]
    public void Predict_EmptyName_ReturnsDefaultAccept()
    {
        using var svc = new NameClassifierService("/nonexistent/path.zip");
        var result = svc.Predict("");

        Assert.IsTrue(result.IsHuman);
    }

    [TestMethod]
    public void TrainAndPredict_SimpleCase_ReturnsPrediction()
    {
        var positives = new List<NameInput>
        {
            new() { Name = "John Smith MD", Label = true },
            new() { Name = "Jane Doe PhD", Label = true },
            new() { Name = "Alice Brown", Label = true },
            new() { Name = "Bob Jones", Label = true },
            new() { Name = "Carol White", Label = true },
        };
        var negatives = new List<NameInput>
        {
            new() { Name = "Study Director", Label = false },
            new() { Name = "Medical Monitor", Label = false },
            new() { Name = "Clinical Trial", Label = false },
            new() { Name = "Call Center", Label = false },
            new() { Name = "Pfizer Inc", Label = false },
        };

        var allSamples = new List<NameInput>();
        allSamples.AddRange(positives);
        allSamples.AddRange(negatives);

        var svc = NameClassifierService.Train(allSamples, "/tmp/test-name-classifier.zip");
        try
        {
            Assert.IsTrue(svc.ModelAvailable);

            var johnResult = svc.Predict("John Smith MD");
            Assert.IsTrue(johnResult.IsHuman);
            Assert.IsTrue(johnResult.Confidence >= 0.5);

            var studyResult = svc.Predict("Study Director");
            Assert.IsFalse(studyResult.IsHuman);
            Assert.IsTrue(studyResult.Confidence >= 0.5);
        }
        finally
        {
            svc.Dispose();
            if (File.Exists("/tmp/test-name-classifier.zip"))
                File.Delete("/tmp/test-name-classifier.zip");
        }
    }
}

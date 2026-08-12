using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services;

namespace Scrapers.Tests;

[TestClass]
public sealed class MeSHMatcherTests
{
    [TestMethod]
    public void BuildSearchOrder_DeduplicatesEmbeddingsAndPreservesFirstAliasOrder()
    {
        var searchOrder = MeSHMatcher.BuildSearchOrder([2, 1, 2, 0, 1]);

        CollectionAssert.AreEqual(new[] { 0, 1, 3 }, searchOrder);
    }

    [TestMethod]
    public void GetSequenceLength_ReadsFixedSequenceDimensionFromDynamicBatchShape()
    {
        Assert.AreEqual(128, MeSHMatcher.GetSequenceLength([-1, 128]));
    }

    [TestMethod]
    public void GetSequenceLength_RejectsDynamicSequenceDimension()
    {
        Assert.Throws<InvalidOperationException>(() => MeSHMatcher.GetSequenceLength([-1, -1]));
    }

    [TestMethod]
    public void LoadDoLowerCase_ReadsTokenizerConfiguration()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mesh-tokenizer-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"do_lower_case\":true}");

            Assert.IsTrue(MeSHMatcher.LoadDoLowerCase(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void LoadMatchThreshold_UsesConfiguredValue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mesh-matcher-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"match_threshold\":0.42}");

            Assert.AreEqual(0.42f, MeSHMatcher.LoadMatchThreshold(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Constructor_CorruptModelFile_ThrowsOnnxRuntimeException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var modelPath = Path.Combine(tempDir, "model.onnx");
            File.WriteAllBytes(modelPath, [0, 0, 0, 0, 0, 0, 0, 0]);

            Assert.Throws<Microsoft.ML.OnnxRuntime.OnnxRuntimeException>(
                () => new MeSHMatcher(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

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

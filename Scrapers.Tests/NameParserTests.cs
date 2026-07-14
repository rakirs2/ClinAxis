using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class NameParserTests
{
    [TestMethod]
    public void Parse_PlainName_ReturnsNameOnly()
    {
        var (prefix, name, suffix) = NameParser.Parse("John Smith");
        Assert.IsNull(prefix);
        Assert.AreEqual("John Smith", name);
        Assert.IsNull(suffix);
    }

    [TestMethod]
    public void Parse_PrefixAndName_ParsesPrefix()
    {
        var (prefix, name, suffix) = NameParser.Parse("Dr. John Smith");
        Assert.AreEqual("Dr.", prefix);
        Assert.AreEqual("John Smith", name);
        Assert.IsNull(suffix);
    }

    [TestMethod]
    public void Parse_NameAndSuffix_ParsesSuffix()
    {
        var (prefix, name, suffix) = NameParser.Parse("John Smith, MD");
        Assert.IsNull(prefix);
        Assert.AreEqual("John Smith", name);
        Assert.AreEqual("MD", suffix);
    }

    [TestMethod]
    public void Parse_FullHonorifics_ParsesAll()
    {
        var (prefix, name, suffix) = NameParser.Parse("Prof. John Smith, PhD");
        Assert.AreEqual("Prof.", prefix);
        Assert.AreEqual("John Smith", name);
        Assert.AreEqual("PhD", suffix);
    }

    [TestMethod]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        var (prefix, name, suffix) = NameParser.Parse("");
        Assert.IsNull(prefix);
        Assert.AreEqual("", name);
        Assert.IsNull(suffix);
    }

    [TestMethod]
    public void Parse_Whitespace_ReturnsEmpty()
    {
        var (prefix, name, suffix) = NameParser.Parse("   ");
        Assert.IsNull(prefix);
        Assert.AreEqual("", name);
        Assert.IsNull(suffix);
    }

    [TestMethod]
    public void Parse_SuffixWithoutComma_ParsesSuffix()
    {
        var (prefix, name, suffix) = NameParser.Parse("John Smith MD");
        Assert.IsNull(prefix);
        Assert.AreEqual("John Smith", name);
        Assert.AreEqual("MD", suffix);
    }

    [TestMethod]
    public void Parse_JrSuffix_ParsesCorrectly()
    {
        var (prefix, name, suffix) = NameParser.Parse("John Smith, Jr.");
        Assert.IsNull(prefix);
        Assert.AreEqual("John Smith", name);
        Assert.AreEqual("Jr.", suffix);
    }

    [TestMethod]
    public void Parse_NonHonorificName_DoesNotStrip()
    {
        var (prefix, name, suffix) = NameParser.Parse("Maria Garcia-Lopez");
        Assert.IsNull(prefix);
        Assert.AreEqual("Maria Garcia-Lopez", name);
        Assert.IsNull(suffix);
    }

    [TestMethod]
    public void Parse_ProfWithMdSuffix_ParsesBoth()
    {
        var (prefix, name, suffix) = NameParser.Parse("Prof. Jane Doe, MD, PhD");
        Assert.AreEqual("Prof.", prefix);
        Assert.AreEqual("Jane Doe", name);
        Assert.AreEqual("MD, PhD", suffix);
    }
}

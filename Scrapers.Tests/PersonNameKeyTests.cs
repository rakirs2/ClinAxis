using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class PersonNameKeyTests
{
    [TestMethod]
    public void Parse_PlainName_ReturnsTrimmedRaw()
    {
        var (prefix, fullName, raw) = PersonNameKey.Parse("  John Smith  ");

        Assert.IsNull(prefix);
        Assert.AreEqual("John Smith", fullName);
        Assert.AreEqual("John Smith", raw);
    }

    [TestMethod]
    public void Parse_PrefixedName_ReturnsPrefixAndFullName()
    {
        var (prefix, fullName, _) = PersonNameKey.Parse("Dr. John Smith");

        Assert.AreEqual("Dr.", prefix);
        Assert.AreEqual("John Smith", fullName);
    }

    [TestMethod]
    public void Parse_EmptyString_ReturnsEmptyFullName()
    {
        var (prefix, fullName, _) = PersonNameKey.Parse("");

        Assert.IsNull(prefix);
        Assert.AreEqual("", fullName);
    }

    [TestMethod]
    public void LookupOrder_SameName_ReturnsSingleKey()
    {
        var keys = PersonNameKey.LookupOrder("John Smith", "John Smith");

        Assert.AreEqual(1, keys.Length);
        Assert.AreEqual("John Smith", keys[0]);
    }

    [TestMethod]
    public void LookupOrder_NormalizedDiffersFromRaw_ParsedFirstThenLegacy()
    {
        var keys = PersonNameKey.LookupOrder("John Smith", "JOHN SMITH");

        Assert.AreEqual(2, keys.Length);
        Assert.AreEqual("John Smith", keys[0]);
        Assert.AreEqual("JOHN SMITH", keys[1]);
    }

    [TestMethod]
    public void MergePrefix_NoExistingPrefix_FillsCandidate()
    {
        Assert.AreEqual("Dr.", PersonNameKey.MergePrefix(null, "Dr."));
    }

    [TestMethod]
    public void MergePrefix_ExistingPrefix_IsPreserved()
    {
        Assert.AreEqual("Prof.", PersonNameKey.MergePrefix("Prof.", "Dr."));
    }

    [TestMethod]
    public void MergePrefix_NullCandidate_KeepsCurrent()
    {
        Assert.AreEqual("Dr.", PersonNameKey.MergePrefix("Dr.", null));
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class PersonNamePartsTests
{
    [TestMethod]
    public void Variations_EmptyString_ReturnsEmpty()
    {
        var variations = PersonNameParts.Variations("");

        Assert.AreEqual(0, variations.Count);
    }

    [TestMethod]
    public void Variations_WhitespaceOnly_ReturnsEmpty()
    {
        var variations = PersonNameParts.Variations("   ");

        Assert.AreEqual(0, variations.Count);
    }

    [TestMethod]
    public void Variations_SingleTokenSuffix_ReturnsFirstEmptyLastToken()
    {
        var variations = PersonNameParts.Variations("MD");

        Assert.AreEqual(1, variations.Count);
        Assert.AreEqual(("", "MD"), variations[0]);
    }

    [TestMethod]
    public void Variations_SingleToken_ReturnsFirstEmptyLastToken()
    {
        var variations = PersonNameParts.Variations("Madonna");

        Assert.AreEqual(1, variations.Count);
        Assert.AreEqual(("", "Madonna"), variations[0]);
    }

    [TestMethod]
    public void Variations_FirstLast_ReturnsSingleVariation()
    {
        var variations = PersonNameParts.Variations("John Smith");

        Assert.AreEqual(1, variations.Count);
        Assert.AreEqual(("John", "Smith"), variations[0]);
    }

    [TestMethod]
    public void Variations_FirstMiddleLast_ReturnsAllVariations()
    {
        var variations = PersonNameParts.Variations("John A Smith");

        Assert.AreEqual(3, variations.Count);
        Assert.AreEqual(("John", "Smith"), variations[0]);
        Assert.AreEqual(("John", "A Smith"), variations[1]);
        Assert.AreEqual(("John", "Smith"), variations[2]);
    }

    [TestMethod]
    public void Variations_FirstMultiWordMiddleLast_ReturnsNameVariationsWithoutDuplicate()
    {
        var variations = PersonNameParts.Variations("John Alexander Smith");

        Assert.AreEqual(2, variations.Count);
        Assert.AreEqual(("John", "Smith"), variations[0]);
        Assert.AreEqual(("John", "Alexander Smith"), variations[1]);
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class SpecialtyCategoryMapperTests
{
    [TestMethod]
    public void MapToCategories_NeoplasmText_MapsToOncology()
    {
        var categories = SpecialtyCategoryMapper.MapToCategories("Breast Neoplasms");
        CollectionAssert.Contains(categories.ToList(), "oncology");
    }

    [TestMethod]
    public void MapToCategories_CardiovascularText_MapsToCardiology()
    {
        var categories = SpecialtyCategoryMapper.MapToCategories("Coronary Artery Disease");
        CollectionAssert.Contains(categories.ToList(), "cardiology");
    }

    [TestMethod]
    public void MapToCategories_NeurologyText_MapsToNeurology()
    {
        var categories = SpecialtyCategoryMapper.MapToCategories("Alzheimer Disease");
        CollectionAssert.Contains(categories.ToList(), "neurology");
    }

    [TestMethod]
    public void MapToCategories_UnrelatedText_ReturnsEmpty()
    {
        var categories = SpecialtyCategoryMapper.MapToCategories("Unrelated, Physical Therapy");
        Assert.AreEqual(0, categories.Count);
    }

    [TestMethod]
    public void MapToCategories_NullOrEmpty_ReturnsEmpty()
    {
        Assert.AreEqual(0, SpecialtyCategoryMapper.MapToCategories(null!).Count);
        Assert.AreEqual(0, SpecialtyCategoryMapper.MapToCategories("").Count);
    }

    [TestMethod]
    public void MapToCategories_TaxonomyDescription_MapsBothSides()
    {
        var personCategories = SpecialtyCategoryMapper.MapToCategories("Lung Neoplasms");
        var candidateCategories = SpecialtyCategoryMapper.MapToCategories("Medical Oncology");
        Assert.IsTrue(SpecialtyCategoryMapper.AnyOverlap(personCategories, candidateCategories));
    }

    [TestMethod]
    public void AllCategories_ContainsExpectedSpecialties()
    {
        var all = SpecialtyCategoryMapper.AllCategories;
        CollectionAssert.Contains(all.ToList(), "oncology");
        CollectionAssert.Contains(all.ToList(), "cardiology");
        CollectionAssert.Contains(all.ToList(), "neurology");
    }
}

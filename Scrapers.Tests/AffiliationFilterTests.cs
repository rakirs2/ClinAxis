using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class AffiliationFilterTests
{
    [TestMethod]
    public void IsValidInstitutionName_RealInstitution_ReturnsTrue()
    {
        Assert.IsTrue(AffiliationFilter.IsValidInstitutionName("Cardiology Center"));
        Assert.IsTrue(AffiliationFilter.IsValidInstitutionName("Mayo Clinic"));
    }

    [TestMethod]
    public void IsValidInstitutionName_BlocklistRole_ReturnsFalse()
    {
        Assert.IsFalse(AffiliationFilter.IsValidInstitutionName("Anesthesiologist"));
        Assert.IsFalse(AffiliationFilter.IsValidInstitutionName("Professor"));
        Assert.IsFalse(AffiliationFilter.IsValidInstitutionName("Surgeon"));
    }

    [TestMethod]
    public void IsValidInstitutionName_SingleWordOccupationalSuffix_ReturnsFalse()
    {
        Assert.IsFalse(AffiliationFilter.IsValidInstitutionName("Dentist"));
        Assert.IsFalse(AffiliationFilter.IsValidInstitutionName("Librarian"));
    }

    [TestMethod]
    public void IsValidInstitutionName_BlankOrWhitespace_ReturnsFalse()
    {
        Assert.IsFalse(AffiliationFilter.IsValidInstitutionName(""));
        Assert.IsFalse(AffiliationFilter.IsValidInstitutionName("   "));
    }

    [TestMethod]
    public void IsValidInstitutionName_UntrimmedRealName_ReturnsTrue()
    {
        Assert.IsTrue(AffiliationFilter.IsValidInstitutionName("  Cardiology Center  "));
    }
}

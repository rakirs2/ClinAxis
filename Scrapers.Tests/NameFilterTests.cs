using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class NameFilterTests
{
    [TestMethod]
    public void IsHumanName_NormalName_ReturnsTrue()
    {
        Assert.IsTrue(NameFilter.IsHumanName("John Smith", null));
    }

    [TestMethod]
    public void IsHumanName_NameWithHyphen_ReturnsTrue()
    {
        Assert.IsTrue(NameFilter.IsHumanName("Maria Garcia-Lopez", null));
    }

    [TestMethod]
    public void IsHumanName_CjkName_ReturnsTrue()
    {
        Assert.IsTrue(NameFilter.IsHumanName("张伟", null));
    }

    [TestMethod]
    public void IsHumanName_KnownPiRole_ReturnsTrue()
    {
        Assert.IsTrue(NameFilter.IsHumanName("Study Coordinator", "PRINCIPAL_INVESTIGATOR"));
    }

    [TestMethod]
    public void IsHumanName_PharmaCompany_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Pfizer", null));
    }

    [TestMethod]
    public void IsHumanName_PharmaCompanyWithRole_ReturnsTrue()
    {
        Assert.IsTrue(NameFilter.IsHumanName("Pfizer", "PRINCIPAL_INVESTIGATOR"));
    }

    [TestMethod]
    public void IsHumanName_UniversityName_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("University of California", null));
    }

    [TestMethod]
    public void IsHumanName_DepartmentName_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Department of Cardiology", null));
    }

    [TestMethod]
    public void IsHumanName_ContactForPublicQueries_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Contact for Public Queries", null));
    }

    [TestMethod]
    public void IsHumanName_CorporateSuffix_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Research Lab Inc", null));
    }

    [TestMethod]
    public void IsHumanName_AndCompany_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Johnson & Johnson", null));
    }

    [TestMethod]
    public void IsHumanName_EmptyName_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("", null));
    }

    [TestMethod]
    public void IsHumanName_TooShort_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("AB", null));
    }

    [TestMethod]
    public void IsHumanName_TooManyWords_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("A B C D E F G", null));
    }

    [TestMethod]
    public void IsHumanName_HospitalName_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Massachusetts General Hospital", null));
    }

    [TestMethod]
    public void IsHumanName_InstituteName_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("National Institutes of Health", null));
    }

    [TestMethod]
    public void IsHumanName_AllCapsRoleLabel_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("STUDY DIRECTOR CONTACT PERSON", null));
    }

    [TestMethod]
    public void IsHumanName_SingleLongWord_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Supercalifragilisticexpialidocious", null));
    }

    [TestMethod]
    public void IsHumanName_PharmaBlocklistSingleWord_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Novartis", null));
        Assert.IsFalse(NameFilter.IsHumanName("Roche", null));
        Assert.IsFalse(NameFilter.IsHumanName("AstraZeneca", null));
    }

    [TestMethod]
    public void IsHumanName_MedicalCenter_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Cleveland Clinic", null));
    }

    [TestMethod]
    public void IsHumanName_GskPharma_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("GSK", null));
    }

    [TestMethod]
    public void IsHumanName_GskClinicalTrials_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("GSK Clinical Trials", null));
    }

    [TestMethod]
    public void IsHumanName_SponsorPrefix_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Sponsor Chugai Pharmaceutical CO.Ltd", null));
    }

    [TestMethod]
    public void IsHumanName_ChugaiPharma_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Chugai", null));
    }
}

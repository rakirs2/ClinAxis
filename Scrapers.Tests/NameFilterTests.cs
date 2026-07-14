using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class NameFilterTests
{
    [TestMethod]
    public void IsHumanName_NormalName_ReturnsTrue()
    {
        Assert.IsTrue(NameFilter.IsHumanName("John Smith", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_NameWithHyphen_ReturnsTrue()
    {
        Assert.IsTrue(NameFilter.IsHumanName("Maria Garcia-Lopez", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_CjkName_ReturnsTrue()
    {
        Assert.IsTrue(NameFilter.IsHumanName("张伟", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_KnownPiRole_ReturnsTrue()
    {
        Assert.IsTrue(NameFilter.IsHumanName("Jane Smith", "PRINCIPAL_INVESTIGATOR").IsHuman);
    }

    [TestMethod]
    public void IsHumanName_PharmaCompany_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Pfizer", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_PharmaCompanyWithRole_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Pfizer", "PRINCIPAL_INVESTIGATOR").IsHuman);
    }

    [TestMethod]
    public void IsHumanName_UniversityName_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("University of California", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_DepartmentName_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Department of Cardiology", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_ContactForPublicQueries_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Contact for Public Queries", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_CorporateSuffix_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Research Lab Inc", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_AndCompany_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Johnson & Johnson", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_EmptyName_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_TooShort_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("AB", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_TooManyWords_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("A B C D E F G", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_HospitalName_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Massachusetts General Hospital", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_InstituteName_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("National Institutes of Health", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_AllCapsRoleLabel_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("STUDY DIRECTOR CONTACT PERSON", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_SingleLongWord_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Supercalifragilisticexpialidocious", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_PharmaBlocklistSingleWord_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Novartis", null).IsHuman);
        Assert.IsFalse(NameFilter.IsHumanName("Roche", null).IsHuman);
        Assert.IsFalse(NameFilter.IsHumanName("AstraZeneca", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_MedicalCenter_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Cleveland Clinic", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_GskPharma_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("GSK", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_GskClinicalTrials_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("GSK Clinical Trials", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_SponsorPrefix_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Sponsor Chugai Pharmaceutical CO.Ltd", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_ChugaiPharma_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Chugai", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_GskClinicalTrialsWithStudyDirectorRole_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("GSK Clinical Trials", "STUDY_DIRECTOR").IsHuman);
    }

    [TestMethod]
    public void IsHumanName_PfizerCtgovCallCenter_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Pfizer CT.gov Call Center", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_MedicalDirector_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Medical Director", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_GileadStudyDirectorWithRole_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Gilead Study Director", "STUDY_DIRECTOR").IsHuman);
    }

    [TestMethod]
    public void IsHumanName_GlobalClinicalRegistry_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Global Clinical Registry", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_StudyDriector_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Study Driector", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_MedicalMontiior_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Medical Montiior", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_MedicalResponsible_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Medical Responsible", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_UcbCares_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("UCB Cares", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_ClinicalTrialManagement_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Clinical Trial Management", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_NovartisPharmaceutricals_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Novartis Pharmaceutricals", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_AbbVieInc_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("AbbVie Inc", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_BristolMyersSquibb_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Bristol-Myers Squibb", null).IsHuman);
        Assert.IsFalse(NameFilter.IsHumanName("Bristol Myers Squibb", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_UseCentralContact_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Use Central Contact", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_MedImmune_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("MedImmune", null).IsHuman);
    }

    [TestMethod]
    public void IsHumanName_NameContainingSponsor_ReturnsFalse()
    {
        Assert.IsFalse(NameFilter.IsHumanName("Sebastiano Biondo, Sponsor", null).IsHuman);
    }
}

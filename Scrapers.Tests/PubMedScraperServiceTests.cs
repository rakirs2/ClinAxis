using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;
using Scrapers.Services;
using Scrapers.Testing;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Scrapers.Tests
{
    [TestClass]
    public class PubMedScraperServiceTests : DbTestBase
    {
        private static string LoadFixture(string name)
        {
            return File.ReadAllText($"Data/PubMed/{name}");
        }

        [TestMethod]
        public void ParsePubmedXml_ParsesMultiplePublicationTypes()
        {
            var xml = LoadFixture("MultipleTypes.xml");
            var result = PubMedScraperService.ParsePubmedXml(xml);

            Assert.IsNotNull(result);
            Assert.AreEqual("Journal Article, Clinical Trial, Randomized Controlled Trial", result.PublicationTypes);
        }

        [TestMethod]
        public void ParsePubmedXml_ParsesSinglePublicationType()
        {
            var xml = LoadFixture("SingleType.xml");
            var result = PubMedScraperService.ParsePubmedXml(xml);

            Assert.IsNotNull(result);
            Assert.AreEqual("Journal Article, Review", result.PublicationTypes);
        }

        [TestMethod]
        public void ParsePubmedXml_ReturnsNullWhenNoPublicationTypeList()
        {
            var xml = LoadFixture("NoTypeList.xml");
            var result = PubMedScraperService.ParsePubmedXml(xml);

            Assert.IsNotNull(result);
            Assert.IsNull(result.PublicationTypes);
        }

        [TestMethod]
        public void ParsePubmedXml_ParsesOtherFieldsCorrectly()
        {
            var xml = LoadFixture("MultipleTypes.xml");
            var result = PubMedScraperService.ParsePubmedXml(xml);

            Assert.IsNotNull(result);
            Assert.AreEqual("Clinical Trial of Drug X for Condition Y", result.Title);
            Assert.AreEqual("Test Journal", result.Journal);
            Assert.AreEqual(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc), result.PublicationDate);
            Assert.IsFalse(result.IsNonEnglish);
            Assert.IsNull(result.Doi);
            Assert.IsNotNull(result.Abstract);
        }

        [TestMethod]
        public void ParsePubmedXml_ParsesMeshHeadings()
        {
            var xml = LoadFixture("WithMeshHeadings.xml");
            var result = PubMedScraperService.ParsePubmedXml(xml);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.MeshHeadings);
            Assert.AreEqual(3, result.MeshHeadings.Count);

            var first = result.MeshHeadings[0];
            Assert.AreEqual("Breast Neoplasms", first.DescriptorName);
            Assert.AreEqual("drug therapy", first.QualifierName);
            Assert.AreEqual("D001943", first.DescriptorUI);

            var second = result.MeshHeadings[1];
            Assert.AreEqual("Neoplasm Metastasis", second.DescriptorName);
            Assert.IsNull(second.QualifierName);

            var third = result.MeshHeadings[2];
            Assert.AreEqual("Lung Neoplasms", third.DescriptorName);
            Assert.AreEqual("surgery", third.QualifierName);
        }

        [TestMethod]
        public async Task IngestPubMedPapersAsync_DoesNotInsertDuplicatePmids()
        {
            var study = new StudyEntity
            {
                NctId = "NCT00000001",
                BriefTitle = "Test Study",
                OverallStatus = "Recruiting"
            };
            Context.Studies.Add(study);

            var existingPaper = new PubmedPaperEntity
            {
                Pmid = "12345678",
                Title = "Existing Paper Title",
                PublicationTypes = "Journal Article",
            };
            Context.PubmedPapers.Add(existingPaper);
            await Context.SaveChangesAsync();

            // Link the existing paper to the study
            Context.StudyPapers.Add(new StudyPaperEntity
            {
                StudyNctId = study.NctId,
                PubmedPaperId = existingPaper.Id,
            });
            await Context.SaveChangesAsync();

            var scraper = new PubMedScraperService(ConnectionString);
            var count = await scraper.IngestPubMedPapersAsync();

            var linkCount = await Context.StudyPapers.CountAsync(sp => sp.StudyNctId == study.NctId);
            Assert.AreEqual(1, linkCount);
            var paper = await Context.PubmedPapers.FirstAsync(p => p.Pmid == "12345678");
            Assert.AreEqual("Existing Paper Title", paper.Title);
        }
    }
}

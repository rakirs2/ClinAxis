using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Utilities;

namespace Scrapers.Services
{
    public class PubMedScraperService
    {
        private static readonly HttpClient _httpClient = new();
        private readonly string _connectionString;

        public PubMedScraperService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<int> IngestPubMedPapersAsync(CancellationToken cancellationToken = default)
        {
            DbContextOptions<ClinicalTrialsContext> contextOptions = new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(_connectionString)
                .Options;

            var totalPapers = 0;

            using (var context = new ClinicalTrialsContext(contextOptions))
            {
                var studiesWithReferences = await context.Studies
                    .Where(s => !s.IsIncomplete && s.OverallStatus != "COMPLETED")
                    .Include(s => s.References)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                foreach (var study in studiesWithReferences)
                {
                    if (study.References == null || study.References.Count == 0)
                    {
                        continue;
                    }

                    foreach (var reference in study.References)
                    {
                        if (string.IsNullOrWhiteSpace(reference.Pmid))
                        {
                            continue;
                        }

                        // Check if a StudyPaper link already exists for this study + PMID
                        var existingLink = await context.StudyPapers
                            .AnyAsync(sp => sp.StudyNctId == study.NctId && sp.PubmedPaper!.Pmid == reference.Pmid, cancellationToken)
                            .ConfigureAwait(false);
                        if (existingLink)
                        {
                            continue;
                        }

                        // Find or create canonical PubmedPaperEntity by Pmid
                        var pubmedPaper = await context.PubmedPapers
                            .FirstOrDefaultAsync(p => p.Pmid == reference.Pmid, cancellationToken)
                            .ConfigureAwait(false);

                        if (pubmedPaper == null)
                        {
                            PaperDetail? paperDetail = await FetchPaperDetailAsync(reference.Pmid, cancellationToken).ConfigureAwait(false);

                            pubmedPaper = new PubmedPaperEntity
                            {
                                Pmid = reference.Pmid,
                                Doi = paperDetail?.Doi,
                                Title = paperDetail?.Title,
                                Journal = paperDetail?.Journal,
                                PublicationDate = paperDetail?.PublicationDate,
                                Abstract = paperDetail?.Abstract,
                                IsNonEnglish = paperDetail?.IsNonEnglish ?? false,
                            };

                            context.PubmedPapers.Add(pubmedPaper);
                        }

                        // Create the link between this study and the paper
                        context.StudyPapers.Add(new StudyPaperEntity
                        {
                            StudyNctId = study.NctId,
                            PubmedPaperId = pubmedPaper.Id,
                        });

                        totalPapers++;
                    }
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return totalPapers;
        }

        internal static async Task<PaperDetail?> FetchPaperDetailAsync(string pmid, CancellationToken cancellationToken)
        {
            var url = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?db=pubmed&id={pmid}&retmode=xml&rettype=abstract";

            HttpResponseMessage response = await _httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParsePubmedXml(xml);
        }

        private static PaperDetail? ParsePubmedXml(string xml)
        {
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(xml);

            XmlNode? article = doc.SelectSingleNode("//PubmedArticle//Article");
            if (article == null)
            {
                return null;
            }

            var title = article.SelectSingleNode("ArticleTitle")?.InnerText;
            var journal = article.SelectSingleNode("Journal//Title")?.InnerText;
            var isNonEnglish = LanguageHelper.IsNonEnglish(title);
            var abstractText = article.SelectSingleNode("Abstract/AbstractText")?.InnerText;

            DateTime? pubDate = null;
            XmlNode? pubDateNode = article.SelectSingleNode("Journal//JournalIssue//PubDate");
            if (pubDateNode != null)
            {
                var year = pubDateNode["Year"]?.InnerText;
                var month = pubDateNode["Month"]?.InnerText;
                var day = pubDateNode["Day"]?.InnerText;
                if (int.TryParse(year, out var y))
                {
                    var m = month switch
                    {
                        "Jan" => 1,
                        "Feb" => 2,
                        "Mar" => 3,
                        "Apr" => 4,
                        "May" => 5,
                        "Jun" => 6,
                        "Jul" => 7,
                        "Aug" => 8,
                        "Sep" => 9,
                        "Oct" => 10,
                        "Nov" => 11,
                        "Dec" => 12,
                        _ => 1
                    };
                    var d = int.TryParse(day, out var dayVal) ? dayVal : 1;
                    pubDate = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);
                }
            }

            string? doi = null;
            XmlNodeList? articleIdList = doc.SelectNodes("//ArticleIdList/ArticleId");
            if (articleIdList != null)
            {
                foreach (System.Xml.XmlNode idNode in articleIdList)
                {
                    if (idNode.Attributes?["IdType"]?.Value == "doi")
                    {
                        doi = idNode.InnerText;
                        break;
                    }
                }
            }

            var authors = new List<AuthorInfo>();
            XmlNode? authorList = article.SelectSingleNode("AuthorList");
            if (authorList != null)
            {
                foreach (System.Xml.XmlNode authorNode in authorList.ChildNodes)
                {
                    if (authorNode.Name != "Author")
                    {
                        continue;
                    }

                    var lastName = authorNode["LastName"]?.InnerText;
                    var firstName = authorNode["ForeName"]?.InnerText;

                    string? orcid = null;
                    if (authorNode["Identifier"] is { } identifier)
                    {
                        orcid = identifier.InnerText;
                    }

                    authors.Add(new AuthorInfo
                    {
                        LastName = lastName,
                        ForeName = firstName,
                        Orcid = orcid
                    });
                }
            }

            return new PaperDetail
            {
                Title = title,
                Journal = journal,
                PublicationDate = pubDate,
                Doi = doi,
                Abstract = abstractText,
                IsNonEnglish = isNonEnglish,
                Authors = authors.Count > 0 ? authors : null
            };
        }

        internal sealed class PaperDetail
        {
            public string? Title { get; set; }
            public string? Journal { get; set; }
            public DateTime? PublicationDate { get; set; }
            public string? Doi { get; set; }
            public string? Abstract { get; set; }
            public bool IsNonEnglish { get; set; }
            public List<AuthorInfo>? Authors { get; set; }
        }

        internal sealed class AuthorInfo
        {
            public string? LastName { get; set; }
            public string? ForeName { get; set; }
            public string? Orcid { get; set; }
        }
    }
}

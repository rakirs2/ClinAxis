using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Utilities;

namespace Scrapers.Services
{
    public class PubMedScraperService
    {
        private readonly string _connectionString;

        public PubMedScraperService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<int> IngestPubMedPapersAsync(CancellationToken cancellationToken = default)
        {
            var contextOptions = new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(_connectionString)
                .Options;

            int totalPapers = 0;

            await using (var context = new ClinicalTrialsContext(contextOptions))
            {
                var studiesWithPmids = await context.Studies
                    .Where(s => !s.IsIncomplete)
                    .Select(s => new { s.NctId })
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                foreach (var study in studiesWithPmids)
                {
                    var pmids = await FetchPmidsFromClinicalTrialsGovAsync(study.NctId, cancellationToken).ConfigureAwait(false);
                    if (pmids.Count == 0) continue;

                    foreach (var pmid in pmids)
                    {
                        var existing = await context.PubmedStudies.AnyAsync(
                            p => p.StudyNctId == study.NctId && p.Pmid == pmid, cancellationToken);
                        if (existing) continue;

                        var paperDetail = await FetchPaperDetailAsync(pmid, cancellationToken).ConfigureAwait(false);

                        var pubmedStudy = new PubmedStudyEntity
                        {
                            StudyNctId = study.NctId,
                            Pmid = pmid,
                            Doi = paperDetail?.Doi,
                            Title = paperDetail?.Title,
                            Journal = paperDetail?.Journal,
                            PublicationDate = paperDetail?.PublicationDate,
                            Abstract = paperDetail?.Abstract,
                            IsNonEnglish = paperDetail?.IsNonEnglish ?? false,
                            CreatedAt = DateTime.UtcNow
                        };

                        context.PubmedStudies.Add(pubmedStudy);
                        totalPapers++;

                        if (paperDetail?.Authors != null)
                        {
                            foreach (var author in paperDetail.Authors)
                            {
                                context.StudyAuthors.Add(new StudyAuthorEntity
                                {
                                    StudyNctId = study.NctId,
                                    Pmid = pmid,
                                    LastName = author.LastName,
                                    ForeName = author.ForeName,
                                    Orcid = author.Orcid
                                });
                            }
                        }
                    }
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return totalPapers;
        }

        private static async Task<List<string>> FetchPmidsFromClinicalTrialsGovAsync(string nctId, CancellationToken cancellationToken)
        {
            var pmids = new List<string>();
            var url = $"https://clinicaltrials.gov/api/v2/studies/{nctId}";

            using var client = new HttpClient();
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return pmids;

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("protocolSection", out var ps) ||
                !ps.TryGetProperty("referencesModule", out var refModule) ||
                !refModule.TryGetProperty("references", out var references))
            {
                return pmids;
            }

            foreach (var reference in references.EnumerateArray())
            {
                if (reference.TryGetProperty("pmid", out var pmidEl) && pmidEl.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var pmid = pmidEl.GetString();
                    if (!string.IsNullOrWhiteSpace(pmid))
                    {
                        pmids.Add(pmid);
                    }
                }
            }

            return pmids;
        }

        private static async Task<PaperDetail?> FetchPaperDetailAsync(string pmid, CancellationToken cancellationToken)
        {
            var url = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?db=pubmed&id={pmid}&retmode=xml&rettype=abstract";

            using var client = new HttpClient();
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParsePubmedXml(xml);
        }

        private static PaperDetail? ParsePubmedXml(string xml)
        {
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(xml);

            var article = doc.SelectSingleNode("//PubmedArticle//Article");
            if (article == null) return null;

            var title = article.SelectSingleNode("ArticleTitle")?.InnerText;
            var journal = article.SelectSingleNode("Journal//Title")?.InnerText;
            var isNonEnglish = LanguageHelper.IsNonEnglish(title);
            var abstractText = article.SelectSingleNode("Abstract/AbstractText")?.InnerText;

            DateTime? pubDate = null;
            var pubDateNode = article.SelectSingleNode("Journal//JournalIssue//PubDate");
            if (pubDateNode != null)
            {
                var year = pubDateNode["Year"]?.InnerText;
                var month = pubDateNode["Month"]?.InnerText;
                var day = pubDateNode["Day"]?.InnerText;
                if (int.TryParse(year, out var y))
                {
                    var m = month switch
                    {
                        "Jan" => 1, "Feb" => 2, "Mar" => 3, "Apr" => 4,
                        "May" => 5, "Jun" => 6, "Jul" => 7, "Aug" => 8,
                        "Sep" => 9, "Oct" => 10, "Nov" => 11, "Dec" => 12,
                        _ => 1
                    };
                    var d = int.TryParse(day, out var dayVal) ? dayVal : 1;
                    pubDate = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);
                }
            }

            string? doi = null;
            var articleIdList = doc.SelectNodes("//ArticleIdList/ArticleId");
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
            var authorList = article.SelectSingleNode("AuthorList");
            if (authorList != null)
            {
                foreach (System.Xml.XmlNode authorNode in authorList.ChildNodes)
                {
                    if (authorNode.Name != "Author") continue;

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

        private class PaperDetail
        {
            public string? Title { get; set; }
            public string? Journal { get; set; }
            public DateTime? PublicationDate { get; set; }
            public string? Doi { get; set; }
            public string? Abstract { get; set; }
            public bool IsNonEnglish { get; set; }
            public List<AuthorInfo>? Authors { get; set; }
        }

        private class AuthorInfo
        {
            public string? LastName { get; set; }
            public string? ForeName { get; set; }
            public string? Orcid { get; set; }
        }
    }
}
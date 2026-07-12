using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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
            var repo = new StudyRepository(_connectionString);
            var nctIds = await repo.GetStudyNctIdsNeedingCrawlAsync("PubMed", int.MaxValue, cancellationToken).ConfigureAwait(false);
            var total = 0;

            foreach (var nctId in nctIds)
            {
                total += await ProcessStudyAsync(nctId, cancellationToken).ConfigureAwait(false);
            }

            return total;
        }

        public async Task<int> ProcessStudyAsync(string nctId, CancellationToken cancellationToken = default)
        {
            DbContextOptions<ClinicalTrialsContext> contextOptions = new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(_connectionString)
                .Options;

            using var context = new ClinicalTrialsContext(contextOptions);

            List<string> pmids = await FetchPmidsFromClinicalTrialsGovAsync(nctId, cancellationToken).ConfigureAwait(false);
            var newPapers = 0;

            foreach (var pmid in pmids)
            {
                var existing = await context.PubmedStudies.AnyAsync(
                    p => p.StudyNctId == nctId && p.Pmid == pmid, cancellationToken).ConfigureAwait(false);
                if (existing)
                {
                    continue;
                }

                PaperDetail? paperDetail = await FetchPaperDetailAsync(pmid, cancellationToken).ConfigureAwait(false);
                if (paperDetail == null)
                {
                    continue;
                }

                var pubmedStudy = new PubmedStudyEntity
                {
                    StudyNctId = nctId,
                    Pmid = pmid,
                    Doi = paperDetail.Doi,
                    Title = paperDetail.Title,
                    Journal = paperDetail.Journal,
                    PublicationDate = paperDetail.PublicationDate,
                    Abstract = paperDetail.Abstract,
                    IsNonEnglish = paperDetail.IsNonEnglish,
                    Url = new Uri($"https://pubmed.ncbi.nlm.nih.gov/{pmid}/"),
                    PublicationTypes = paperDetail.PublicationTypes,
                    MeSHTerms = paperDetail.MeSHTerms,
                    Keywords = paperDetail.Keywords,
                    CreatedAt = DateTime.UtcNow
                };

                context.PubmedStudies.Add(pubmedStudy);
                newPapers++;

                if (paperDetail.Authors != null)
                {
                    foreach (AuthorInfo author in paperDetail.Authors)
                    {
                        context.StudyAuthors.Add(new StudyAuthorEntity
                        {
                            StudyNctId = nctId,
                            Pmid = pmid,
                            LastName = author.LastName,
                            ForeName = author.ForeName,
                            Orcid = author.Orcid,
                            NcbiId = author.NcbiId
                        });
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await LinkAuthorsToInvestigatorsAsync(context, nctId, cancellationToken).ConfigureAwait(false);

            var repo = new StudyRepository(_connectionString);
            await repo.UpdateStudyCrawlTimestampAsync(nctId, "PubMed", cancellationToken).ConfigureAwait(false);

            return newPapers;
        }

        private static async Task LinkAuthorsToInvestigatorsAsync(ClinicalTrialsContext context, string nctId, CancellationToken cancellationToken)
        {
            List<StudyAuthorEntity> authors = await context.StudyAuthors
                .Where(a => a.StudyNctId == nctId && a.InvestigatorUuid == null)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (authors.Count == 0)
            {
                return;
            }

            List<InvestigatorEntity> investigators = await context.Investigators
                .Where(i => i.StudyNctId == nctId)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (StudyAuthorEntity author in authors)
            {
                if (!string.IsNullOrWhiteSpace(author.Orcid))
                {
                    InvestigatorEntity? match = investigators.FirstOrDefault(i =>
                        !string.IsNullOrWhiteSpace(i.Name) &&
                        i.Name.Contains(author.LastName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        author.InvestigatorUuid = match.Uuid;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(author.NcbiId))
                {
                    InvestigatorEntity? match = investigators.FirstOrDefault(i =>
                        !string.IsNullOrWhiteSpace(i.Name) &&
                        i.Name.Contains(author.LastName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        author.InvestigatorUuid = match.Uuid;
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<List<string>> FetchPmidsFromClinicalTrialsGovAsync(string nctId, CancellationToken cancellationToken)
        {
            var pmids = new List<string>();
            var url = $"https://clinicaltrials.gov/api/v2/studies/{nctId}";

            HttpResponseMessage response = await _httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return pmids;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("protocolSection", out JsonElement ps) ||
                !ps.TryGetProperty("referencesModule", out JsonElement refModule) ||
                !refModule.TryGetProperty("references", out JsonElement references))
            {
                return pmids;
            }

            foreach (JsonElement reference in references.EnumerateArray())
            {
                if (reference.TryGetProperty("pmid", out JsonElement pmidEl) && pmidEl.ValueKind == System.Text.Json.JsonValueKind.String)
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

        internal static PaperDetail? ParsePubmedXml(string xml)
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
                    var foreName = authorNode["ForeName"]?.InnerText;

                    string? orcid = null;
                    string? ncbiId = null;
                    if (authorNode.SelectNodes("Identifier") is XmlNodeList identifiers)
                    {
                        foreach (XmlNode idNode in identifiers)
                        {
                            var source = idNode.Attributes?["Source"]?.Value;
                            if (string.Equals(source, "ORCID", StringComparison.OrdinalIgnoreCase))
                            {
                                orcid = idNode.InnerText;
                            }
                            else if (string.Equals(source, "NCBI", StringComparison.OrdinalIgnoreCase))
                            {
                                ncbiId = idNode.InnerText;
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(orcid) && string.IsNullOrWhiteSpace(ncbiId))
                    {
                        continue;
                    }

                    authors.Add(new AuthorInfo
                    {
                        LastName = lastName,
                        ForeName = foreName,
                        Orcid = orcid,
                        NcbiId = ncbiId
                    });
                }
            }

            string? publicationTypes = null;
            XmlNode? publicationTypeList = article.SelectSingleNode("PublicationTypeList");
            if (publicationTypeList != null)
            {
                var types = new List<string>();
                foreach (XmlNode ptNode in publicationTypeList.ChildNodes)
                {
                    if (ptNode.Name == "PublicationType")
                    {
                        types.Add(ptNode.InnerText);
                    }
                }
                if (types.Count > 0)
                {
                    publicationTypes = string.Join(", ", types);
                }
            }

            string? meshTerms = null;
            XmlNode? meshHeadingList = article.SelectSingleNode("MeshHeadingList");
            if (meshHeadingList != null)
            {
                var terms = new List<string>();
                foreach (XmlNode heading in meshHeadingList.ChildNodes)
                {
                    if (heading.Name != "MeshHeading")
                    {
                        continue;
                    }

                    var descriptor = heading.SelectSingleNode("DescriptorName")?.InnerText;
                    if (string.IsNullOrWhiteSpace(descriptor))
                    {
                        continue;
                    }

                    var qualifiers = new List<string>();
                    if (heading.SelectNodes("QualifierName") is XmlNodeList qualifierNodes)
                    {
                        foreach (XmlNode q in qualifierNodes)
                        {
                            var qName = q.InnerText;
                            if (!string.IsNullOrWhiteSpace(qName))
                            {
                                qualifiers.Add(qName);
                            }
                        }
                    }

                    terms.Add(qualifiers.Count > 0
                        ? $"{descriptor}/{string.Join(", ", qualifiers)}"
                        : descriptor);
                }
                if (terms.Count > 0)
                {
                    meshTerms = string.Join(", ", terms);
                }
            }

            string? keywords = null;
            XmlNode? keywordList = article.SelectSingleNode("KeywordList");
            if (keywordList != null)
            {
                var kwList = new List<string>();
                foreach (XmlNode kwNode in keywordList.ChildNodes)
                {
                    if (kwNode.Name == "Keyword")
                    {
                        var kw = kwNode.InnerText;
                        if (!string.IsNullOrWhiteSpace(kw))
                        {
                            kwList.Add(kw);
                        }
                    }
                }
                if (kwList.Count > 0)
                {
                    keywords = string.Join(", ", kwList);
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
                Authors = authors.Count > 0 ? authors : null,
                PublicationTypes = publicationTypes,
                MeSHTerms = meshTerms,
                Keywords = keywords
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
            public string? PublicationTypes { get; set; }
            public string? MeSHTerms { get; set; }
            public string? Keywords { get; set; }
        }

        internal sealed class AuthorInfo
        {
            public string? LastName { get; set; }
            public string? ForeName { get; set; }
            public string? Orcid { get; set; }
            public string? NcbiId { get; set; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using Scrapers.Persistence.Entities;

namespace Scrapers.Services
{
    public class PubMedClient : IPubMedClient
    {
        private readonly HttpClient _httpClient;

        public PubMedClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<List<PubMedPaper>> GetPapersForInvestigatorAsync(InvestigatorEntity investigator, CancellationToken cancellationToken)
        {
            if (investigator == null) throw new ArgumentNullException(nameof(investigator));

            var authorQuery = Uri.EscapeDataString(investigator.Name);
            var pmidList = await GetPmidsByAuthorAsync(authorQuery, cancellationToken);
            if (pmidList.Count == 0) return new List<PubMedPaper>();

            var papers = await GetPaperDetailsAsync(pmidList, cancellationToken);

            var filteredPapers = papers.Where(p => p.Authors.Any(a => a.Equals(investigator.Name, StringComparison.OrdinalIgnoreCase))).ToList();

            return filteredPapers.Select(p => new PubMedPaper(
                p.Title,
                $"https://pubmed.ncbi.nlm.nih.gov/{p.Pmid}/",
                p.NcbiId,
                p.OrcidId)).ToList();
        }

        private async Task<List<string>> GetPmidsByAuthorAsync(string authorQuery, CancellationToken cancellationToken)
        {
            var esearchUrl = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esearch.fcgi?db=pubmed&term={authorQuery}[Author]&retmax=100";
            var response = await SendWithRetryAsync(() => _httpClient.GetStringAsync(esearchUrl, cancellationToken));
            var xml = XDocument.Parse(response);
            var pmidElements = xml.Descendants("Id");
            return pmidElements.Select(x => x.Value).ToList();
        }

        private async Task<List<PaperDetail>> GetPaperDetailsAsync(List<string> pmids, CancellationToken cancellationToken)
        {
            var papers = new List<PaperDetail>();
            const int batchSize = 20;
            for (int i = 0; i < pmids.Count; i += batchSize)
            {
                var batchPmids = pmids.Skip(i).Take(batchSize);
                var idString = string.Join(",", batchPmids);
                var esummaryUrl = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esummary.fcgi?db=pubmed&id={idString}";

                var response = await SendWithRetryAsync(() => _httpClient.GetStringAsync(esummaryUrl, cancellationToken));
                var xml = XDocument.Parse(response);
                var docSums = xml.Descendants("DocSum");

                foreach (var docSum in docSums)
                {
                    var pmid = docSum.Elements("Id").FirstOrDefault()?.Value ?? string.Empty;
                    var title = docSum.Descendants("Item").FirstOrDefault(i => i.Attribute("Name")?.Value == "Title")?.Value ?? string.Empty;

                    var authors = docSum.Descendants("Item")
                        .FirstOrDefault(i => i.Attribute("Name")?.Value == "AuthorList")?
                        .Elements("Item").Select(a => a.Value).ToList() ?? new List<string>();

                    string ncbiId = string.Empty;
                    string orcidId = string.Empty;

                    var itemElements = docSum.Elements("Item");
                    var orcidElement = itemElements.FirstOrDefault(i => i.Attribute("Name")?.Value == "ORCID");
                    if (orcidElement != null)
                    {
                        orcidId = orcidElement.Value;
                    }

                    papers.Add(new PaperDetail
                    {
                        Pmid = pmid,
                        Title = title,
                        Authors = authors,
                        NcbiId = ncbiId,
                        OrcidId = orcidId
                    });
                }
            }

            return papers;
        }

        private async Task<string> SendWithRetryAsync(Func<Task<string>> action, int maxRetries = 3, int delayMs = 1000)
        {
            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    return await action();
                }
                catch (HttpRequestException ex) when (i < maxRetries && ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    await Task.Delay(delayMs * (int)Math.Pow(2, i));
                }
            }

            return await action();
        }

        private class PaperDetail
        {
            public string Pmid { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public List<string> Authors { get; set; } = new List<string>();
            public string NcbiId { get; set; } = string.Empty;
            public string OrcidId { get; set; } = string.Empty;
        }
    }

    public record PubMedPaper(string Title, string Url, string? NcbiId = null, string? OrcidId = null);

    public interface IPubMedClient
    {
        Task<List<PubMedPaper>> GetPapersForInvestigatorAsync(InvestigatorEntity investigator, CancellationToken cancellationToken);
    }
}

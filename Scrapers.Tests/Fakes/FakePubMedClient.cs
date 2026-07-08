using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scrapers.Persistence.Entities;
using Scrapers.Services;

namespace Scrapers.Tests.Fakes
{
    public class FakePubMedClient : IPubMedClient
    {
        public Task<List<PubMedPaper>> GetPapersForInvestigatorAsync(InvestigatorEntity investigator, CancellationToken cancellationToken)
        {
            var samplePapers = new List<PubMedPaper>
            {
                new PubMedPaper("Clinical trial of cancer drug therapy", "https://pubmed.ncbi.nlm.nih.gov/12345/", "ncbi123", "orcid123"),
                new PubMedPaper("Therapy effects on cancerous cells", "https://pubmed.ncbi.nlm.nih.gov/67890/"),
            };

            return Task.FromResult(samplePapers);
        }
    }
}

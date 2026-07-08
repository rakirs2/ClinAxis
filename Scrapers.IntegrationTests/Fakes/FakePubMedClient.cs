using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scrapers.Persistence.Entities;
using Scrapers.Services;

namespace Scrapers.IntegrationTests.Fakes
{
    public class FakePubMedClient : IPubMedClient
    {
        private readonly List<PubMedPaper> _papers;

        public FakePubMedClient()
        {
            _papers =
            [
                new PubMedPaper("Clinical trial of cancer drug therapy", "https://pubmed.ncbi.nlm.nih.gov/11111/", "ncbi1", "orcid1"),
                new PubMedPaper("Therapy effects on cancerous cells", "https://pubmed.ncbi.nlm.nih.gov/22222/"),
                new PubMedPaper("Oncology clinical outcomes study", "https://pubmed.ncbi.nlm.nih.gov/33333/", "ncbi3"),
            ];
        }

        public FakePubMedClient(List<PubMedPaper> papers)
        {
            _papers = papers;
        }

        public Task<List<PubMedPaper>> GetPapersForInvestigatorAsync(InvestigatorEntity investigator, CancellationToken cancellationToken)
        {
            return Task.FromResult(_papers);
        }
    }
}

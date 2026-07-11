using System.Threading;
using System.Threading.Tasks;
using Scrapers.Services;

namespace Scrapers.Coordinators
{
    public class IngestionCoordinatorService
    {
        private readonly ClinicalTrialsIngestionService _clinicalTrialsService;
        private readonly PubMedScraperService _pubMedScraperService;

        public IngestionCoordinatorService(ClinicalTrialsIngestionService clinicalTrialsService, PubMedScraperService pubMedScraperService)
        {
            _clinicalTrialsService = clinicalTrialsService;
            _pubMedScraperService = pubMedScraperService;
        }

        public async Task FullScraperPipelineAsync(int clinicalTrialsCount = 10, CancellationToken cancellationToken = default)
        {
            await _clinicalTrialsService.IngestAsync(clinicalTrialsCount, cancellationToken).ConfigureAwait(false);
            await _pubMedScraperService.IngestPubMedPapersAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence;

namespace Scrapers.Services;

public class ClinicalTrialsIngestionService
{
    private readonly ClinicalTrialsGov _client;
    private readonly StudyRepository _repository;

    public ClinicalTrialsIngestionService(ClinicalTrialsGov client, StudyRepository repository)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<int> IngestAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return 0;
        }

        await _repository.MigrateSchemaAsync(cancellationToken).ConfigureAwait(false);

        var totalIngested = 0;

        await _client.GetTrialRecordsBatchedAsync(count, async batch =>
        {
            var ingested = await _repository.UpdateStudiesWithClinicalTrialsAsync(batch, cancellationToken).ConfigureAwait(false);
            totalIngested += ingested;
        }, lastUpdatedPost: null, cancellationToken: cancellationToken).ConfigureAwait(false);

        return totalIngested;
    }
}

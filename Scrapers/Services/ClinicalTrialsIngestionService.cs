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

        await _repository.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var records = await _client.GetTrialRecordsAsync(count, cancellationToken).ConfigureAwait(false);
        return await _repository.UpsertStudiesAsync(records, cancellationToken).ConfigureAwait(false);
    }
}

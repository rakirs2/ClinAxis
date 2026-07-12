using Scrapers.Persistence;

namespace Scrapers.Services.Cms;

public sealed class EnrichmentPipeline
{
    private readonly NppesNpiRegistryClient _nppesClient;
    private readonly OrcidApiClient _orcidClient;
    private readonly StudyRepository _repository;

    public EnrichmentPipeline(NppesNpiRegistryClient nppesClient, OrcidApiClient orcidClient, StudyRepository repository)
    {
        _nppesClient = nppesClient ?? throw new ArgumentNullException(nameof(nppesClient));
        _orcidClient = orcidClient ?? throw new ArgumentNullException(nameof(orcidClient));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Func<IReadOnlyList<(Guid Uuid, string Name, string? Affiliation)>,
         Task<Dictionary<Guid, (string? Npi, string? Orcid)>>> CreateEnrichmentCallback()
    {
        return async persons =>
        {
            var results = new Dictionary<Guid, (string? Npi, string? Orcid)>();
            foreach (var (uuid, name, affiliation) in persons)
            {
                var (firstName, lastName) = ParseName(name);
                var npi = !string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(lastName)
                    ? await _nppesClient.LookupNpiAsync(firstName, lastName).ConfigureAwait(false)
                    : null;
                var orcid = !string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(lastName)
                    ? await _orcidClient.LookupOrcidAsync(firstName, lastName).ConfigureAwait(false)
                    : null;
                results[uuid] = (npi, orcid);
            }
            return results;
        };
    }

    public static (string FirstName, string LastName) ParseName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return ("", "");

        var namePart = fullName.Split(',')[0].Trim();
        var parts = namePart.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return ("", "");

        var lastName = parts[^1];
        var firstName = parts[0];

        if (firstName.Length <= 1 && parts.Length > 2)
            firstName = parts[1];

        return (firstName, lastName);
    }
}

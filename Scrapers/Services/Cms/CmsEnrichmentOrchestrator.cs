using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Services.Cms;

public sealed class CmsEnrichmentOrchestrator
{
    private readonly string _connectionString;

    public CmsEnrichmentOrchestrator(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<int> ImportAndMatchProvidersAsync(IReadOnlyList<CmsProviderEntity> providers, CancellationToken ct = default)
    {
        var repo = new StudyRepository(_connectionString);
        var imported = await repo.UpsertCmsProvidersAsync(providers, ct).ConfigureAwait(false);

        var matched = 0;
        using var context = CreateContext();
        foreach (var provider in providers)
        {
            if (provider.Uuid != Guid.Empty)
                continue;

            var investigator = await context.Investigators
                .Where(i => i.Npi == provider.Npi)
                .Select(i => new { i.Uuid })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (investigator is null)
                continue;

            var existing = await context.CmsProviders
                .FirstOrDefaultAsync(p => p.Npi == provider.Npi, ct)
                .ConfigureAwait(false);

            if (existing is not null && existing.Uuid == Guid.Empty)
            {
                existing.Uuid = investigator.Uuid;
                matched++;
            }
        }

        if (matched > 0)
        {
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return imported;
    }

    private ClinicalTrialsContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new ClinicalTrialsContext(options);
    }
}

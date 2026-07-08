using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Scrapers.Models.ClinicalTrialsGov;
using Scrapers.Persistence.Entities;

namespace Scrapers.Persistence;

public class StudyRepository
{
    private readonly DbContextOptions<ClinicalTrialsContext> _options;

    public StudyRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must be provided.", nameof(connectionString));
        }

        var builder = new DbContextOptionsBuilder<ClinicalTrialsContext>();
        builder.UseNpgsql(connectionString);
        _options = builder.Options;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> UpsertStudiesAsync(IEnumerable<ClinicalTrialRecord> records, CancellationToken cancellationToken = default)
    {
        var recordList = records.ToList();
        if (recordList.Count == 0)
        {
            return 0;
        }

        await using var context = CreateContext();
        foreach (var record in recordList)
        {
            if (string.IsNullOrWhiteSpace(record.Summary.NctId))
            {
                continue;
            }

            var entity = await context.Studies
                .Include(s => s.Investigators)
                .FirstOrDefaultAsync(s => s.NctId == record.Summary.NctId, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
            {
                entity = new StudyEntity
                {
                    NctId = record.Summary.NctId!,
                    BriefTitle = record.Summary.BriefTitle,
                    OverallStatus = record.Summary.OverallStatus,
                    CreatedAt = DateTime.UtcNow,
                };
                context.Studies.Add(entity);
            }
            else
            {
                entity.BriefTitle = record.Summary.BriefTitle;
                entity.OverallStatus = record.Summary.OverallStatus;
            }

            entity.Investigators.Clear();
            foreach (var investigator in record.Investigators.Where(i => i.HasName))
            {
                entity.Investigators.Add(new InvestigatorEntity
                {
                    Name = investigator.Name!,
                    Affiliation = investigator.Affiliation,
                    Role = investigator.Role
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return recordList.Count;
    }

    public async Task<int> CountStudiesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Studies.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountInvestigatorsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Investigators.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.Investigators.RemoveRange(context.Investigators);
        context.Studies.RemoveRange(context.Studies);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private ClinicalTrialsContext CreateContext() => new(_options);
}

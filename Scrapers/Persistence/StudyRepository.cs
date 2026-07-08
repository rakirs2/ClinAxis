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

public async Task<int> UpdateStudiesWithClinicalTrialsAsync(IEnumerable<ClinicalTrialRecord> records, CancellationToken cancellationToken = default)
        {
            if (records == null || !records.Any())
            {
                return 0;
            }

            var recordList = records.ToList();

            await using var context = CreateContext();
            foreach (var record in recordList)
            {
                if (record == null) throw new ArgumentNullException(nameof(record), "Record is null.");
                if (record.Summary == null) throw new ArgumentNullException(nameof(record.Summary), "Record Summary is null.");
                if (string.IsNullOrWhiteSpace(record.Summary.NctId)) throw new ArgumentException("Record Summary NctId is null or whitespace.");

                bool incomplete = false;

                if (record.Investigators == null || record.Investigators.Count == 0 || record.Investigators.Any(i => i == null || string.IsNullOrWhiteSpace(i?.Name)))
                {
                    incomplete = true;
                }

                var entity = await context.Studies
                    .Include(s => s.Investigators)
                    .FirstOrDefaultAsync(s => s.NctId == record.Summary.NctId, cancellationToken)
                    .ConfigureAwait(false);

                if (entity == null)
                {
                    entity = new StudyEntity
                    {
                        NctId = record.Summary.NctId!,
                        BriefTitle = record.Summary.BriefTitle,
                        OverallStatus = record.Summary.OverallStatus,
                        CreatedAt = DateTime.UtcNow,
                        IsIncomplete = incomplete,
                        Investigators = new List<InvestigatorEntity>()
                    };
                    context.Studies.Add(entity);
                }
                else
                {
                    entity.BriefTitle = record.Summary.BriefTitle;
                    entity.OverallStatus = record.Summary.OverallStatus;
                    entity.IsIncomplete = incomplete;
                }

                if (incomplete)
                {
                    // Skip adding investigators if study is incomplete
                    continue;
                }

                entity.Investigators!.Clear();

                foreach (var investigator in record.Investigators!)
                {
                    entity.Investigators.Add(new InvestigatorEntity
                    {
                        Name = investigator!.Name!,
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

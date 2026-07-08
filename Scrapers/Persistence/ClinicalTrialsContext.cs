using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence.Entities;

namespace Scrapers.Persistence;

public class ClinicalTrialsContext : DbContext
{
    public ClinicalTrialsContext(DbContextOptions<ClinicalTrialsContext> options) : base(options) {}

    public DbSet<StudyEntity> Studies => Set<StudyEntity>();
    public DbSet<InvestigatorEntity> Investigators => Set<InvestigatorEntity>();
    public DbSet<PubmedStudyEntity> PubmedStudies => Set<PubmedStudyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StudyEntity>(entity =>
        {
            entity.ToTable("studies");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NctId).IsRequired();
            entity.HasIndex(e => e.NctId).IsUnique();
            entity.Property(e => e.BriefTitle);
            entity.Property(e => e.OverallStatus);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            entity.Property(e => e.IsIncomplete).HasDefaultValue(false);
        });

        modelBuilder.Entity<InvestigatorEntity>(entity =>
        {
            entity.ToTable("investigators");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.OrcidId);
            entity.Property(e => e.NcbiId);
            entity.Property(e => e.LastSuccessfulPubmedCrawl);
            entity.HasOne(e => e.Study)
                .WithMany(s => s.Investigators)
                .HasForeignKey(e => e.StudyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.PubmedStudies)
                .WithOne(p => p.Investigator)
                .HasForeignKey(p => p.InvestigatorId);
        });

        modelBuilder.Entity<PubmedStudyEntity>(entity =>
        {
            entity.ToTable("pubmed_studies");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Url);
            entity.Property(e => e.Keywords);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
        });
    }
}

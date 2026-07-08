using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence.Entities;

namespace Scrapers.Persistence;

public class ClinicalTrialsContext : DbContext
{
    public ClinicalTrialsContext(DbContextOptions<ClinicalTrialsContext> options) : base(options)
    {
    }

    public DbSet<StudyEntity> Studies => Set<StudyEntity>();
    public DbSet<InvestigatorEntity> Investigators => Set<InvestigatorEntity>();

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
        });

        modelBuilder.Entity<InvestigatorEntity>(entity =>
        {
            entity.ToTable("investigators");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasOne(e => e.Study)
                .WithMany(s => s.Investigators)
                .HasForeignKey(e => e.StudyId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

using System;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence.Entities;

namespace Scrapers.Persistence
{
    public class ClinicalTrialsContext : DbContext
    {
        public DbSet<StudyEntity> Studies => Set<StudyEntity>();
        public DbSet<InvestigatorEntity> Investigators => Set<InvestigatorEntity>();
        public DbSet<PubmedStudyEntity> PubmedStudies => Set<PubmedStudyEntity>();
        public DbSet<StudyKeywordEntity> StudyKeywords => Set<StudyKeywordEntity>();
        public DbSet<StudyConditionEntity> StudyConditions => Set<StudyConditionEntity>();
        public DbSet<StudyPhaseEntity> StudyPhases => Set<StudyPhaseEntity>();
        public DbSet<StudyAuthorEntity> StudyAuthors => Set<StudyAuthorEntity>();
        public DbSet<PipelineRunEntity> PipelineRuns => Set<PipelineRunEntity>();
        public DbSet<PiAggregationEntity> PiAggregations => Set<PiAggregationEntity>();
        public DbSet<CategoryAggregationEntity> CategoryAggregations => Set<CategoryAggregationEntity>();

        public ClinicalTrialsContext(DbContextOptions<ClinicalTrialsContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<StudyEntity>(entity =>
            {
                entity.ToTable("studies");
                entity.HasKey(e => e.NctId);
                entity.Property(e => e.NctId).HasColumnName("nct_id").HasMaxLength(20);
                entity.Property(e => e.BriefTitle).HasColumnName("brief_title");
                entity.Property(e => e.OfficialTitle).HasColumnName("official_title");
                entity.Property(e => e.OverallStatus).HasColumnName("overall_status");
                entity.Property(e => e.StudyType).HasColumnName("study_type");
                entity.Property(e => e.BriefSummary).HasColumnName("brief_summary");
                entity.Property(e => e.PrimaryPurpose).HasColumnName("primary_purpose");
                entity.Property(e => e.InterventionModel).HasColumnName("intervention_model");
                entity.Property(e => e.Allocation).HasColumnName("allocation");
                entity.Property(e => e.EnrollmentCount).HasColumnName("enrollment_count");
                entity.Property(e => e.Sex).HasColumnName("sex");
                entity.Property(e => e.MinimumAge).HasColumnName("minimum_age");
                entity.Property(e => e.MaximumAge).HasColumnName("maximum_age");
                entity.Property(e => e.StartDate).HasColumnName("start_date");
                entity.Property(e => e.CompletionDate).HasColumnName("completion_date");
                entity.Property(e => e.StudyFirstPostDate).HasColumnName("study_first_post_date");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.IsIncomplete).HasColumnName("is_incomplete");
            });

            modelBuilder.Entity<InvestigatorEntity>(entity =>
            {
                entity.ToTable("investigators");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Role).HasColumnName("role");
                entity.Property(e => e.Affiliation).HasColumnName("affiliation");

                entity.HasOne(e => e.Study)
                    .WithMany(s => s.Investigators)
                    .HasForeignKey(e => e.StudyNctId)
                    .HasPrincipalKey(s => s.NctId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PubmedStudyEntity>(entity =>
            {
                entity.ToTable("pubmed_studies");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.Pmid).HasColumnName("pmid").HasMaxLength(20);
                entity.Property(e => e.Doi).HasColumnName("doi");
                entity.Property(e => e.Title).HasColumnName("title");
                entity.Property(e => e.Journal).HasColumnName("journal");
                entity.Property(e => e.PublicationDate).HasColumnName("publication_date");
                entity.Property(e => e.Abstract).HasColumnName("abstract");
                entity.Property(e => e.IsNonEnglish).HasColumnName("is_non_english");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");

                entity.HasOne(e => e.Study)
                    .WithMany(s => s.PubmedStudies)
                    .HasForeignKey(e => e.StudyNctId)
                    .HasPrincipalKey(s => s.NctId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.StudyNctId, e.Pmid }).IsUnique();
            });

            modelBuilder.Entity<StudyKeywordEntity>(entity =>
            {
                entity.ToTable("study_keywords");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.Keyword).HasColumnName("keyword");

                entity.HasOne(e => e.Study)
                    .WithMany(s => s.Keywords)
                    .HasForeignKey(e => e.StudyNctId)
                    .HasPrincipalKey(s => s.NctId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.Keyword);
            });

            modelBuilder.Entity<StudyConditionEntity>(entity =>
            {
                entity.ToTable("study_conditions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.Condition).HasColumnName("condition");

                entity.HasOne(e => e.Study)
                    .WithMany(s => s.Conditions)
                    .HasForeignKey(e => e.StudyNctId)
                    .HasPrincipalKey(s => s.NctId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.Condition);
            });

            modelBuilder.Entity<StudyPhaseEntity>(entity =>
            {
                entity.ToTable("study_phases");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.Phase).HasColumnName("phase");

                entity.HasOne(e => e.Study)
                    .WithMany(s => s.Phases)
                    .HasForeignKey(e => e.StudyNctId)
                    .HasPrincipalKey(s => s.NctId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<StudyAuthorEntity>(entity =>
            {
                entity.ToTable("study_authors");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.Pmid).HasColumnName("pmid").HasMaxLength(20);
                entity.Property(e => e.LastName).HasColumnName("last_name");
                entity.Property(e => e.ForeName).HasColumnName("fore_name");
                entity.Property(e => e.Orcid).HasColumnName("orcid");

                entity.HasOne(e => e.Study)
                    .WithMany(s => s.Authors)
                    .HasForeignKey(e => e.StudyNctId)
                    .HasPrincipalKey(s => s.NctId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.LastName);
                entity.HasIndex(e => e.Orcid);
            });

            modelBuilder.Entity<PipelineRunEntity>(entity =>
            {
                entity.ToTable("pipeline_runs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StartedAt).HasColumnName("started_at");
                entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20);
                entity.Property(e => e.TotalStudies).HasColumnName("total_studies");
                entity.Property(e => e.TotalInvestigators).HasColumnName("total_investigators");
                entity.Property(e => e.TotalPubmedPapers).HasColumnName("total_pubmed_papers");
                entity.Property(e => e.TotalKeywords).HasColumnName("total_keywords");
                entity.Property(e => e.TotalAuthors).HasColumnName("total_authors");
                entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            });

            modelBuilder.Entity<PiAggregationEntity>(entity =>
            {
                entity.ToTable("pi_aggregations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.InvestigatorName).HasColumnName("investigator_name").HasMaxLength(200);
                entity.Property(e => e.Affiliation).HasColumnName("affiliation");
                entity.Property(e => e.StudyCount).HasColumnName("study_count");
                entity.Property(e => e.PubmedPaperCount).HasColumnName("pubmed_paper_count");
                entity.Property(e => e.StudyNctIds).HasColumnName("study_nct_ids");
                entity.Property(e => e.ComputedAt).HasColumnName("computed_at");

                entity.HasIndex(e => e.InvestigatorName);
            });

            modelBuilder.Entity<CategoryAggregationEntity>(entity =>
            {
                entity.ToTable("category_aggregations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.CategoryName).HasColumnName("category_name").HasMaxLength(200);
                entity.Property(e => e.CategoryType).HasColumnName("category_type").HasMaxLength(50);
                entity.Property(e => e.StudyCount).HasColumnName("study_count");
                entity.Property(e => e.PubmedPaperCount).HasColumnName("pubmed_paper_count");
                entity.Property(e => e.StudyNctIds).HasColumnName("study_nct_ids");
                entity.Property(e => e.ComputedAt).HasColumnName("computed_at");

                entity.HasIndex(e => e.CategoryName);
                entity.HasIndex(e => e.CategoryType);
            });
        }
    }
}

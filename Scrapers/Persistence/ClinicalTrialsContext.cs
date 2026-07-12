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
        public DbSet<StudyLocationEntity> StudyLocations => Set<StudyLocationEntity>();
        public DbSet<StudyPhaseEntity> StudyPhases => Set<StudyPhaseEntity>();
        public DbSet<StudyAuthorEntity> StudyAuthors => Set<StudyAuthorEntity>();
        public DbSet<PipelineRunEntity> PipelineRuns => Set<PipelineRunEntity>();
        public DbSet<PiAggregationEntity> PiAggregations => Set<PiAggregationEntity>();
        public DbSet<CategoryAggregationEntity> CategoryAggregations => Set<CategoryAggregationEntity>();
        public DbSet<ScrapeEventEntity> ScrapeEvents => Set<ScrapeEventEntity>();
        public DbSet<PipelineEventEntity> PipelineEvents => Set<PipelineEventEntity>();
        public DbSet<DataSourceStateEntity> DataSourceStates => Set<DataSourceStateEntity>();
        public DbSet<SourceFetchHistoryEntity> SourceFetchHistories => Set<SourceFetchHistoryEntity>();
        public DbSet<ScraperPivotEntity> ScraperPivots => Set<ScraperPivotEntity>();
        public DbSet<CmsProviderEntity> CmsProviders => Set<CmsProviderEntity>();

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
                entity.Property(e => e.Uuid).HasColumnName("uuid");
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Role).HasColumnName("role");
                entity.Property(e => e.Affiliation).HasColumnName("affiliation");
                entity.Property(e => e.Npi).HasColumnName("npi").HasMaxLength(10);
                entity.Property(e => e.NpiLookupAt).HasColumnName("npi_lookup_at");
                entity.Property(e => e.Orcid).HasColumnName("orcid").HasMaxLength(19);
                entity.Property(e => e.OrcidLookupAt).HasColumnName("orcid_lookup_at");

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

            modelBuilder.Entity<StudyLocationEntity>(entity =>
            {
                entity.ToTable("study_locations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.Facility).HasColumnName("facility");
                entity.Property(e => e.City).HasColumnName("city");
                entity.Property(e => e.State).HasColumnName("state");
                entity.Property(e => e.Country).HasColumnName("country");

                entity.HasOne(e => e.Study)
                    .WithMany(s => s.Locations)
                    .HasForeignKey(e => e.StudyNctId)
                    .HasPrincipalKey(s => s.NctId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.Country);
                entity.HasIndex(e => e.State);
                entity.HasIndex(e => e.City);
                entity.HasIndex(e => e.Facility);
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

            modelBuilder.Entity<ScrapeEventEntity>(entity =>
            {
                entity.ToTable("scrape_events");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.PipelineRunId).HasColumnName("pipeline_run_id");
                entity.Property(e => e.Timestamp).HasColumnName("timestamp");
                entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);
                entity.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(50);
                entity.Property(e => e.Level).HasColumnName("level").HasMaxLength(20);
                entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
                entity.Property(e => e.RecordsAffected).HasColumnName("records_affected");
                entity.Property(e => e.Message).HasColumnName("message");
                entity.Property(e => e.HttpStatusCode).HasColumnName("http_status_code");

                entity.HasOne(e => e.PipelineRun)
                    .WithMany()
                    .HasForeignKey(e => e.PipelineRunId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.Source);
                entity.HasIndex(e => e.EventType);
            });

            modelBuilder.Entity<PipelineEventEntity>(entity =>
            {
                entity.ToTable("pipeline_events");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(100);
                entity.Property(e => e.Data).HasColumnName("data");
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20);
                entity.Property(e => e.ClaimedBy).HasColumnName("claimed_by").HasMaxLength(100);
                entity.Property(e => e.ClaimedAt).HasColumnName("claimed_at");
                entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
                entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
                entity.Property(e => e.RetryCount).HasColumnName("retry_count");
                entity.Property(e => e.LastErrorAt).HasColumnName("last_error_at");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.EventType);
                entity.HasIndex(e => e.CreatedAt);
            });

            modelBuilder.Entity<DataSourceStateEntity>(entity =>
            {
                entity.ToTable("data_source_state");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.SourceName).HasColumnName("source_name").HasMaxLength(100);
                entity.Property(e => e.LastSyncTimestamp).HasColumnName("last_sync_timestamp");
                entity.Property(e => e.LastSyncHash).HasColumnName("last_sync_hash").HasMaxLength(255);
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
                entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => e.SourceName).IsUnique();
            });

            modelBuilder.Entity<SourceFetchHistoryEntity>(entity =>
            {
                entity.ToTable("source_fetch_history");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.SourceType).HasColumnName("source_type").HasMaxLength(100);
                entity.Property(e => e.LastFetchTimestamp).HasColumnName("last_fetch_timestamp");
                entity.Property(e => e.ContentHash).HasColumnName("content_hash").HasMaxLength(255);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => new { e.StudyNctId, e.SourceType }).IsUnique();
                entity.HasIndex(e => e.StudyNctId);
            });

            modelBuilder.Entity<ScraperPivotEntity>(entity =>
            {
                entity.ToTable("scraper_pivots");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100);
                entity.Property(e => e.ServiceType).HasColumnName("service_type");
                entity.Property(e => e.Enabled).HasColumnName("enabled");
                entity.Property(e => e.CacheTtlDays).HasColumnName("cache_ttl_days");
                entity.Property(e => e.BatchSize).HasColumnName("batch_size");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<CmsProviderEntity>(entity =>
            {
                entity.ToTable("cms_providers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.Uuid).HasColumnName("uuid");
                entity.Property(e => e.Npi).HasColumnName("npi").HasMaxLength(10);
                entity.Property(e => e.ProviderName).HasColumnName("provider_name");
                entity.Property(e => e.Gender).HasColumnName("gender").HasMaxLength(10);
                entity.Property(e => e.Credential).HasColumnName("credential").HasMaxLength(20);
                entity.Property(e => e.MedicalSchoolName).HasColumnName("medical_school_name");
                entity.Property(e => e.GraduationYear).HasColumnName("graduation_year");
                entity.Property(e => e.PrimarySpecialty).HasColumnName("primary_specialty");
                entity.Property(e => e.SecondarySpecialty).HasColumnName("secondary_specialty");
                entity.Property(e => e.OrganizationLegalName).HasColumnName("organization_legal_name");
                entity.Property(e => e.PracticeAddressCity).HasColumnName("practice_address_city");
                entity.Property(e => e.PracticeAddressState).HasColumnName("practice_address_state").HasMaxLength(2);
                entity.Property(e => e.PracticeAddressZip).HasColumnName("practice_address_zip").HasMaxLength(10);
                entity.Property(e => e.MedicareParticipation).HasColumnName("medicare_participation").HasMaxLength(20);
                entity.Property(e => e.TotalMedicareServices).HasColumnName("total_medicare_services");
                entity.Property(e => e.TotalMedicarePayments).HasColumnName("total_medicare_payments");
                entity.Property(e => e.TotalMedicareBeneficiaries).HasColumnName("total_medicare_beneficiaries");

                entity.HasIndex(e => e.Npi).IsUnique();
                entity.HasIndex(e => e.Uuid);
                entity.HasIndex(e => e.PrimarySpecialty);
                entity.HasIndex(e => e.PracticeAddressState);
            });
        }
    }
}

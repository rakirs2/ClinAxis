using System;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence.Entities;

namespace Scrapers.Persistence
{
    public class ClinicalTrialsContext : DbContext
    {
        public DbSet<StudyEntity> Studies => Set<StudyEntity>();
        public DbSet<PubmedPaperEntity> PubmedPapers => Set<PubmedPaperEntity>();
        public DbSet<StudyPaperEntity> StudyPapers => Set<StudyPaperEntity>();
        public DbSet<InvestigatorPaperEntity> InvestigatorPapers => Set<InvestigatorPaperEntity>();
        public DbSet<StudyKeywordEntity> StudyKeywords => Set<StudyKeywordEntity>();
        public DbSet<StudyConditionEntity> StudyConditions => Set<StudyConditionEntity>();
        public DbSet<StudyLocationEntity> StudyLocations => Set<StudyLocationEntity>();
        public DbSet<StudyPhaseEntity> StudyPhases => Set<StudyPhaseEntity>();
        public DbSet<PipelineRunEntity> PipelineRuns => Set<PipelineRunEntity>();
        public DbSet<PiAggregationEntity> PiAggregations => Set<PiAggregationEntity>();
        public DbSet<CategoryAggregationEntity> CategoryAggregations => Set<CategoryAggregationEntity>();
        public DbSet<ScrapeEventEntity> ScrapeEvents => Set<ScrapeEventEntity>();
        public DbSet<PipelineEventEntity> PipelineEvents => Set<PipelineEventEntity>();
        public DbSet<DataSourceStateEntity> DataSourceStates => Set<DataSourceStateEntity>();
        public DbSet<SourceFetchHistoryEntity> SourceFetchHistories => Set<SourceFetchHistoryEntity>();
        public DbSet<ScraperPivotEntity> ScraperPivots => Set<ScraperPivotEntity>();
        public DbSet<StudyReferenceEntity> StudyReferences => Set<StudyReferenceEntity>();
        public DbSet<InvestigatorPersonEntity> InvestigatorPersons => Set<InvestigatorPersonEntity>();
        public DbSet<InvestigatorAffiliationEntity> InvestigatorAffiliations => Set<InvestigatorAffiliationEntity>();
        public DbSet<StudyInvestigatorEntity> StudyInvestigators => Set<StudyInvestigatorEntity>();
        public DbSet<StudyOutcomeEntity> StudyOutcomes => Set<StudyOutcomeEntity>();
        public DbSet<StudyArmGroupEntity> StudyArmGroups => Set<StudyArmGroupEntity>();
        public DbSet<RejectedEntityEntity> RejectedEntities => Set<RejectedEntityEntity>();
        public DbSet<RejectedInvestigatorNameEntity> RejectedInvestigatorNames => Set<RejectedInvestigatorNameEntity>();
        public DbSet<PersonIdentifierCandidateEntity> PersonIdentifierCandidates => Set<PersonIdentifierCandidateEntity>();
        public DbSet<MedicareUtilizationEntity> MedicareUtilizations => Set<MedicareUtilizationEntity>();
        public DbSet<InvestigatorMetricEntity> InvestigatorMetrics => Set<InvestigatorMetricEntity>();
        public DbSet<EntityAliasEntity> EntityAliases => Set<EntityAliasEntity>();
        public DbSet<MedicareProcedureEntity> MedicareProcedures => Set<MedicareProcedureEntity>();

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
                entity.Property(e => e.Masking).HasColumnName("masking");
                entity.Property(e => e.OrgStudyId).HasColumnName("org_study_id");
                entity.Property(e => e.LeadSponsorName).HasColumnName("lead_sponsor_name");
                entity.Property(e => e.CollaboratorNames).HasColumnName("collaborator_names");
                entity.Property(e => e.EligibilityCriteria).HasColumnName("eligibility_criteria");
                entity.Property(e => e.HealthyVolunteers).HasColumnName("healthy_volunteers");
                entity.Property(e => e.EnrollmentCount).HasColumnName("enrollment_count");
                entity.Property(e => e.Sex).HasColumnName("sex");
                entity.Property(e => e.MinimumAge).HasColumnName("minimum_age");
                entity.Property(e => e.MaximumAge).HasColumnName("maximum_age");
                entity.Property(e => e.StartDate).HasColumnName("start_date");
                entity.Property(e => e.CompletionDate).HasColumnName("completion_date");
                entity.Property(e => e.StudyFirstPostDate).HasColumnName("study_first_post_date");
                entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.IsIncomplete).HasColumnName("is_incomplete");
            });

            modelBuilder.Entity<PubmedPaperEntity>(entity =>
            {
                entity.ToTable("pubmed_papers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);
                entity.Property(e => e.Pmid).HasColumnName("pmid").HasMaxLength(20);
                entity.Property(e => e.Doi).HasColumnName("doi");
                entity.Property(e => e.Title).HasColumnName("title");
                entity.Property(e => e.Journal).HasColumnName("journal");
                entity.Property(e => e.PublicationDate).HasColumnName("publication_date");
                entity.Property(e => e.Abstract).HasColumnName("abstract");
                entity.Property(e => e.IsNonEnglish).HasColumnName("is_non_english");
                entity.Property(e => e.PublicationTypes).HasColumnName("publication_types").HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => e.Pmid).IsUnique();
            });

            modelBuilder.Entity<StudyPaperEntity>(entity =>
            {
                entity.ToTable("study_papers");
                entity.HasKey(e => new { e.StudyNctId, e.PubmedPaperId });
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.PubmedPaperId).HasColumnName("pubmed_paper_id");

                entity.HasOne(e => e.Study)
                    .WithMany(s => s.StudyPapers)
                    .HasForeignKey(e => e.StudyNctId)
                    .HasPrincipalKey(s => s.NctId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.PubmedPaper)
                    .WithMany(p => p.StudyPapers)
                    .HasForeignKey(e => e.PubmedPaperId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<InvestigatorPaperEntity>(entity =>
            {
                entity.ToTable("investigator_papers");
                entity.HasKey(e => new { e.InvestigatorPersonId, e.PubmedPaperId });
                entity.Property(e => e.InvestigatorPersonId).HasColumnName("investigator_person_id");
                entity.Property(e => e.PubmedPaperId).HasColumnName("pubmed_paper_id");
                entity.Property(e => e.AuthorPosition).HasColumnName("author_position");
                entity.Property(e => e.IsCorrespondingAuthor).HasColumnName("is_corresponding_author");

                entity.HasOne(e => e.InvestigatorPerson)
                    .WithMany(p => p.InvestigatorPapers)
                    .HasForeignKey(e => e.InvestigatorPersonId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.PubmedPaper)
                    .WithMany(p => p.InvestigatorPapers)
                    .HasForeignKey(e => e.PubmedPaperId)
                    .OnDelete(DeleteBehavior.Cascade);
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

            modelBuilder.Entity<InvestigatorPersonEntity>(entity =>
            {
                entity.ToTable("investigator_persons");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(300);
                entity.Property(e => e.Prefix).HasColumnName("prefix").HasMaxLength(50);
                entity.Property(e => e.Orcid).HasColumnName("orcid").HasMaxLength(50);
                entity.Property(e => e.NcbiId).HasColumnName("ncbi_id").HasMaxLength(50);
                entity.Property(e => e.Npi).HasColumnName("npi").HasMaxLength(20);
                entity.Property(e => e.NpiLookupAttemptedAt).HasColumnName("npi_lookup_attempted_at");
                entity.Property(e => e.NpiEnrichmentResult).HasColumnName("npi_enrichment_result").HasMaxLength(20);
                entity.Property(e => e.MedicareLookupAttemptedAt).HasColumnName("medicare_lookup_attempted_at");
                entity.Property(e => e.MedicareLookupResult).HasColumnName("medicare_lookup_result").HasMaxLength(20);
                entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);
                entity.Property(e => e.IsHuman).HasColumnName("is_human");
                entity.Property(e => e.VerifiedAt).HasColumnName("verified_at");
                entity.Property(e => e.VerificationSource).HasColumnName("verification_source").HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => e.Orcid).IsUnique().HasFilter("orcid IS NOT NULL");
                entity.HasIndex(e => e.NcbiId).IsUnique().HasFilter("ncbi_id IS NOT NULL");
                entity.HasIndex(e => e.Npi).IsUnique().HasFilter("npi IS NOT NULL");
                entity.HasIndex(e => e.FullName);

                entity.HasMany(e => e.MedicareUtilizations)
                    .WithOne(m => m.InvestigatorPerson)
                    .HasForeignKey(m => m.InvestigatorPersonId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Procedures)
                    .WithOne(p => p.InvestigatorPerson)
                    .HasForeignKey(p => p.InvestigatorPersonId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PersonIdentifierCandidateEntity>(entity =>
            {
                entity.ToTable("person_identifier_candidates");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.PersonId).HasColumnName("person_id");
                entity.Property(e => e.IdentifierType).HasColumnName("identifier_type").HasMaxLength(20);
                entity.Property(e => e.IdentifierValue).HasColumnName("identifier_value").HasMaxLength(50);
                entity.Property(e => e.SourceName).HasColumnName("source_name").HasMaxLength(50);
                entity.Property(e => e.MatchedFullName).HasColumnName("matched_full_name").HasMaxLength(300);
                entity.Property(e => e.MatchedAffiliation).HasColumnName("matched_affiliation").HasMaxLength(300);
                entity.Property(e => e.MatchedState).HasColumnName("matched_state").HasMaxLength(100);
                entity.Property(e => e.SourceStatus).HasColumnName("source_status").HasMaxLength(10);
                entity.Property(e => e.SourceDeactivatedAt).HasColumnName("source_deactivated_at");
                entity.Property(e => e.IsAutoApproved).HasColumnName("is_auto_approved");
                entity.Property(e => e.IsResolved).HasColumnName("is_resolved");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");

                entity.HasOne(e => e.Person)
                    .WithMany(p => p.IdentifierCandidates)
                    .HasForeignKey(e => e.PersonId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.PersonId);
                entity.HasIndex(e => new { e.PersonId, e.IdentifierType });
            });

            modelBuilder.Entity<InvestigatorAffiliationEntity>(entity =>
            {
                entity.ToTable("investigator_affiliations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.InvestigatorPersonId).HasColumnName("investigator_person_id");
                entity.Property(e => e.InstitutionName).HasColumnName("institution_name").HasMaxLength(300);
                entity.Property(e => e.Department).HasColumnName("department").HasMaxLength(200);
                entity.Property(e => e.City).HasColumnName("city").HasMaxLength(200);
                entity.Property(e => e.State).HasColumnName("state").HasMaxLength(200);
                entity.Property(e => e.Country).HasColumnName("country").HasMaxLength(200);
                entity.Property(e => e.StartDate).HasColumnName("start_date");
                entity.Property(e => e.EndDate).HasColumnName("end_date");
                entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(200);
                entity.Property(e => e.IsPrimary).HasColumnName("is_primary");

                entity.HasOne(e => e.InvestigatorPerson)
                    .WithMany(p => p.Affiliations)
                    .HasForeignKey(e => e.InvestigatorPersonId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.InvestigatorPersonId);
                entity.HasIndex(e => e.InstitutionName);
            });

            modelBuilder.Entity<StudyInvestigatorEntity>(entity =>
            {
                entity.ToTable("study_investigators");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.InvestigatorPersonId).HasColumnName("investigator_person_id");
                entity.Property(e => e.RoleOnStudy).HasColumnName("role_on_study").HasMaxLength(200);
                entity.Property(e => e.ContactPhone).HasColumnName("contact_phone").HasMaxLength(50);
                entity.Property(e => e.ContactEmail).HasColumnName("contact_email").HasMaxLength(200);
                entity.Property(e => e.IsOverallOfficial).HasColumnName("is_overall_official");

                entity.HasOne(e => e.Study)
                    .WithMany(s => s.StudyInvestigators)
                    .HasForeignKey(e => e.StudyNctId)
                    .HasPrincipalKey(s => s.NctId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.InvestigatorPerson)
                    .WithMany(p => p.StudyInvestigators)
                    .HasForeignKey(e => e.InvestigatorPersonId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.StudyNctId, e.InvestigatorPersonId });
                entity.HasIndex(e => e.InvestigatorPersonId);
            });

            modelBuilder.Entity<StudyReferenceEntity>(entity =>
            {
                entity.ToTable("study_references");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.Pmid).HasColumnName("pmid").HasMaxLength(20);
                entity.Property(e => e.Citation).HasColumnName("citation");
                entity.Property(e => e.Type).HasColumnName("type");

                entity.HasOne(e => e.Study)
                    .WithMany(s => s.References)
                    .HasForeignKey(e => e.StudyNctId)
                    .HasPrincipalKey(s => s.NctId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.Pmid);
            });

            modelBuilder.Entity<StudyOutcomeEntity>(entity =>
            {
                entity.ToTable("study_outcomes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.OutcomeType).HasColumnName("outcome_type").HasMaxLength(20);
                entity.Property(e => e.Measure).HasColumnName("measure");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.TimeFrame).HasColumnName("time_frame");

                entity.HasOne(e => e.Study)
                    .WithMany(s => s.Outcomes)
                    .HasForeignKey(e => e.StudyNctId)
                    .HasPrincipalKey(s => s.NctId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<StudyArmGroupEntity>(entity =>
            {
                entity.ToTable("study_arm_groups");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.Label).HasColumnName("label");
                entity.Property(e => e.Type).HasColumnName("type");
                entity.Property(e => e.Description).HasColumnName("description");

                entity.HasOne(e => e.Study)
                    .WithMany(s => s.ArmGroups)
                    .HasForeignKey(e => e.StudyNctId)
                    .HasPrincipalKey(s => s.NctId)
                    .OnDelete(DeleteBehavior.Cascade);
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
                entity.Property(e => e.RejectedKeywordsTotal).HasColumnName("rejected_keywords_total");

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

            modelBuilder.Entity<RejectedEntityEntity>(entity =>
            {
                entity.ToTable("rejected_entities");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(50);
                entity.Property(e => e.Value).HasColumnName("value");
                entity.Property(e => e.StudyNctId).HasColumnName("study_nct_id").HasMaxLength(20);
                entity.Property(e => e.RejectedAt).HasColumnName("rejected_at");

                entity.HasIndex(e => e.EntityType);
                entity.HasIndex(e => e.StudyNctId);
            });

            modelBuilder.Entity<RejectedInvestigatorNameEntity>(entity =>
            {
                entity.ToTable("rejected_investigator_names");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(300);
                entity.Property(e => e.OccurrenceCount).HasColumnName("occurrence_count");
                entity.Property(e => e.StudyCount).HasColumnName("study_count");
                entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(100);
                entity.Property(e => e.IsHumanOverride).HasColumnName("is_human_override");
                entity.Property(e => e.Note).HasColumnName("note");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => e.FullName).IsUnique();
            });

            modelBuilder.Entity<MedicareUtilizationEntity>(entity =>
            {
                entity.ToTable("medicare_utilizations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.InvestigatorPersonId).HasColumnName("investigator_person_id");
                entity.Property(e => e.DataYear).HasColumnName("data_year");
                entity.Property(e => e.ProviderType).HasColumnName("provider_type").HasMaxLength(200);
                entity.Property(e => e.TotalBeneficiaries).HasColumnName("total_beneficiaries");
                entity.Property(e => e.TotalServices).HasColumnName("total_services");
                entity.Property(e => e.TotalSubmittedCharges).HasColumnName("total_submitted_charges").HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalMedicareAllowedAmount).HasColumnName("total_medicare_allowed_amount").HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalMedicarePaymentAmount).HasColumnName("total_medicare_payment_amount").HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalMedicareStandardizedAmount).HasColumnName("total_medicare_standardized_amount").HasColumnType("decimal(18,2)");
                entity.Property(e => e.MedicareParticipationIndicator).HasColumnName("medicare_participation_indicator").HasMaxLength(50);
                entity.Property(e => e.BeneAgeLt65Count).HasColumnName("bene_age_lt65_count");
                entity.Property(e => e.BeneAge65To74Count).HasColumnName("bene_age_65_to_74_count");
                entity.Property(e => e.BeneAge75To84Count).HasColumnName("bene_age_75_to_84_count");
                entity.Property(e => e.BeneAgeGt84Count).HasColumnName("bene_age_gt84_count");
                entity.Property(e => e.BeneFemaleCount).HasColumnName("bene_female_count");
                entity.Property(e => e.BeneMaleCount).HasColumnName("bene_male_count");
                entity.Property(e => e.BeneDualCount).HasColumnName("bene_dual_count");
                entity.Property(e => e.BeneNonDualCount).HasColumnName("bene_non_dual_count");
                entity.Property(e => e.ChronicConditionsJson).HasColumnName("chronic_conditions_json");
                entity.Property(e => e.AvgRiskScore).HasColumnName("avg_risk_score").HasColumnType("decimal(10,4)");
                entity.Property(e => e.MedicalServices).HasColumnName("medical_services");
                entity.Property(e => e.DrugServices).HasColumnName("drug_services");
                entity.Property(e => e.MedicalMedicarePayment).HasColumnName("medical_medicare_payment").HasColumnType("decimal(18,2)");
                entity.Property(e => e.DrugMedicarePayment).HasColumnName("drug_medicare_payment").HasColumnType("decimal(18,2)");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasOne(e => e.InvestigatorPerson)
                    .WithMany(p => p.MedicareUtilizations)
                    .HasForeignKey(e => e.InvestigatorPersonId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.InvestigatorPersonId);
                entity.HasIndex(e => new { e.InvestigatorPersonId, e.DataYear }).IsUnique();
            });

            modelBuilder.Entity<InvestigatorMetricEntity>(entity =>
            {
                entity.ToTable("investigator_metrics");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.InvestigatorPersonId).HasColumnName("investigator_person_id");
                entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);
                entity.Property(e => e.HIndex).HasColumnName("h_index");
                entity.Property(e => e.CitationCount).HasColumnName("citation_count");
                entity.Property(e => e.I10Index).HasColumnName("i10_index");
                entity.Property(e => e.TotalPapers).HasColumnName("total_papers");
                entity.Property(e => e.ExternalAuthorId).HasColumnName("external_author_id").HasMaxLength(100);
                entity.Property(e => e.LookupAttemptedAt).HasColumnName("lookup_attempted_at");
                entity.Property(e => e.LookupResult).HasColumnName("lookup_result").HasMaxLength(20);
                entity.Property(e => e.LookupErrorMessage).HasColumnName("lookup_error_message");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasOne(e => e.InvestigatorPerson)
                    .WithMany(p => p.Metrics)
                    .HasForeignKey(e => e.InvestigatorPersonId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.InvestigatorPersonId);
                entity.HasIndex(e => new { e.InvestigatorPersonId, e.Source }).IsUnique();
            });

            modelBuilder.Entity<MedicareProcedureEntity>(entity =>
            {
                entity.ToTable("medicare_procedures");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.InvestigatorPersonId).HasColumnName("investigator_person_id");
                entity.Property(e => e.DataYear).HasColumnName("data_year");
                entity.Property(e => e.HcpcsCode).HasColumnName("hcpcs_code").HasMaxLength(20);
                entity.Property(e => e.HcpcsDescription).HasColumnName("hcpcs_description");
                entity.Property(e => e.PlaceOfService).HasColumnName("place_of_service").HasMaxLength(10);
                entity.Property(e => e.BeneficiaryCount).HasColumnName("beneficiary_count");
                entity.Property(e => e.ServiceCount).HasColumnName("service_count");
                entity.Property(e => e.SubmittedChargeAmount).HasColumnName("submitted_charge_amount").HasColumnType("decimal(18,2)");
                entity.Property(e => e.MedicareAllowedAmount).HasColumnName("medicare_allowed_amount").HasColumnType("decimal(18,2)");
                entity.Property(e => e.MedicarePaymentAmount).HasColumnName("medicare_payment_amount").HasColumnType("decimal(18,2)");
                entity.Property(e => e.ProviderType).HasColumnName("provider_type").HasMaxLength(200);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasOne(e => e.InvestigatorPerson)
                    .WithMany(p => p.Procedures)
                    .HasForeignKey(e => e.InvestigatorPersonId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.InvestigatorPersonId);
                entity.HasIndex(e => new { e.InvestigatorPersonId, e.DataYear, e.HcpcsCode, e.PlaceOfService }).IsUnique();
            });

            modelBuilder.Entity<EntityAliasEntity>(entity =>
            {
                entity.ToTable("entity_aliases");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(50);
                entity.Property(e => e.CanonicalId).HasColumnName("canonical_id").HasMaxLength(255);
                entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);
                entity.Property(e => e.SourceEntityId).HasColumnName("source_entity_id").HasMaxLength(255);
                entity.Property(e => e.FirstSeenAt).HasColumnName("first_seen_at");
                entity.Property(e => e.LastSeenAt).HasColumnName("last_seen_at");

                entity.HasIndex(e => new { e.EntityType, e.Source, e.SourceEntityId }).IsUnique();
                entity.HasIndex(e => new { e.EntityType, e.CanonicalId });
            });
        }
    }
}

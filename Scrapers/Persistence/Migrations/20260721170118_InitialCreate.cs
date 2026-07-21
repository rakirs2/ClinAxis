using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "category_aggregations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    study_count = table.Column<int>(type: "integer", nullable: false),
                    pubmed_paper_count = table.Column<int>(type: "integer", nullable: false),
                    study_nct_ids = table.Column<string>(type: "text", nullable: false),
                    computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_aggregations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_source_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_sync_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_sync_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rejected_keywords_total = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_source_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entity_aliases",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    canonical_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_entity_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_aliases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "investigator_persons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    prefix = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    orcid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ncbi_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    npi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    npi_lookup_attempted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    npi_enrichment_result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    medicare_lookup_attempted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    medicare_lookup_result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_human = table.Column<bool>(type: "boolean", nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verification_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigator_persons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pi_aggregations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    investigator_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    affiliation = table.Column<string>(type: "text", nullable: true),
                    study_count = table.Column<int>(type: "integer", nullable: false),
                    pubmed_paper_count = table.Column<int>(type: "integer", nullable: false),
                    study_nct_ids = table.Column<string>(type: "text", nullable: false),
                    computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pi_aggregations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    data = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    claimed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    last_error_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipeline_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_runs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_studies = table.Column<int>(type: "integer", nullable: true),
                    total_investigators = table.Column<int>(type: "integer", nullable: true),
                    total_pubmed_papers = table.Column<int>(type: "integer", nullable: true),
                    total_keywords = table.Column<int>(type: "integer", nullable: true),
                    total_authors = table.Column<int>(type: "integer", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipeline_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pubmed_papers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pmid = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    doi = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: true),
                    journal = table.Column<string>(type: "text", nullable: true),
                    publication_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    @abstract = table.Column<string>(name: "abstract", type: "text", nullable: true),
                    is_non_english = table.Column<bool>(type: "boolean", nullable: false),
                    publication_types = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pubmed_papers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rejected_entities",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rejected_entities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rejected_investigator_names",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    occurrence_count = table.Column<int>(type: "integer", nullable: false),
                    study_count = table.Column<int>(type: "integer", nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_human_override = table.Column<bool>(type: "boolean", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rejected_investigator_names", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scraper_pivots",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    service_type = table.Column<string>(type: "text", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    cache_ttl_days = table.Column<int>(type: "integer", nullable: false),
                    batch_size = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scraper_pivots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "source_fetch_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_fetch_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    content_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_fetch_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "studies",
                columns: table => new
                {
                    nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    brief_title = table.Column<string>(type: "text", nullable: true),
                    official_title = table.Column<string>(type: "text", nullable: true),
                    overall_status = table.Column<string>(type: "text", nullable: true),
                    study_type = table.Column<string>(type: "text", nullable: true),
                    brief_summary = table.Column<string>(type: "text", nullable: true),
                    primary_purpose = table.Column<string>(type: "text", nullable: true),
                    intervention_model = table.Column<string>(type: "text", nullable: true),
                    allocation = table.Column<string>(type: "text", nullable: true),
                    masking = table.Column<string>(type: "text", nullable: true),
                    org_study_id = table.Column<string>(type: "text", nullable: true),
                    lead_sponsor_name = table.Column<string>(type: "text", nullable: true),
                    collaborator_names = table.Column<string>(type: "text", nullable: true),
                    eligibility_criteria = table.Column<string>(type: "text", nullable: true),
                    healthy_volunteers = table.Column<string>(type: "text", nullable: true),
                    enrollment_count = table.Column<int>(type: "integer", nullable: true),
                    sex = table.Column<string>(type: "text", nullable: true),
                    minimum_age = table.Column<string>(type: "text", nullable: true),
                    maximum_age = table.Column<string>(type: "text", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    completion_date = table.Column<DateOnly>(type: "date", nullable: true),
                    study_first_post_date = table.Column<DateOnly>(type: "date", nullable: true),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_incomplete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_studies", x => x.nct_id);
                });

            migrationBuilder.CreateTable(
                name: "investigator_affiliations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    state = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigator_affiliations", x => x.id);
                    table.ForeignKey(
                        name: "FK_investigator_affiliations_investigator_persons_investigator~",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "investigator_metrics",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    h_index = table.Column<int>(type: "integer", nullable: true),
                    citation_count = table.Column<int>(type: "integer", nullable: true),
                    i10_index = table.Column<int>(type: "integer", nullable: true),
                    total_papers = table.Column<int>(type: "integer", nullable: true),
                    external_author_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lookup_attempted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lookup_result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    lookup_error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigator_metrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_investigator_metrics_investigator_persons_investigator_pers~",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "medicare_procedures",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_year = table.Column<int>(type: "integer", nullable: false),
                    hcpcs_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hcpcs_description = table.Column<string>(type: "text", nullable: true),
                    place_of_service = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    beneficiary_count = table.Column<int>(type: "integer", nullable: true),
                    service_count = table.Column<long>(type: "bigint", nullable: true),
                    submitted_charge_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    medicare_allowed_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    medicare_payment_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    provider_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medicare_procedures", x => x.id);
                    table.ForeignKey(
                        name: "FK_medicare_procedures_investigator_persons_investigator_perso~",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "medicare_utilizations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_year = table.Column<int>(type: "integer", nullable: false),
                    provider_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    total_beneficiaries = table.Column<int>(type: "integer", nullable: true),
                    total_services = table.Column<long>(type: "bigint", nullable: true),
                    total_submitted_charges = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_medicare_allowed_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_medicare_payment_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_medicare_standardized_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    medicare_participation_indicator = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bene_age_lt65_count = table.Column<int>(type: "integer", nullable: true),
                    bene_age_65_to_74_count = table.Column<int>(type: "integer", nullable: true),
                    bene_age_75_to_84_count = table.Column<int>(type: "integer", nullable: true),
                    bene_age_gt84_count = table.Column<int>(type: "integer", nullable: true),
                    bene_female_count = table.Column<int>(type: "integer", nullable: true),
                    bene_male_count = table.Column<int>(type: "integer", nullable: true),
                    bene_dual_count = table.Column<int>(type: "integer", nullable: true),
                    bene_non_dual_count = table.Column<int>(type: "integer", nullable: true),
                    chronic_conditions_json = table.Column<string>(type: "text", nullable: true),
                    avg_risk_score = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    medical_services = table.Column<long>(type: "bigint", nullable: true),
                    drug_services = table.Column<long>(type: "bigint", nullable: true),
                    medical_medicare_payment = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    drug_medicare_payment = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medicare_utilizations", x => x.id);
                    table.ForeignKey(
                        name: "FK_medicare_utilizations_investigator_persons_investigator_per~",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "open_payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_year = table.Column<int>(type: "integer", nullable: false),
                    payment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payment_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    payment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payor_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    nature_of_payment = table.Column<string>(type: "text", nullable: true),
                    form_of_payment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    study_name = table.Column<string>(type: "text", nullable: true),
                    clinical_trials_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    context_of_research = table.Column<string>(type: "text", nullable: true),
                    product_category = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    product_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    record_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_open_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_open_payments_investigator_persons_investigator_person_id",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_identifier_candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identifier_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identifier_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    matched_full_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    matched_affiliation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    matched_state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    source_deactivated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_auto_approved = table.Column<bool>(type: "boolean", nullable: false),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_identifier_candidates", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_identifier_candidates_investigator_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scrape_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pipeline_run_id = table.Column<int>(type: "integer", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    records_affected = table.Column<int>(type: "integer", nullable: true),
                    message = table.Column<string>(type: "text", nullable: true),
                    http_status_code = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scrape_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_scrape_events_pipeline_runs_pipeline_run_id",
                        column: x => x.pipeline_run_id,
                        principalTable: "pipeline_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "investigator_papers",
                columns: table => new
                {
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pubmed_paper_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_position = table.Column<int>(type: "integer", nullable: true),
                    is_corresponding_author = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigator_papers", x => new { x.investigator_person_id, x.pubmed_paper_id });
                    table.ForeignKey(
                        name: "FK_investigator_papers_investigator_persons_investigator_perso~",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_investigator_papers_pubmed_papers_pubmed_paper_id",
                        column: x => x.pubmed_paper_id,
                        principalTable: "pubmed_papers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_arm_groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_arm_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_arm_groups_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_conditions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    condition = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_conditions", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_conditions_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_investigators",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_on_study = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_overall_official = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_investigators", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_investigators_investigator_persons_investigator_perso~",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_study_investigators_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_keywords",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    keyword = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_keywords", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_keywords_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_locations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    facility = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_locations", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_locations_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_outcomes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    outcome_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    measure = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    time_frame = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_outcomes", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_outcomes_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_papers",
                columns: table => new
                {
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pubmed_paper_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_papers", x => new { x.study_nct_id, x.pubmed_paper_id });
                    table.ForeignKey(
                        name: "FK_study_papers_pubmed_papers_pubmed_paper_id",
                        column: x => x.pubmed_paper_id,
                        principalTable: "pubmed_papers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_study_papers_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_phases",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_phases", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_phases_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_references",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pmid = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    citation = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_references", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_references_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_category_aggregations_category_name",
                table: "category_aggregations",
                column: "category_name");

            migrationBuilder.CreateIndex(
                name: "IX_category_aggregations_category_type",
                table: "category_aggregations",
                column: "category_type");

            migrationBuilder.CreateIndex(
                name: "IX_data_source_state_source_name",
                table: "data_source_state",
                column: "source_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entity_aliases_entity_type_canonical_id",
                table: "entity_aliases",
                columns: new[] { "entity_type", "canonical_id" });

            migrationBuilder.CreateIndex(
                name: "IX_entity_aliases_entity_type_source_source_entity_id",
                table: "entity_aliases",
                columns: new[] { "entity_type", "source", "source_entity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investigator_affiliations_institution_name",
                table: "investigator_affiliations",
                column: "institution_name");

            migrationBuilder.CreateIndex(
                name: "IX_investigator_affiliations_investigator_person_id",
                table: "investigator_affiliations",
                column: "investigator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_investigator_metrics_investigator_person_id",
                table: "investigator_metrics",
                column: "investigator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_investigator_metrics_investigator_person_id_source",
                table: "investigator_metrics",
                columns: new[] { "investigator_person_id", "source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investigator_papers_pubmed_paper_id",
                table: "investigator_papers",
                column: "pubmed_paper_id");

            migrationBuilder.CreateIndex(
                name: "IX_investigator_persons_full_name",
                table: "investigator_persons",
                column: "full_name");

            migrationBuilder.CreateIndex(
                name: "IX_investigator_persons_is_human_npi_enrichment_result",
                table: "investigator_persons",
                columns: new[] { "is_human", "npi_enrichment_result" });

            migrationBuilder.CreateIndex(
                name: "IX_investigator_persons_ncbi_id",
                table: "investigator_persons",
                column: "ncbi_id",
                unique: true,
                filter: "ncbi_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_investigator_persons_npi",
                table: "investigator_persons",
                column: "npi",
                unique: true,
                filter: "npi IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_investigator_persons_orcid",
                table: "investigator_persons",
                column: "orcid",
                unique: true,
                filter: "orcid IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_medicare_procedures_investigator_person_id",
                table: "medicare_procedures",
                column: "investigator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_medicare_procedures_investigator_person_id_data_year_hcpcs_~",
                table: "medicare_procedures",
                columns: new[] { "investigator_person_id", "data_year", "hcpcs_code", "place_of_service" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medicare_utilizations_investigator_person_id",
                table: "medicare_utilizations",
                column: "investigator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_medicare_utilizations_investigator_person_id_data_year",
                table: "medicare_utilizations",
                columns: new[] { "investigator_person_id", "data_year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_open_payments_investigator_person_id",
                table: "open_payments",
                column: "investigator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_open_payments_investigator_person_id_data_year_payment_type~",
                table: "open_payments",
                columns: new[] { "investigator_person_id", "data_year", "payment_type", "record_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_identifier_candidates_person_id",
                table: "person_identifier_candidates",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_identifier_candidates_person_id_identifier_type",
                table: "person_identifier_candidates",
                columns: new[] { "person_id", "identifier_type" });

            migrationBuilder.CreateIndex(
                name: "IX_pi_aggregations_investigator_name",
                table: "pi_aggregations",
                column: "investigator_name");

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_events_created_at",
                table: "pipeline_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_events_event_type",
                table: "pipeline_events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_events_status",
                table: "pipeline_events",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_pubmed_papers_pmid",
                table: "pubmed_papers",
                column: "pmid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rejected_entities_entity_type",
                table: "rejected_entities",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "IX_rejected_entities_study_nct_id",
                table: "rejected_entities",
                column: "study_nct_id");

            migrationBuilder.CreateIndex(
                name: "IX_rejected_investigator_names_full_name",
                table: "rejected_investigator_names",
                column: "full_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scrape_events_event_type",
                table: "scrape_events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_scrape_events_pipeline_run_id",
                table: "scrape_events",
                column: "pipeline_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_scrape_events_source",
                table: "scrape_events",
                column: "source");

            migrationBuilder.CreateIndex(
                name: "IX_scrape_events_timestamp",
                table: "scrape_events",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_scraper_pivots_name",
                table: "scraper_pivots",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_fetch_history_study_nct_id",
                table: "source_fetch_history",
                column: "study_nct_id");

            migrationBuilder.CreateIndex(
                name: "IX_source_fetch_history_study_nct_id_source_type",
                table: "source_fetch_history",
                columns: new[] { "study_nct_id", "source_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_studies_enrollment_count",
                table: "studies",
                column: "enrollment_count");

            migrationBuilder.CreateIndex(
                name: "IX_studies_overall_status_start_date",
                table: "studies",
                columns: new[] { "overall_status", "start_date" });

            migrationBuilder.CreateIndex(
                name: "IX_study_arm_groups_study_nct_id",
                table: "study_arm_groups",
                column: "study_nct_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_conditions_condition",
                table: "study_conditions",
                column: "condition");

            migrationBuilder.CreateIndex(
                name: "IX_study_conditions_study_nct_id",
                table: "study_conditions",
                column: "study_nct_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_investigators_investigator_person_id",
                table: "study_investigators",
                column: "investigator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_investigators_study_nct_id_investigator_person_id",
                table: "study_investigators",
                columns: new[] { "study_nct_id", "investigator_person_id" });

            migrationBuilder.CreateIndex(
                name: "IX_study_keywords_keyword",
                table: "study_keywords",
                column: "keyword");

            migrationBuilder.CreateIndex(
                name: "IX_study_keywords_study_nct_id",
                table: "study_keywords",
                column: "study_nct_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_locations_city",
                table: "study_locations",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "IX_study_locations_country",
                table: "study_locations",
                column: "country");

            migrationBuilder.CreateIndex(
                name: "IX_study_locations_facility",
                table: "study_locations",
                column: "facility");

            migrationBuilder.CreateIndex(
                name: "IX_study_locations_state",
                table: "study_locations",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "IX_study_locations_study_nct_id",
                table: "study_locations",
                column: "study_nct_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_outcomes_study_nct_id",
                table: "study_outcomes",
                column: "study_nct_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_papers_pubmed_paper_id",
                table: "study_papers",
                column: "pubmed_paper_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_phases_study_nct_id",
                table: "study_phases",
                column: "study_nct_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_references_pmid",
                table: "study_references",
                column: "pmid");

            migrationBuilder.CreateIndex(
                name: "IX_study_references_study_nct_id",
                table: "study_references",
                column: "study_nct_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_aggregations");

            migrationBuilder.DropTable(
                name: "data_source_state");

            migrationBuilder.DropTable(
                name: "entity_aliases");

            migrationBuilder.DropTable(
                name: "investigator_affiliations");

            migrationBuilder.DropTable(
                name: "investigator_metrics");

            migrationBuilder.DropTable(
                name: "investigator_papers");

            migrationBuilder.DropTable(
                name: "medicare_procedures");

            migrationBuilder.DropTable(
                name: "medicare_utilizations");

            migrationBuilder.DropTable(
                name: "open_payments");

            migrationBuilder.DropTable(
                name: "person_identifier_candidates");

            migrationBuilder.DropTable(
                name: "pi_aggregations");

            migrationBuilder.DropTable(
                name: "pipeline_events");

            migrationBuilder.DropTable(
                name: "rejected_entities");

            migrationBuilder.DropTable(
                name: "rejected_investigator_names");

            migrationBuilder.DropTable(
                name: "scrape_events");

            migrationBuilder.DropTable(
                name: "scraper_pivots");

            migrationBuilder.DropTable(
                name: "source_fetch_history");

            migrationBuilder.DropTable(
                name: "study_arm_groups");

            migrationBuilder.DropTable(
                name: "study_conditions");

            migrationBuilder.DropTable(
                name: "study_investigators");

            migrationBuilder.DropTable(
                name: "study_keywords");

            migrationBuilder.DropTable(
                name: "study_locations");

            migrationBuilder.DropTable(
                name: "study_outcomes");

            migrationBuilder.DropTable(
                name: "study_papers");

            migrationBuilder.DropTable(
                name: "study_phases");

            migrationBuilder.DropTable(
                name: "study_references");

            migrationBuilder.DropTable(
                name: "pipeline_runs");

            migrationBuilder.DropTable(
                name: "investigator_persons");

            migrationBuilder.DropTable(
                name: "pubmed_papers");

            migrationBuilder.DropTable(
                name: "studies");
        }
    }
}

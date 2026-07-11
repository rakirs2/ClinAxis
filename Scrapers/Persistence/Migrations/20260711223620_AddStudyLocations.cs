using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyLocations : Migration
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
                    enrollment_count = table.Column<int>(type: "integer", nullable: true),
                    sex = table.Column<string>(type: "text", nullable: true),
                    minimum_age = table.Column<string>(type: "text", nullable: true),
                    maximum_age = table.Column<string>(type: "text", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    completion_date = table.Column<DateOnly>(type: "date", nullable: true),
                    study_first_post_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_incomplete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_studies", x => x.nct_id);
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
                name: "investigators",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "text", nullable: true),
                    affiliation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigators", x => x.id);
                    table.ForeignKey(
                        name: "FK_investigators_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pubmed_studies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pmid = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    doi = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: true),
                    journal = table.Column<string>(type: "text", nullable: true),
                    publication_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    @abstract = table.Column<string>(name: "abstract", type: "text", nullable: true),
                    is_non_english = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pubmed_studies", x => x.id);
                    table.ForeignKey(
                        name: "FK_pubmed_studies_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_authors",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pmid = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    fore_name = table.Column<string>(type: "text", nullable: true),
                    orcid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_authors", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_authors_studies_study_nct_id",
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

            migrationBuilder.CreateIndex(
                name: "IX_category_aggregations_category_name",
                table: "category_aggregations",
                column: "category_name");

            migrationBuilder.CreateIndex(
                name: "IX_category_aggregations_category_type",
                table: "category_aggregations",
                column: "category_type");

            migrationBuilder.CreateIndex(
                name: "IX_investigators_study_nct_id",
                table: "investigators",
                column: "study_nct_id");

            migrationBuilder.CreateIndex(
                name: "IX_pi_aggregations_investigator_name",
                table: "pi_aggregations",
                column: "investigator_name");

            migrationBuilder.CreateIndex(
                name: "IX_pubmed_studies_study_nct_id_pmid",
                table: "pubmed_studies",
                columns: new[] { "study_nct_id", "pmid" },
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
                name: "IX_study_authors_last_name",
                table: "study_authors",
                column: "last_name");

            migrationBuilder.CreateIndex(
                name: "IX_study_authors_orcid",
                table: "study_authors",
                column: "orcid");

            migrationBuilder.CreateIndex(
                name: "IX_study_authors_study_nct_id",
                table: "study_authors",
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
                name: "IX_study_phases_study_nct_id",
                table: "study_phases",
                column: "study_nct_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_aggregations");

            migrationBuilder.DropTable(
                name: "investigators");

            migrationBuilder.DropTable(
                name: "pi_aggregations");

            migrationBuilder.DropTable(
                name: "pubmed_studies");

            migrationBuilder.DropTable(
                name: "scrape_events");

            migrationBuilder.DropTable(
                name: "study_authors");

            migrationBuilder.DropTable(
                name: "study_conditions");

            migrationBuilder.DropTable(
                name: "study_keywords");

            migrationBuilder.DropTable(
                name: "study_locations");

            migrationBuilder.DropTable(
                name: "study_phases");

            migrationBuilder.DropTable(
                name: "pipeline_runs");

            migrationBuilder.DropTable(
                name: "studies");
        }
    }
}

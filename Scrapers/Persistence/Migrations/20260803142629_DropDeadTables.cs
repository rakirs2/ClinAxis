using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropDeadTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_scrape_events_pipeline_runs_pipeline_run_id",
                table: "scrape_events");

            migrationBuilder.DropTable(
                name: "category_aggregations");

            migrationBuilder.DropTable(
                name: "pi_aggregations");

            migrationBuilder.DropTable(
                name: "pipeline_runs");

            migrationBuilder.DropTable(
                name: "scraper_pivots");

            migrationBuilder.DropTable(
                name: "source_fetch_history");

            migrationBuilder.DropIndex(
                name: "IX_scrape_events_pipeline_run_id",
                table: "scrape_events");

            migrationBuilder.DropColumn(
                name: "pipeline_run_id",
                table: "scrape_events");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "pipeline_run_id",
                table: "scrape_events",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "category_aggregations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    pubmed_paper_count = table.Column<int>(type: "integer", nullable: false),
                    study_count = table.Column<int>(type: "integer", nullable: false),
                    study_nct_ids = table.Column<string>(type: "text", nullable: false)
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
                    affiliation = table.Column<string>(type: "text", nullable: true),
                    computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    investigator_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pubmed_paper_count = table.Column<int>(type: "integer", nullable: false),
                    study_count = table.Column<int>(type: "integer", nullable: false),
                    study_nct_ids = table.Column<string>(type: "text", nullable: false)
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
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_authors = table.Column<int>(type: "integer", nullable: true),
                    total_investigators = table.Column<int>(type: "integer", nullable: true),
                    total_keywords = table.Column<int>(type: "integer", nullable: true),
                    total_pubmed_papers = table.Column<int>(type: "integer", nullable: true),
                    total_studies = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipeline_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scraper_pivots",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    batch_size = table.Column<int>(type: "integer", nullable: false),
                    cache_ttl_days = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    service_type = table.Column<string>(type: "text", nullable: false),
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
                    content_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_fetch_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_fetch_history", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scrape_events_pipeline_run_id",
                table: "scrape_events",
                column: "pipeline_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_category_aggregations_category_name",
                table: "category_aggregations",
                column: "category_name");

            migrationBuilder.CreateIndex(
                name: "IX_category_aggregations_category_type",
                table: "category_aggregations",
                column: "category_type");

            migrationBuilder.CreateIndex(
                name: "IX_pi_aggregations_investigator_name",
                table: "pi_aggregations",
                column: "investigator_name");

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

            migrationBuilder.AddForeignKey(
                name: "FK_scrape_events_pipeline_runs_pipeline_run_id",
                table: "scrape_events",
                column: "pipeline_run_id",
                principalTable: "pipeline_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

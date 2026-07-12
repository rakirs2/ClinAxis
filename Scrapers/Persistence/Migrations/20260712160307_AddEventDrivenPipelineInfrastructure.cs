using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventDrivenPipelineInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_source_state", x => x.id);
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

            migrationBuilder.CreateIndex(
                name: "IX_data_source_state_source_name",
                table: "data_source_state",
                column: "source_name",
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_source_state");

            migrationBuilder.DropTable(
                name: "pipeline_events");

            migrationBuilder.DropTable(
                name: "scraper_pivots");

            migrationBuilder.DropTable(
                name: "source_fetch_history");
        }
    }
}

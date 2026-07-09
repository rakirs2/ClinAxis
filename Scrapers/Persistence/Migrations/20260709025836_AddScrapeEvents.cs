using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScrapeEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scrape_events");
        }
    }
}

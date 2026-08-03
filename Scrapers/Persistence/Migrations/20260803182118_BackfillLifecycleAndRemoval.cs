using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLifecycleAndRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_seen_in_sweep_utc",
                table: "studies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "removed_from_source_at",
                table: "studies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "backfill_completed_utc",
                table: "data_source_state",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "backfill_remaining_studies",
                table: "data_source_state",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "backfill_started_utc",
                table: "data_source_state",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "backfill_status",
                table: "data_source_state",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_seen_in_sweep_utc",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "removed_from_source_at",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "backfill_completed_utc",
                table: "data_source_state");

            migrationBuilder.DropColumn(
                name: "backfill_remaining_studies",
                table: "data_source_state");

            migrationBuilder.DropColumn(
                name: "backfill_started_utc",
                table: "data_source_state");

            migrationBuilder.DropColumn(
                name: "backfill_status",
                table: "data_source_state");
        }
    }
}

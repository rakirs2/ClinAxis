using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventInFlightProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "progress_processed",
                table: "pipeline_events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "progress_total",
                table: "pipeline_events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "progress_updated_at",
                table: "pipeline_events",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "progress_processed",
                table: "pipeline_events");

            migrationBuilder.DropColumn(
                name: "progress_total",
                table: "pipeline_events");

            migrationBuilder.DropColumn(
                name: "progress_updated_at",
                table: "pipeline_events");
        }
    }
}

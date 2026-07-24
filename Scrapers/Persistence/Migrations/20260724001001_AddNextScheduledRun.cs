using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNextScheduledRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "next_scheduled_run",
                table: "data_source_state",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "next_scheduled_run",
                table: "data_source_state");
        }
    }
}

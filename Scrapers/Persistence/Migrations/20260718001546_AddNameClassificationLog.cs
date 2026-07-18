using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNameClassificationLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "name_classification_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    name_filter_decision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name_filter_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ml_decision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ml_confidence = table.Column<double>(type: "double precision", nullable: false),
                    user_classification = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_name_classification_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_name_classification_log_ml_decision",
                table: "name_classification_log",
                column: "ml_decision");

            migrationBuilder.CreateIndex(
                name: "IX_name_classification_log_name_filter_decision",
                table: "name_classification_log",
                column: "name_filter_decision");

            migrationBuilder.CreateIndex(
                name: "IX_name_classification_log_reviewed_at",
                table: "name_classification_log",
                column: "reviewed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "name_classification_log");
        }
    }
}

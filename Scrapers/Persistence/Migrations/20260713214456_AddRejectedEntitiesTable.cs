using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectedEntitiesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_rejected_entities_entity_type",
                table: "rejected_entities",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "IX_rejected_entities_study_nct_id",
                table: "rejected_entities",
                column: "study_nct_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rejected_entities");
        }
    }
}

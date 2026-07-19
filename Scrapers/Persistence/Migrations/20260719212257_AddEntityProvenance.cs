using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "studies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "ClinicalTrials.gov/v2");

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "pubmed_papers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "PubMed/EUtils");

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "investigator_persons",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "ClinicalTrials.gov");

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

            migrationBuilder.CreateIndex(
                name: "IX_entity_aliases_entity_type_canonical_id",
                table: "entity_aliases",
                columns: new[] { "entity_type", "canonical_id" });

            migrationBuilder.CreateIndex(
                name: "IX_entity_aliases_entity_type_source_source_entity_id",
                table: "entity_aliases",
                columns: new[] { "entity_type", "source", "source_entity_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entity_aliases");

            migrationBuilder.DropColumn(
                name: "source",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "source",
                table: "pubmed_papers");

            migrationBuilder.DropColumn(
                name: "source",
                table: "investigator_persons");
        }
    }
}

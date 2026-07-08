using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    public partial class InitPubMedScraperTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrcidId",
                table: "investigators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NcbiId",
                table: "investigators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessfulPubmedCrawl",
                table: "investigators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pubmed_studies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvestigatorId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Keywords = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pubmed_studies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pubmed_studies_investigators_InvestigatorId",
                        column: x => x.InvestigatorId,
                        principalTable: "investigators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pubmed_studies_InvestigatorId",
                table: "pubmed_studies",
                column: "InvestigatorId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pubmed_studies");

            migrationBuilder.DropColumn(
                name: "OrcidId",
                table: "investigators");

            migrationBuilder.DropColumn(
                name: "NcbiId",
                table: "investigators");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulPubmedCrawl",
                table: "investigators");
        }
    }
}

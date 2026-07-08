using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "studies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NctId = table.Column<string>(type: "text", nullable: false),
                    BriefTitle = table.Column<string>(type: "text", nullable: true),
                    OverallStatus = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_studies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investigators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Affiliation = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: true),
                    OrcidId = table.Column<string>(type: "text", nullable: true),
                    NcbiId = table.Column<string>(type: "text", nullable: true),
                    LastSuccessfulPubmedCrawl = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_investigators_studies_StudyId",
                        column: x => x.StudyId,
                        principalTable: "studies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'")
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
                name: "IX_investigators_StudyId",
                table: "investigators",
                column: "StudyId");

            migrationBuilder.CreateIndex(
                name: "IX_pubmed_studies_InvestigatorId",
                table: "pubmed_studies",
                column: "InvestigatorId");

            migrationBuilder.CreateIndex(
                name: "IX_studies_NctId",
                table: "studies",
                column: "NctId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pubmed_studies");

            migrationBuilder.DropTable(
                name: "investigators");

            migrationBuilder.DropTable(
                name: "studies");
        }
    }
}

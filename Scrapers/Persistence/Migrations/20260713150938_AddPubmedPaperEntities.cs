using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPubmedPaperEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pubmed_studies");

            migrationBuilder.DropTable(
                name: "study_authors");

            migrationBuilder.CreateTable(
                name: "pubmed_papers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pmid = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    doi = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: true),
                    journal = table.Column<string>(type: "text", nullable: true),
                    publication_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    @abstract = table.Column<string>(name: "abstract", type: "text", nullable: true),
                    is_non_english = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pubmed_papers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "investigator_papers",
                columns: table => new
                {
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pubmed_paper_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_position = table.Column<int>(type: "integer", nullable: true),
                    is_corresponding_author = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigator_papers", x => new { x.investigator_person_id, x.pubmed_paper_id });
                    table.ForeignKey(
                        name: "FK_investigator_papers_investigator_persons_investigator_perso~",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_investigator_papers_pubmed_papers_pubmed_paper_id",
                        column: x => x.pubmed_paper_id,
                        principalTable: "pubmed_papers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_papers",
                columns: table => new
                {
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pubmed_paper_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_papers", x => new { x.study_nct_id, x.pubmed_paper_id });
                    table.ForeignKey(
                        name: "FK_study_papers_pubmed_papers_pubmed_paper_id",
                        column: x => x.pubmed_paper_id,
                        principalTable: "pubmed_papers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_study_papers_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_investigator_papers_pubmed_paper_id",
                table: "investigator_papers",
                column: "pubmed_paper_id");

            migrationBuilder.CreateIndex(
                name: "IX_pubmed_papers_pmid",
                table: "pubmed_papers",
                column: "pmid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_papers_pubmed_paper_id",
                table: "study_papers",
                column: "pubmed_paper_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investigator_papers");

            migrationBuilder.DropTable(
                name: "study_papers");

            migrationBuilder.DropTable(
                name: "pubmed_papers");

            migrationBuilder.CreateTable(
                name: "pubmed_studies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    @abstract = table.Column<string>(name: "abstract", type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    doi = table.Column<string>(type: "text", nullable: true),
                    is_non_english = table.Column<bool>(type: "boolean", nullable: false),
                    journal = table.Column<string>(type: "text", nullable: true),
                    pmid = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    publication_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    title = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pubmed_studies", x => x.id);
                    table.ForeignKey(
                        name: "FK_pubmed_studies_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_authors",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fore_name = table.Column<string>(type: "text", nullable: true),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    orcid = table.Column<string>(type: "text", nullable: true),
                    pmid = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_authors", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_authors_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pubmed_studies_study_nct_id_pmid",
                table: "pubmed_studies",
                columns: new[] { "study_nct_id", "pmid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_authors_last_name",
                table: "study_authors",
                column: "last_name");

            migrationBuilder.CreateIndex(
                name: "IX_study_authors_orcid",
                table: "study_authors",
                column: "orcid");

            migrationBuilder.CreateIndex(
                name: "IX_study_authors_study_nct_id",
                table: "study_authors",
                column: "study_nct_id");
        }
    }
}

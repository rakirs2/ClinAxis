using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedInvestigatorEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "investigator_persons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    orcid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ncbi_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verification_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigator_persons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "investigator_affiliations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    state = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigator_affiliations", x => x.id);
                    table.ForeignKey(
                        name: "FK_investigator_affiliations_investigator_persons_investigator~",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_investigators",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_on_study = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_overall_official = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_investigators", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_investigators_investigator_persons_investigator_perso~",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_study_investigators_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_investigator_affiliations_institution_name",
                table: "investigator_affiliations",
                column: "institution_name");

            migrationBuilder.CreateIndex(
                name: "IX_investigator_affiliations_investigator_person_id",
                table: "investigator_affiliations",
                column: "investigator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_investigator_persons_full_name",
                table: "investigator_persons",
                column: "full_name");

            migrationBuilder.CreateIndex(
                name: "IX_investigator_persons_ncbi_id",
                table: "investigator_persons",
                column: "ncbi_id",
                unique: true,
                filter: "ncbi_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_investigator_persons_orcid",
                table: "investigator_persons",
                column: "orcid",
                unique: true,
                filter: "orcid IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_study_investigators_investigator_person_id",
                table: "study_investigators",
                column: "investigator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_investigators_study_nct_id_investigator_person_id",
                table: "study_investigators",
                columns: new[] { "study_nct_id", "investigator_person_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investigator_affiliations");

            migrationBuilder.DropTable(
                name: "study_investigators");

            migrationBuilder.DropTable(
                name: "investigator_persons");
        }
    }
}

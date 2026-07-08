using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTrialMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "authors_json",
                table: "pubmed_studies");

            migrationBuilder.AddColumn<string>(
                name: "allocation",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "brief_summary",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "completion_date",
                table: "studies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "enrollment_count",
                table: "studies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "intervention_model",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "maximum_age",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "minimum_age",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "official_title",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "primary_purpose",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sex",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "start_date",
                table: "studies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "study_first_post_date",
                table: "studies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "study_type",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "study_authors",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pmid = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    fore_name = table.Column<string>(type: "text", nullable: true),
                    orcid = table.Column<string>(type: "text", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "study_conditions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    condition = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_conditions", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_conditions_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_keywords",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    keyword = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_keywords", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_keywords_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_phases",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_phases", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_phases_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_study_conditions_condition",
                table: "study_conditions",
                column: "condition");

            migrationBuilder.CreateIndex(
                name: "IX_study_conditions_study_nct_id",
                table: "study_conditions",
                column: "study_nct_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_keywords_keyword",
                table: "study_keywords",
                column: "keyword");

            migrationBuilder.CreateIndex(
                name: "IX_study_keywords_study_nct_id",
                table: "study_keywords",
                column: "study_nct_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_phases_study_nct_id",
                table: "study_phases",
                column: "study_nct_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_authors");

            migrationBuilder.DropTable(
                name: "study_conditions");

            migrationBuilder.DropTable(
                name: "study_keywords");

            migrationBuilder.DropTable(
                name: "study_phases");

            migrationBuilder.DropColumn(
                name: "allocation",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "brief_summary",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "completion_date",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "enrollment_count",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "intervention_model",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "maximum_age",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "minimum_age",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "official_title",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "primary_purpose",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "sex",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "start_date",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "study_first_post_date",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "study_type",
                table: "studies");

            migrationBuilder.AddColumn<string>(
                name: "authors_json",
                table: "pubmed_studies",
                type: "text",
                nullable: true);
        }
    }
}

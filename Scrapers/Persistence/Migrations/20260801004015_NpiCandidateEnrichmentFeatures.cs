using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NpiCandidateEnrichmentFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "matched_city",
                table: "person_identifier_candidates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "matched_credential",
                table: "person_identifier_candidates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "matched_gender",
                table: "person_identifier_candidates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "matched_identifiers_json",
                table: "person_identifier_candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "matched_middle_name",
                table: "person_identifier_candidates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "matched_name_prefix",
                table: "person_identifier_candidates",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "matched_other_names_json",
                table: "person_identifier_candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "matched_taxonomy_desc",
                table: "person_identifier_candidates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "matched_taxonomy_license",
                table: "person_identifier_candidates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "matched_taxonomy_state",
                table: "person_identifier_candidates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "rule_score",
                table: "person_identifier_candidates",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "matched_city",
                table: "person_identifier_candidates");

            migrationBuilder.DropColumn(
                name: "matched_credential",
                table: "person_identifier_candidates");

            migrationBuilder.DropColumn(
                name: "matched_gender",
                table: "person_identifier_candidates");

            migrationBuilder.DropColumn(
                name: "matched_identifiers_json",
                table: "person_identifier_candidates");

            migrationBuilder.DropColumn(
                name: "matched_middle_name",
                table: "person_identifier_candidates");

            migrationBuilder.DropColumn(
                name: "matched_name_prefix",
                table: "person_identifier_candidates");

            migrationBuilder.DropColumn(
                name: "matched_other_names_json",
                table: "person_identifier_candidates");

            migrationBuilder.DropColumn(
                name: "matched_taxonomy_desc",
                table: "person_identifier_candidates");

            migrationBuilder.DropColumn(
                name: "matched_taxonomy_license",
                table: "person_identifier_candidates");

            migrationBuilder.DropColumn(
                name: "matched_taxonomy_state",
                table: "person_identifier_candidates");

            migrationBuilder.DropColumn(
                name: "rule_score",
                table: "person_identifier_candidates");
        }
    }
}

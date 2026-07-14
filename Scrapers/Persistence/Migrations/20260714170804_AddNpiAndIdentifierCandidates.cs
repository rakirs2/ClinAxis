using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNpiAndIdentifierCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsHuman",
                table: "investigator_persons",
                newName: "is_human");

            migrationBuilder.AddColumn<string>(
                name: "npi",
                table: "investigator_persons",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "npi_lookup_attempted_at",
                table: "investigator_persons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "person_identifier_candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identifier_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identifier_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    matched_full_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    matched_affiliation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    matched_state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    source_deactivated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_auto_approved = table.Column<bool>(type: "boolean", nullable: false),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_identifier_candidates", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_identifier_candidates_investigator_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_investigator_persons_npi",
                table: "investigator_persons",
                column: "npi",
                unique: true,
                filter: "npi IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_person_identifier_candidates_person_id",
                table: "person_identifier_candidates",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_identifier_candidates_person_id_identifier_type",
                table: "person_identifier_candidates",
                columns: new[] { "person_id", "identifier_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "person_identifier_candidates");

            migrationBuilder.DropIndex(
                name: "IX_investigator_persons_npi",
                table: "investigator_persons");

            migrationBuilder.DropColumn(
                name: "npi",
                table: "investigator_persons");

            migrationBuilder.DropColumn(
                name: "npi_lookup_attempted_at",
                table: "investigator_persons");

            migrationBuilder.RenameColumn(
                name: "is_human",
                table: "investigator_persons",
                newName: "IsHuman");
        }
    }
}

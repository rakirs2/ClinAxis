using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataLossRemediationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "collaborator_names",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "eligibility_criteria",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "healthy_volunteers",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lead_sponsor_name",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "masking",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "org_study_id",
                table: "studies",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "study_arm_groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_arm_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_arm_groups_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_outcomes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    outcome_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    measure = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    time_frame = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_outcomes", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_outcomes_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_arm_groups_study_nct_id",
                table: "study_arm_groups",
                column: "study_nct_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_outcomes_study_nct_id",
                table: "study_outcomes",
                column: "study_nct_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_arm_groups");

            migrationBuilder.DropTable(
                name: "study_outcomes");

            migrationBuilder.DropColumn(
                name: "collaborator_names",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "eligibility_criteria",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "healthy_volunteers",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "lead_sponsor_name",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "masking",
                table: "studies");

            migrationBuilder.DropColumn(
                name: "org_study_id",
                table: "studies");
        }
    }
}

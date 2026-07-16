using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicareUtilization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "medicare_lookup_attempted_at",
                table: "investigator_persons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "medicare_lookup_result",
                table: "investigator_persons",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "medicare_utilizations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_year = table.Column<int>(type: "integer", nullable: false),
                    provider_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    total_beneficiaries = table.Column<int>(type: "integer", nullable: true),
                    total_services = table.Column<long>(type: "bigint", nullable: true),
                    total_submitted_charges = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_medicare_allowed_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_medicare_payment_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_medicare_standardized_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    medicare_participation_indicator = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bene_age_lt65_count = table.Column<int>(type: "integer", nullable: true),
                    bene_age_65_to_74_count = table.Column<int>(type: "integer", nullable: true),
                    bene_age_75_to_84_count = table.Column<int>(type: "integer", nullable: true),
                    bene_age_gt84_count = table.Column<int>(type: "integer", nullable: true),
                    bene_female_count = table.Column<int>(type: "integer", nullable: true),
                    bene_male_count = table.Column<int>(type: "integer", nullable: true),
                    bene_dual_count = table.Column<int>(type: "integer", nullable: true),
                    bene_non_dual_count = table.Column<int>(type: "integer", nullable: true),
                    chronic_conditions_json = table.Column<string>(type: "text", nullable: true),
                    avg_risk_score = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    medical_services = table.Column<long>(type: "bigint", nullable: true),
                    drug_services = table.Column<long>(type: "bigint", nullable: true),
                    medical_medicare_payment = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    drug_medicare_payment = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medicare_utilizations", x => x.id);
                    table.ForeignKey(
                        name: "FK_medicare_utilizations_investigator_persons_investigator_per~",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_medicare_utilizations_investigator_person_id",
                table: "medicare_utilizations",
                column: "investigator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_medicare_utilizations_investigator_person_id_data_year",
                table: "medicare_utilizations",
                columns: new[] { "investigator_person_id", "data_year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medicare_utilizations");

            migrationBuilder.DropColumn(
                name: "medicare_lookup_attempted_at",
                table: "investigator_persons");

            migrationBuilder.DropColumn(
                name: "medicare_lookup_result",
                table: "investigator_persons");
        }
    }
}

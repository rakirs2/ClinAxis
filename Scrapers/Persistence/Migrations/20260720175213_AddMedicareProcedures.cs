using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicareProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "medicare_procedures",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_year = table.Column<int>(type: "integer", nullable: false),
                    hcpcs_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hcpcs_description = table.Column<string>(type: "text", nullable: true),
                    place_of_service = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    beneficiary_count = table.Column<int>(type: "integer", nullable: true),
                    service_count = table.Column<long>(type: "bigint", nullable: true),
                    submitted_charge_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    medicare_allowed_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    medicare_payment_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    provider_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medicare_procedures", x => x.id);
                    table.ForeignKey(
                        name: "FK_medicare_procedures_investigator_persons_investigator_perso~",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_medicare_procedures_investigator_person_id",
                table: "medicare_procedures",
                column: "investigator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_medicare_procedures_investigator_person_id_data_year_hcpcs_~",
                table: "medicare_procedures",
                columns: new[] { "investigator_person_id", "data_year", "hcpcs_code", "place_of_service" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medicare_procedures");
        }
    }
}

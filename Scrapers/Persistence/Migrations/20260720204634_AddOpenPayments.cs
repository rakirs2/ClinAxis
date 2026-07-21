using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "open_payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_year = table.Column<int>(type: "integer", nullable: false),
                    payment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payment_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    payment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payor_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    nature_of_payment = table.Column<string>(type: "text", nullable: true),
                    form_of_payment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    study_name = table.Column<string>(type: "text", nullable: true),
                    clinical_trials_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    context_of_research = table.Column<string>(type: "text", nullable: true),
                    product_category = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    product_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    record_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_open_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_open_payments_investigator_persons_investigator_person_id",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_open_payments_investigator_person_id",
                table: "open_payments",
                column: "investigator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_open_payments_investigator_person_id_data_year_payment_type~",
                table: "open_payments",
                columns: new[] { "investigator_person_id", "data_year", "payment_type", "record_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "open_payments");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCmsProviderInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Uuid",
                table: "investigators",
                newName: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "npi",
                table: "investigators",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "npi_lookup_at",
                table: "investigators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "orcid",
                table: "investigators",
                type: "character varying(19)",
                maxLength: 19,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "orcid_lookup_at",
                table: "investigators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cms_providers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    uuid = table.Column<Guid>(type: "uuid", nullable: false),
                    npi = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    provider_name = table.Column<string>(type: "text", nullable: true),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    credential = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    medical_school_name = table.Column<string>(type: "text", nullable: true),
                    graduation_year = table.Column<int>(type: "integer", nullable: true),
                    primary_specialty = table.Column<string>(type: "text", nullable: true),
                    secondary_specialty = table.Column<string>(type: "text", nullable: true),
                    organization_legal_name = table.Column<string>(type: "text", nullable: true),
                    practice_address_city = table.Column<string>(type: "text", nullable: true),
                    practice_address_state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    practice_address_zip = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    medicare_participation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    total_medicare_services = table.Column<int>(type: "integer", nullable: true),
                    total_medicare_payments = table.Column<decimal>(type: "numeric", nullable: true),
                    total_medicare_beneficiaries = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cms_providers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cms_providers_npi",
                table: "cms_providers",
                column: "npi",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cms_providers_practice_address_state",
                table: "cms_providers",
                column: "practice_address_state");

            migrationBuilder.CreateIndex(
                name: "IX_cms_providers_primary_specialty",
                table: "cms_providers",
                column: "primary_specialty");

            migrationBuilder.CreateIndex(
                name: "IX_cms_providers_uuid",
                table: "cms_providers",
                column: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cms_providers");

            migrationBuilder.DropColumn(
                name: "npi",
                table: "investigators");

            migrationBuilder.DropColumn(
                name: "npi_lookup_at",
                table: "investigators");

            migrationBuilder.DropColumn(
                name: "orcid",
                table: "investigators");

            migrationBuilder.DropColumn(
                name: "orcid_lookup_at",
                table: "investigators");

            migrationBuilder.RenameColumn(
                name: "uuid",
                table: "investigators",
                newName: "Uuid");
        }
    }
}

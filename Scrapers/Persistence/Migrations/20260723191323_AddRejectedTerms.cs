using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectedTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rejected_terms",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "condition"),
                    side_a_valid = table.Column<bool>(type: "boolean", nullable: false),
                    side_b_matched = table.Column<bool>(type: "boolean", nullable: false),
                    side_b_mesh_term = table.Column<string>(type: "text", nullable: false),
                    side_b_mesh_cui = table.Column<string>(type: "text", nullable: false),
                    side_b_category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "unmapped"),
                    side_b_similarity = table.Column<float>(type: "real", nullable: false),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    rejection_reason = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rejected_terms", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rejected_terms_accepted",
                table: "rejected_terms",
                column: "accepted");

            migrationBuilder.CreateIndex(
                name: "IX_rejected_terms_side_b_category",
                table: "rejected_terms",
                column: "side_b_category");

            migrationBuilder.CreateIndex(
                name: "IX_rejected_terms_study_nct_id",
                table: "rejected_terms",
                column: "study_nct_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rejected_terms");
        }
    }
}

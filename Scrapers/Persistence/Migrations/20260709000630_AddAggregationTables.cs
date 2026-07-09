using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAggregationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "category_aggregations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    study_count = table.Column<int>(type: "integer", nullable: false),
                    pubmed_paper_count = table.Column<int>(type: "integer", nullable: false),
                    study_nct_ids = table.Column<string>(type: "text", nullable: false),
                    computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_category_aggregations", x => x.id));

            migrationBuilder.CreateTable(
                name: "pi_aggregations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    investigator_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    affiliation = table.Column<string>(type: "text", nullable: true),
                    study_count = table.Column<int>(type: "integer", nullable: false),
                    pubmed_paper_count = table.Column<int>(type: "integer", nullable: false),
                    study_nct_ids = table.Column<string>(type: "text", nullable: false),
                    computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_pi_aggregations", x => x.id));

            migrationBuilder.CreateIndex(
                name: "IX_category_aggregations_category_name",
                table: "category_aggregations",
                column: "category_name");

            migrationBuilder.CreateIndex(
                name: "IX_category_aggregations_category_type",
                table: "category_aggregations",
                column: "category_type");

            migrationBuilder.CreateIndex(
                name: "IX_pi_aggregations_investigator_name",
                table: "pi_aggregations",
                column: "investigator_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_aggregations");

            migrationBuilder.DropTable(
                name: "pi_aggregations");
        }
    }
}

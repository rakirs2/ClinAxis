using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestigatorMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "investigator_metrics",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    investigator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    h_index = table.Column<int>(type: "integer", nullable: true),
                    citation_count = table.Column<int>(type: "integer", nullable: true),
                    i10_index = table.Column<int>(type: "integer", nullable: true),
                    total_papers = table.Column<int>(type: "integer", nullable: true),
                    external_author_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lookup_attempted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lookup_result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    lookup_error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigator_metrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_investigator_metrics_investigator_persons_investigator_pers~",
                        column: x => x.investigator_person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_investigator_metrics_investigator_person_id",
                table: "investigator_metrics",
                column: "investigator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_investigator_metrics_investigator_person_id_source",
                table: "investigator_metrics",
                columns: new[] { "investigator_person_id", "source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investigator_metrics");
        }
    }
}

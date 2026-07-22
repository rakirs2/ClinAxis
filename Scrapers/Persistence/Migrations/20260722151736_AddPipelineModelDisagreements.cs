using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineModelDisagreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pipeline_model_disagreements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_based_result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rule_based_npi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bert_top_candidate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bert_top_score = table.Column<double>(type: "double precision", nullable: true),
                    bert_recommended_result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    disagreement_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    investigator_context = table.Column<string>(type: "text", nullable: true),
                    candidates_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipeline_model_disagreements", x => x.id);
                    table.ForeignKey(
                        name: "FK_pipeline_model_disagreements_investigator_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "investigator_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_model_disagreements_created_at",
                table: "pipeline_model_disagreements",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_model_disagreements_disagreement_type",
                table: "pipeline_model_disagreements",
                column: "disagreement_type");

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_model_disagreements_person_id",
                table: "pipeline_model_disagreements",
                column: "person_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pipeline_model_disagreements");
        }
    }
}

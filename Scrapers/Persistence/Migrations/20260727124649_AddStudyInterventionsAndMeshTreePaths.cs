using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyInterventionsAndMeshTreePaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mesh_tree_paths",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mesh_descriptor_id = table.Column<int>(type: "integer", nullable: false),
                    tree_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mesh_tree_paths", x => x.id);
                    table.ForeignKey(
                        name: "FK_mesh_tree_paths_mesh_descriptors_mesh_descriptor_id",
                        column: x => x.mesh_descriptor_id,
                        principalTable: "mesh_descriptors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_interventions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    study_nct_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    intervention_name = table.Column<string>(type: "text", nullable: true),
                    intervention_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    mesh_descriptor_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_interventions", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_interventions_mesh_descriptors_mesh_descriptor_id",
                        column: x => x.mesh_descriptor_id,
                        principalTable: "mesh_descriptors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_study_interventions_studies_study_nct_id",
                        column: x => x.study_nct_id,
                        principalTable: "studies",
                        principalColumn: "nct_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mesh_tree_paths_mesh_descriptor_id",
                table: "mesh_tree_paths",
                column: "mesh_descriptor_id");

            migrationBuilder.CreateIndex(
                name: "ix_mesh_tree_paths_tree_number_pattern",
                table: "mesh_tree_paths",
                column: "tree_number")
                .Annotation("Npgsql:IndexMethod", "btree");

            migrationBuilder.Sql("""
                INSERT INTO mesh_tree_paths (mesh_descriptor_id, tree_number)
                SELECT md.id, unnest(md.tree_numbers)
                FROM mesh_descriptors md
                WHERE md.tree_numbers IS NOT NULL
                """);

            migrationBuilder.CreateIndex(
                name: "IX_study_interventions_mesh_descriptor_id",
                table: "study_interventions",
                column: "mesh_descriptor_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_interventions_study_nct_id_intervention_type",
                table: "study_interventions",
                columns: new[] { "study_nct_id", "intervention_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mesh_tree_paths");

            migrationBuilder.DropTable(
                name: "study_interventions");
        }
    }
}

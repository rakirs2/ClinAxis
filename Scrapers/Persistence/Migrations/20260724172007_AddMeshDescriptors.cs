using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeshDescriptors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_study_conditions_condition",
                table: "study_conditions");

            migrationBuilder.DropColumn(
                name: "condition",
                table: "study_conditions");

            migrationBuilder.AddColumn<int>(
                name: "mesh_descriptor_id",
                table: "study_conditions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "rejected_conditions",
                table: "studies",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mesh_descriptors",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cui = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    tree_numbers = table.Column<string[]>(type: "text[]", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mesh_descriptors", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_conditions_mesh_descriptor_id",
                table: "study_conditions",
                column: "mesh_descriptor_id");

            migrationBuilder.CreateIndex(
                name: "IX_mesh_descriptors_cui",
                table: "mesh_descriptors",
                column: "cui",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mesh_descriptors_tree_numbers",
                table: "mesh_descriptors",
                column: "tree_numbers")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.AddForeignKey(
                name: "FK_study_conditions_mesh_descriptors_mesh_descriptor_id",
                table: "study_conditions",
                column: "mesh_descriptor_id",
                principalTable: "mesh_descriptors",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_study_conditions_mesh_descriptors_mesh_descriptor_id",
                table: "study_conditions");

            migrationBuilder.DropTable(
                name: "mesh_descriptors");

            migrationBuilder.DropIndex(
                name: "IX_study_conditions_mesh_descriptor_id",
                table: "study_conditions");

            migrationBuilder.DropColumn(
                name: "mesh_descriptor_id",
                table: "study_conditions");

            migrationBuilder.DropColumn(
                name: "rejected_conditions",
                table: "studies");

            migrationBuilder.AddColumn<string>(
                name: "condition",
                table: "study_conditions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_study_conditions_condition",
                table: "study_conditions",
                column: "condition");
        }
    }
}

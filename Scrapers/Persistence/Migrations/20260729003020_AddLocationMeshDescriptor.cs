using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationMeshDescriptor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MeshDescriptorId",
                table: "study_locations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_locations_MeshDescriptorId",
                table: "study_locations",
                column: "MeshDescriptorId");

            migrationBuilder.AddForeignKey(
                name: "FK_study_locations_mesh_descriptors_MeshDescriptorId",
                table: "study_locations",
                column: "MeshDescriptorId",
                principalTable: "mesh_descriptors",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_study_locations_mesh_descriptors_MeshDescriptorId",
                table: "study_locations");

            migrationBuilder.DropIndex(
                name: "IX_study_locations_MeshDescriptorId",
                table: "study_locations");

            migrationBuilder.DropColumn(
                name: "MeshDescriptorId",
                table: "study_locations");
        }
    }
}

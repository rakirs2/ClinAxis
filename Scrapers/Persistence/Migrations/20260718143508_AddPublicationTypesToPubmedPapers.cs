using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrapers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicationTypesToPubmedPapers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "publication_types",
                table: "pubmed_papers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "publication_types",
                table: "pubmed_papers");
        }
    }
}

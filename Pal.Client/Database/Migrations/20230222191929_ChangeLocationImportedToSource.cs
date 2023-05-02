using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Client.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChangeLocationImportedToSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Imported",
                table: "Locations",
                newName: "Source");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Source",
                table: "Locations",
                newName: "Imported");
        }
    }
}

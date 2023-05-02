using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Client.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddImportedAndSinceVersionToClientLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Imported",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SinceVersion",
                table: "Locations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Imported",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "SinceVersion",
                table: "Locations");
        }
    }
}

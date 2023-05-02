using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Server.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDebugMarkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM SeenLocation WHERE PalaceLocationId IN (SELECT Id FROM Locations WHERE Type = 3)");
            migrationBuilder.Sql("DELETE FROM Locations WHERE Type = 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

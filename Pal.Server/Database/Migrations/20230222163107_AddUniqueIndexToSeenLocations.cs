using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Server.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexToSeenLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeenLocation_AccountId",
                table: "SeenLocation");

            migrationBuilder.Sql(
                @"DELETE FROM SeenLocation
                WHERE EXISTS (
                  SELECT 1 FROM SeenLocation s2 
                  WHERE SeenLocation.AccountId = s2.AccountId
                  AND SeenLocation.PalaceLocationId = s2.PalaceLocationId
                  AND SeenLocation.rowid > s2.rowid
                )");

            migrationBuilder.CreateIndex(
                name: "IX_SeenLocation_AccountId_PalaceLocationId",
                table: "SeenLocation",
                columns: new[] { "AccountId", "PalaceLocationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeenLocation_AccountId_PalaceLocationId",
                table: "SeenLocation");

            migrationBuilder.CreateIndex(
                name: "IX_SeenLocation_AccountId",
                table: "SeenLocation",
                column: "AccountId");
        }
    }
}

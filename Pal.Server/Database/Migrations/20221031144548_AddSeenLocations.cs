using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Server.Database.Migrations
{
    public partial class AddSeenLocations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeenLocation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PalaceLocationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeenLocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeenLocation_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeenLocation_Locations_PalaceLocationId",
                        column: x => x.PalaceLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeenLocation_AccountId",
                table: "SeenLocation",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SeenLocation_PalaceLocationId",
                table: "SeenLocation",
                column: "PalaceLocationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeenLocation");
        }
    }
}

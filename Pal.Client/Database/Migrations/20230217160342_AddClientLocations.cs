using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Client.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddClientLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    LocalId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TerritoryType = table.Column<ushort>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    X = table.Column<float>(type: "REAL", nullable: false),
                    Y = table.Column<float>(type: "REAL", nullable: false),
                    Z = table.Column<float>(type: "REAL", nullable: false),
                    Seen = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.LocalId);
                });

            migrationBuilder.CreateTable(
                name: "LocationImports",
                columns: table => new
                {
                    ImportedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImportedLocationsLocalId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationImports", x => new { x.ImportedById, x.ImportedLocationsLocalId });
                    table.ForeignKey(
                        name: "FK_LocationImports_Imports_ImportedById",
                        column: x => x.ImportedById,
                        principalTable: "Imports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocationImports_Locations_ImportedLocationsLocalId",
                        column: x => x.ImportedLocationsLocalId,
                        principalTable: "Locations",
                        principalColumn: "LocalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemoteEncounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientLocationId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteEncounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteEncounters_Locations_ClientLocationId",
                        column: x => x.ClientLocationId,
                        principalTable: "Locations",
                        principalColumn: "LocalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocationImports_ImportedLocationsLocalId",
                table: "LocationImports",
                column: "ImportedLocationsLocalId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteEncounters_ClientLocationId",
                table: "RemoteEncounters",
                column: "ClientLocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocationImports");

            migrationBuilder.DropTable(
                name: "RemoteEncounters");

            migrationBuilder.DropTable(
                name: "Locations");
        }
    }
}

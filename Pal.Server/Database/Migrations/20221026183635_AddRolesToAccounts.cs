using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pal.Server.Database.Migrations
{
    public partial class AddRolesToAccounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Roles",
                table: "Accounts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Roles",
                table: "Accounts");
        }
    }
}

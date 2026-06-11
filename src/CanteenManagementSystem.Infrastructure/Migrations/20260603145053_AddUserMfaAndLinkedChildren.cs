using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanteenManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMfaAndLinkedChildren : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LinkedChildrenCsv",
                table: "AppUsers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MfaEnabled",
                table: "AppUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TotpSecret",
                table: "AppUsers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LinkedChildrenCsv",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "MfaEnabled",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "TotpSecret",
                table: "AppUsers");
        }
    }
}

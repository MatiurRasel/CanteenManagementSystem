using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanteenManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKioskDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppKioskDevices",
                columns: table => new
                {
                    KioskDeviceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TokenPrefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RedeemedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedByUserId = table.Column<int>(type: "int", nullable: true),
                    LastSeenIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppKioskDevices", x => x.KioskDeviceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppKioskDevices_ClientId",
                table: "AppKioskDevices",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKioskDevices_IsActive",
                table: "AppKioskDevices",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AppKioskDevices_TokenHash",
                table: "AppKioskDevices",
                column: "TokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppKioskDevices");
        }
    }
}

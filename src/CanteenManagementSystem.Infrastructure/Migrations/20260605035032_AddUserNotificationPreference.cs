using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanteenManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNotificationPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CanteenUserNotificationPreference",
                columns: table => new
                {
                    PreferenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TemplateKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenUserNotificationPreference", x => x.PreferenceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CanteenUserNotificationPreference_ClientId",
                table: "CanteenUserNotificationPreference",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenUserNotificationPreference_UserId_TemplateKey_Channel",
                table: "CanteenUserNotificationPreference",
                columns: new[] { "UserId", "TemplateKey", "Channel" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanteenUserNotificationPreference");
        }
    }
}

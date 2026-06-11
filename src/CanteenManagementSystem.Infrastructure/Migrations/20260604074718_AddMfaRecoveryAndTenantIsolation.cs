using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanteenManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaRecoveryAndTenantIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IsolationMode",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AppMfaRecoveryCodes",
                columns: table => new
                {
                    RecoveryCodeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CodePrefix = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppMfaRecoveryCodes", x => x.RecoveryCodeId);
                    table.ForeignKey(
                        name: "FK_AppMfaRecoveryCodes_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppMfaRecoveryCodes_CodeHash",
                table: "AppMfaRecoveryCodes",
                column: "CodeHash");

            migrationBuilder.CreateIndex(
                name: "IX_AppMfaRecoveryCodes_UserId",
                table: "AppMfaRecoveryCodes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppMfaRecoveryCodes");

            migrationBuilder.DropColumn(
                name: "IsolationMode",
                table: "Clients");
        }
    }
}

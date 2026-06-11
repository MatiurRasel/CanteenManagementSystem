using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanteenManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPreOrderLoyaltyPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPreOrder",
                table: "CanteenOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupAtUtc",
                table: "CanteenOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TableNumber",
                table: "CanteenOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscountRules",
                columns: table => new
                {
                    RuleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConditionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountRules", x => x.RuleId);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyAccounts",
                columns: table => new
                {
                    LoyaltyAccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserExternalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PointsBalance = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEarnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRedeemedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyAccounts", x => x.LoyaltyAccountId);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyEntries",
                columns: table => new
                {
                    LoyaltyEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoyaltyAccountId = table.Column<int>(type: "int", nullable: false),
                    EntryType = table.Column<int>(type: "int", nullable: false),
                    Delta = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyEntries", x => x.LoyaltyEntryId);
                    table.ForeignKey(
                        name: "FK_LoyaltyEntries_LoyaltyAccounts_LoyaltyAccountId",
                        column: x => x.LoyaltyAccountId,
                        principalTable: "LoyaltyAccounts",
                        principalColumn: "LoyaltyAccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_ClientId",
                table: "DiscountRules",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_IsActive_Priority",
                table: "DiscountRules",
                columns: new[] { "IsActive", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_RuleCode",
                table: "DiscountRules",
                column: "RuleCode");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyAccounts_ClientId",
                table: "LoyaltyAccounts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyAccounts_UserExternalId",
                table: "LoyaltyAccounts",
                column: "UserExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyEntries_ClientId",
                table: "LoyaltyEntries",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyEntries_LoyaltyAccountId",
                table: "LoyaltyEntries",
                column: "LoyaltyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyEntries_OrderId",
                table: "LoyaltyEntries",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscountRules");

            migrationBuilder.DropTable(
                name: "LoyaltyEntries");

            migrationBuilder.DropTable(
                name: "LoyaltyAccounts");

            migrationBuilder.DropColumn(
                name: "IsPreOrder",
                table: "CanteenOrders");

            migrationBuilder.DropColumn(
                name: "PickupAtUtc",
                table: "CanteenOrders");

            migrationBuilder.DropColumn(
                name: "TableNumber",
                table: "CanteenOrders");
        }
    }
}

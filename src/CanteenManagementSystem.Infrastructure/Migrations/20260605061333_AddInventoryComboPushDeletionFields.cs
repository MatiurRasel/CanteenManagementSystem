using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanteenManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryComboPushDeletionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Clients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Clients",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "Clients",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HoldUntilUtc",
                table: "Clients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CanteenComboButtons",
                columns: table => new
                {
                    ComboButtonId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FoodItemIdsCsv = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenComboButtons", x => x.ComboButtonId);
                });

            migrationBuilder.CreateTable(
                name: "CanteenPushSubscription",
                columns: table => new
                {
                    PushSubscriptionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    P256dh = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenPushSubscription", x => x.PushSubscriptionId);
                });

            migrationBuilder.CreateTable(
                name: "CanteenStockTakes",
                columns: table => new
                {
                    StockTakeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SubmittedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenStockTakes", x => x.StockTakeId);
                });

            migrationBuilder.CreateTable(
                name: "CanteenSuppliers",
                columns: table => new
                {
                    SupplierId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenSuppliers", x => x.SupplierId);
                });

            migrationBuilder.CreateTable(
                name: "CanteenWasteLog",
                columns: table => new
                {
                    WasteLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FoodItemID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenWasteLog", x => x.WasteLogId);
                    table.ForeignKey(
                        name: "FK_CanteenWasteLog_CanteenFoodItems_FoodItemID",
                        column: x => x.FoodItemID,
                        principalTable: "CanteenFoodItems",
                        principalColumn: "FoodItemID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CanteenStockTakeLines",
                columns: table => new
                {
                    StockTakeLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockTakeId = table.Column<int>(type: "int", nullable: false),
                    FoodItemID = table.Column<int>(type: "int", nullable: false),
                    ExpectedQty = table.Column<int>(type: "int", nullable: false),
                    CountedQty = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenStockTakeLines", x => x.StockTakeLineId);
                    table.ForeignKey(
                        name: "FK_CanteenStockTakeLines_CanteenFoodItems_FoodItemID",
                        column: x => x.FoodItemID,
                        principalTable: "CanteenFoodItems",
                        principalColumn: "FoodItemID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CanteenStockTakeLines_CanteenStockTakes_StockTakeId",
                        column: x => x.StockTakeId,
                        principalTable: "CanteenStockTakes",
                        principalColumn: "StockTakeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CanteenPurchaseOrders",
                columns: table => new
                {
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    OrderedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenPurchaseOrders", x => x.PurchaseOrderId);
                    table.ForeignKey(
                        name: "FK_CanteenPurchaseOrders_CanteenSuppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "CanteenSuppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CanteenPurchaseOrderLines",
                columns: table => new
                {
                    PurchaseOrderLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: false),
                    FoodItemID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenPurchaseOrderLines", x => x.PurchaseOrderLineId);
                    table.ForeignKey(
                        name: "FK_CanteenPurchaseOrderLines_CanteenFoodItems_FoodItemID",
                        column: x => x.FoodItemID,
                        principalTable: "CanteenFoodItems",
                        principalColumn: "FoodItemID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CanteenPurchaseOrderLines_CanteenPurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "CanteenPurchaseOrders",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CanteenComboButtons_ClientId",
                table: "CanteenComboButtons",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenComboButtons_Code",
                table: "CanteenComboButtons",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPurchaseOrderLines_FoodItemID",
                table: "CanteenPurchaseOrderLines",
                column: "FoodItemID");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPurchaseOrderLines_PurchaseOrderId",
                table: "CanteenPurchaseOrderLines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPurchaseOrders_ClientId",
                table: "CanteenPurchaseOrders",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPurchaseOrders_PoNumber",
                table: "CanteenPurchaseOrders",
                column: "PoNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPurchaseOrders_SupplierId",
                table: "CanteenPurchaseOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPushSubscription_ClientId",
                table: "CanteenPushSubscription",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPushSubscription_UserId_Endpoint",
                table: "CanteenPushSubscription",
                columns: new[] { "UserId", "Endpoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanteenStockTakeLines_FoodItemID",
                table: "CanteenStockTakeLines",
                column: "FoodItemID");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenStockTakeLines_StockTakeId",
                table: "CanteenStockTakeLines",
                column: "StockTakeId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenStockTakes_ClientId",
                table: "CanteenStockTakes",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenSuppliers_ClientId",
                table: "CanteenSuppliers",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenSuppliers_Name",
                table: "CanteenSuppliers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenWasteLog_ClientId",
                table: "CanteenWasteLog",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenWasteLog_FoodItemID",
                table: "CanteenWasteLog",
                column: "FoodItemID");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenWasteLog_OccurredAtUtc",
                table: "CanteenWasteLog",
                column: "OccurredAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanteenComboButtons");

            migrationBuilder.DropTable(
                name: "CanteenPurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "CanteenPushSubscription");

            migrationBuilder.DropTable(
                name: "CanteenStockTakeLines");

            migrationBuilder.DropTable(
                name: "CanteenWasteLog");

            migrationBuilder.DropTable(
                name: "CanteenPurchaseOrders");

            migrationBuilder.DropTable(
                name: "CanteenStockTakes");

            migrationBuilder.DropTable(
                name: "CanteenSuppliers");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "HoldUntilUtc",
                table: "Clients");
        }
    }
}

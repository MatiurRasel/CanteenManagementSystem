using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanteenManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDietaryAllergiesPrepShelfFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Allergies",
                table: "Students",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DietaryNotes",
                table: "Students",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Allergies",
                table: "Employees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DietaryNotes",
                table: "Employees",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LowStockThreshold",
                table: "CanteenFoodItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrepTimeSeconds",
                table: "CanteenFoodItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShelfLifeHours",
                table: "CanteenFoodItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CanteenUserFavorites",
                columns: table => new
                {
                    FavoriteId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FoodItemId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenUserFavorites", x => x.FavoriteId);
                    table.ForeignKey(
                        name: "FK_CanteenUserFavorites_CanteenFoodItems_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "CanteenFoodItems",
                        principalColumn: "FoodItemID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CanteenUserFavorites_ClientId",
                table: "CanteenUserFavorites",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenUserFavorites_FoodItemId",
                table: "CanteenUserFavorites",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenUserFavorites_UserId_FoodItemId",
                table: "CanteenUserFavorites",
                columns: new[] { "UserId", "FoodItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanteenUserFavorites");

            migrationBuilder.DropColumn(
                name: "Allergies",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DietaryNotes",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Allergies",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DietaryNotes",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LowStockThreshold",
                table: "CanteenFoodItems");

            migrationBuilder.DropColumn(
                name: "PrepTimeSeconds",
                table: "CanteenFoodItems");

            migrationBuilder.DropColumn(
                name: "ShelfLifeHours",
                table: "CanteenFoodItems");
        }
    }
}

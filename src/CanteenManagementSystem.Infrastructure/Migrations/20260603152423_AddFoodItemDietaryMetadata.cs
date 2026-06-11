using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanteenManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodItemDietaryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Allergens",
                table: "CanteenFoodItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHalal",
                table: "CanteenFoodItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVegetarian",
                table: "CanteenFoodItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "KCalories",
                table: "CanteenFoodItems",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Allergens",
                table: "CanteenFoodItems");

            migrationBuilder.DropColumn(
                name: "IsHalal",
                table: "CanteenFoodItems");

            migrationBuilder.DropColumn(
                name: "IsVegetarian",
                table: "CanteenFoodItems");

            migrationBuilder.DropColumn(
                name: "KCalories",
                table: "CanteenFoodItems");
        }
    }
}

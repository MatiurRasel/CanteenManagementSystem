// =============================================================================
// IPublicMenuService  (CanteenManagementSystem.Application.Menus)
// -----------------------------------------------------------------------------
// Read-only menu queries for the public display board, kiosk, and table-order
// surfaces. Used by /Display/Menu, /kiosk, /t/{table} (ADR 0004).
// =============================================================================

using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Application.Menus;

public sealed record PublicMenuItem(
    int DailyMenuId, int FoodItemId, int ItemNumber,
    string ItemName, decimal Price, CanteenMealType? Category,
    int AvailableQuantity, bool IsAvailable,
    string ImageUrl, string Description)
{
    /// <summary>Vegetarian. NULL = the flag isn't set on this item (treated as false in UI).</summary>
    public bool IsVegetarian { get; init; }

    /// <summary>Halal-prepared.</summary>
    public bool IsHalal { get; init; }

    /// <summary>Calories per serving. NULL = not entered.</summary>
    public int? KCalories { get; init; }

    /// <summary>Comma-separated allergens (e.g. "nuts, dairy, gluten"). Empty when unset.</summary>
    public string Allergens { get; init; } = string.Empty;
}

public interface IPublicMenuService
{
    Task<IReadOnlyList<PublicMenuItem>> GetTodayMenuAsync(DateTime today, CancellationToken ct = default);
}

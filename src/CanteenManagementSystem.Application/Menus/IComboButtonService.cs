// =============================================================================
// IComboButtonService  (CanteenManagementSystem.Application.Menus)
// -----------------------------------------------------------------------------
// Admin CRUD + counter-side resolution for combo / meal-deal quick buttons.
//
// "Resolve" parses the FoodItemIdsCsv into a usable shopping list (FoodItem
// ids + display names + line totals) so the counter can place an order with
// one tap.
// =============================================================================

using CanteenManagementSystem.Domain.Menu;

namespace CanteenManagementSystem.Application.Menus;

public sealed record ComboButtonListItem(
    int ComboButtonId, string Code, string DisplayName, string? Color, int SortOrder,
    bool IsActive, int ItemCount);

public sealed record ComboButtonInput(
    string Code, string DisplayName, string? Color, IReadOnlyList<int> FoodItemIds, int SortOrder, bool IsActive);

public sealed record ResolvedComboItem(int FoodItemId, string ItemName, decimal Price);

public sealed record ResolvedCombo(
    int ComboButtonId, string Code, string DisplayName,
    IReadOnlyList<ResolvedComboItem> Items, decimal TotalPrice);

public interface IComboButtonService
{
    Task<IReadOnlyList<ComboButtonListItem>> ListAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<ComboButton?>                       GetAsync(int id, CancellationToken ct = default);
    Task<ComboButton>                        CreateAsync(ComboButtonInput input, CancellationToken ct = default);
    Task<bool>                               UpdateAsync(int id, ComboButtonInput input, CancellationToken ct = default);
    Task<bool>                               DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Resolve every active combo into a counter-ready payload (parsed CSV + joined FoodItem).</summary>
    Task<IReadOnlyList<ResolvedCombo>>       ResolveActiveAsync(CancellationToken ct = default);
}

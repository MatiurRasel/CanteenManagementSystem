// =============================================================================
// IUserFavoritesService  (CanteenManagementSystem.Application.Favorites)
// -----------------------------------------------------------------------------
// Manages a user's saved favourites — the "re-order" / "one-tap" pattern
// surfaced from /me/orders and the kiosk flow.
// =============================================================================

namespace CanteenManagementSystem.Application.Favorites;

public sealed record FavoriteItemDto(
    long FavoriteId,
    int FoodItemId,
    string ItemName,
    decimal Price,
    string? ImageUrl,
    DateTime CreatedAtUtc);

public interface IUserFavoritesService
{
    /// <summary>List the user's favourites (most-recent first).</summary>
    Task<IReadOnlyList<FavoriteItemDto>> ListAsync(string userId, CancellationToken ct = default);

    /// <summary>Add an item to the user's favourites. No-op (returns existing) if already present.</summary>
    Task<FavoriteItemDto> AddAsync(string userId, int foodItemId, CancellationToken ct = default);

    /// <summary>Remove a favourite by FavoriteId. Returns false when the row doesn't belong to the user.</summary>
    Task<bool> RemoveAsync(string userId, long favoriteId, CancellationToken ct = default);

    /// <summary>True if the user has favourited this item.</summary>
    Task<bool> IsFavoriteAsync(string userId, int foodItemId, CancellationToken ct = default);
}

// =============================================================================
// UserFavoritesService  (CanteenManagementSystem.Infrastructure.Favorites)
// -----------------------------------------------------------------------------
// ADR 0004: IUnitOfWork + IReadOnlyRepository<FoodItem>.
// =============================================================================

using CanteenManagementSystem.Application.Favorites;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Favorites;

internal sealed class UserFavoritesService : IUserFavoritesService
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<FoodItem> _foods;

    public UserFavoritesService(IUnitOfWork uow, IReadOnlyRepository<FoodItem> foods)
    {
        _uow = uow; _foods = foods;
    }

    private IRepository<UserFavorite> Favorites => _uow.Repository<UserFavorite>();

    public async Task<IReadOnlyList<FavoriteItemDto>> ListAsync(string userId, CancellationToken ct = default)
        => await Favorites.NoTrackingQuery()
            .Where(f => f.UserId == userId)
            .Include(f => f.FoodItem)
            .OrderByDescending(f => f.CreatedAtUtc)
            .Select(f => new FavoriteItemDto(
                f.FavoriteId,
                f.FoodItemId,
                f.FoodItem!.ItemName,
                f.FoodItem.Price,
                f.FoodItem.ImageUrl,
                f.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<FavoriteItemDto> AddAsync(string userId, int foodItemId, CancellationToken ct = default)
    {
        var existing = await Favorites.FirstOrDefaultAsync(
            f => f.UserId == userId && f.FoodItemId == foodItemId, ct);
        if (existing is not null)
        {
            var item = await _foods.FirstOrDefaultAsync(f => f.FoodItemID == foodItemId, ct)
                ?? throw new InvalidOperationException("FoodItem missing.");
            return new FavoriteItemDto(existing.FavoriteId, foodItemId, item.ItemName, item.Price, item.ImageUrl, existing.CreatedAtUtc);
        }

        var food = await _foods.FirstOrDefaultAsync(f => f.FoodItemID == foodItemId, ct);
        if (food is null) throw new InvalidOperationException("FoodItem not found.");

        var row = new UserFavorite
        {
            UserId = userId,
            FoodItemId = foodItemId,
            CreatedAtUtc = DateTime.UtcNow
        };
        await Favorites.AddAsync(row, ct);
        await _uow.SaveChangesAsync(ct);

        return new FavoriteItemDto(row.FavoriteId, foodItemId, food.ItemName, food.Price, food.ImageUrl, row.CreatedAtUtc);
    }

    public async Task<bool> RemoveAsync(string userId, long favoriteId, CancellationToken ct = default)
    {
        var row = await Favorites.FirstOrDefaultAsync(f => f.FavoriteId == favoriteId && f.UserId == userId, ct);
        if (row is null) return false;
        Favorites.Remove(row);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> IsFavoriteAsync(string userId, int foodItemId, CancellationToken ct = default)
        => Favorites.AnyAsync(f => f.UserId == userId && f.FoodItemId == foodItemId, ct);
}

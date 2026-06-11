// =============================================================================
// IVerificationSessionService  (CanteenManagementSystem.Application.Verifications)
// -----------------------------------------------------------------------------
// Counter scan + keypad-order data layer. Used by VerificationController so it
// no longer touches IAppDbContext (ADR 0004).
// =============================================================================

using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Wallets;

namespace CanteenManagementSystem.Application.Verifications;

public sealed record ScannedUser(
    string UserId, string UserName, CanteenUserType UserType,
    string PhotoPath, string UserInfo, string? EmployeeTypeName);

public sealed record MenuTile(
    int DailyMenuId, int FoodItemId, string ItemName, decimal Price,
    int AvailableQuantity, bool IsAvailable, CanteenMealType? Category);

public interface IVerificationSessionService
{
    Task<ScannedUser?> LookupByIdentifierAsync(string identifier, CancellationToken ct = default);

    /// <summary>Ensure a UserBalance row exists for this (user, type). Resets monthly cap on first hit of a new month.</summary>
    Task<UserBalance> EnsureMonthlyBalanceAsync(string userId, CanteenUserType userType, decimal monthlyLimit, CancellationToken ct = default);

    Task<IReadOnlyList<MenuTile>> GetTodayMenuTilesAsync(CancellationToken ct = default);

    /// <summary>Resolve the DailyMenu at the given 1-based position in today's ordered menu.</summary>
    Task<DailyMenu?> GetDailyMenuByPositionAsync(int itemNumber, CancellationToken ct = default);
}

// =============================================================================
// UserFavorite  (CanteenManagementSystem.Domain.Users)
// -----------------------------------------------------------------------------
// One row per (UserId, FoodItemId) favourite. Powers /me/favorites — students
// and employees can save items and re-order them with one tap from /me/orders
// or the kiosk flow.
//
// SCHEMA NOTES
//   * UserId is the external identifier (Students.ExternalId / Employees.ExternalId).
//   * Unique on (ClientId, UserId, FoodItemId) so toggling is idempotent.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;
using CanteenManagementSystem.Domain.Menu;

namespace CanteenManagementSystem.Domain.Users;

[Table("CanteenUserFavorites")]
public class UserFavorite : ITenantOwned
{
    [Key] public long FavoriteId { get; set; }

    [Required, StringLength(50)]
    public string UserId { get; set; } = string.Empty;

    public int FoodItemId { get; set; }

    public FoodItem? FoodItem { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

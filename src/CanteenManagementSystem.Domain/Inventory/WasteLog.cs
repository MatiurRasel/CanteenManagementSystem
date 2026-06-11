// =============================================================================
// WasteLog  (CanteenManagementSystem.Domain.Inventory)
// -----------------------------------------------------------------------------
// Records disposal of food items — past-expiry, dropped, contamination, spoilage.
// Surfaces to the wastage report so management can trend loss.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CanteenManagementSystem.Domain.Menu;
using Platform.Domain.Common;

namespace CanteenManagementSystem.Domain.Inventory;

public enum WasteReason
{
    Spoilage = 0,
    Expired  = 1,
    Dropped  = 2,
    Contamination = 3,
    PrepError = 4,
    Other = 9
}

[Table("CanteenWasteLog")]
public class WasteLog : ITenantOwned
{
    [Key] public long WasteLogId { get; set; }

    public int FoodItemID { get; set; }
    public FoodItem? FoodItem { get; set; }

    public int Quantity { get; set; }
    public WasteReason Reason { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
    [StringLength(64)] public string? PerformedBy { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

// =============================================================================
// StockTake + StockTakeLine  (CanteenManagementSystem.Domain.Inventory)
// -----------------------------------------------------------------------------
// Periodic physical count vs system count. The admin opens a new StockTake,
// the service materialises one StockTakeLine per active FoodItem with the
// current ExpectedQty pre-filled, the operator walks the shelves keying in
// CountedQty, then Submits to capture a snapshot. Variance is calculated +
// stored for the audit report.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CanteenManagementSystem.Domain.Menu;
using Platform.Domain.Common;

namespace CanteenManagementSystem.Domain.Inventory;

public enum StockTakeStatus { Draft = 0, Submitted = 1, Cancelled = 2 }

[Table("CanteenStockTakes")]
public class StockTake : IAggregateRoot, ITenantOwned
{
    [Key] public int StockTakeId { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    [StringLength(64)] public string? StartedBy { get; set; }
    [StringLength(64)] public string? SubmittedBy { get; set; }
    [StringLength(500)] public string? Notes { get; set; }

    public StockTakeStatus Status { get; set; } = StockTakeStatus.Draft;

    public ICollection<StockTakeLine> Lines { get; set; } = new List<StockTakeLine>();
}

[Table("CanteenStockTakeLines")]
public class StockTakeLine
{
    [Key] public int StockTakeLineId { get; set; }

    public int StockTakeId { get; set; }
    public StockTake? StockTake { get; set; }

    public int FoodItemID { get; set; }
    public FoodItem? FoodItem { get; set; }

    public int ExpectedQty { get; set; }
    public int CountedQty { get; set; }
    public int VarianceQty => CountedQty - ExpectedQty;
}

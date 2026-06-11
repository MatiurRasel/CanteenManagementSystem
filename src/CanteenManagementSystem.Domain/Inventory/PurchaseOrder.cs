// =============================================================================
// PurchaseOrder + PurchaseOrderLine  (CanteenManagementSystem.Domain.Inventory)
// -----------------------------------------------------------------------------
// Records intent to buy. Status transitions: Draft → Submitted → Received
// (terminal) or Cancelled (terminal). Received POs add their line quantities
// to today's DailyMenu via IPurchaseOrderService.ReceiveAsync.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CanteenManagementSystem.Domain.Menu;
using Platform.Domain.Common;

namespace CanteenManagementSystem.Domain.Inventory;

public enum PurchaseOrderStatus { Draft = 0, Submitted = 1, Received = 2, Cancelled = 3 }

[Table("CanteenPurchaseOrders")]
public class PurchaseOrder : IAggregateRoot, ITenantOwned
{
    [Key] public int PurchaseOrderId { get; set; }

    [Required, StringLength(40)] public string PoNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateTime OrderedAtUtc   { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedAtUtc { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    [StringLength(500)] public string? Notes { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

[Table("CanteenPurchaseOrderLines")]
public class PurchaseOrderLine
{
    [Key] public int PurchaseOrderLineId { get; set; }

    public int PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public int FoodItemID { get; set; }
    public FoodItem? FoodItem { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(10,2)")] public decimal UnitCost { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal LineTotal { get; set; }
}

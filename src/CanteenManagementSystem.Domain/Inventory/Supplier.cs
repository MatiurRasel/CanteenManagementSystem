// =============================================================================
// Supplier  (CanteenManagementSystem.Domain.Inventory)
// -----------------------------------------------------------------------------
// Vendor / wholesaler the canteen orders raw stock from. Lightweight CRUD —
// designed for the small-shop case (one tenant has maybe 5–20 suppliers).
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace CanteenManagementSystem.Domain.Inventory;

[Table("CanteenSuppliers")]
public class Supplier : ITenantOwned
{
    [Key] public int SupplierId { get; set; }

    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(50)]  public string? ContactName { get; set; }
    [StringLength(30)]  public string? ContactNo   { get; set; }
    [StringLength(150)] public string? Email       { get; set; }
    [StringLength(500)] public string? Address     { get; set; }
    [StringLength(500)] public string? Notes       { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

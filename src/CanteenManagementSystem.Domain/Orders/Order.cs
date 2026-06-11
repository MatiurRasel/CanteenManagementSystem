#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Wallets;

namespace CanteenManagementSystem.Domain.Orders;

[Table("CanteenOrders")]
public class Order : IAggregateRoot, ITenantOwned
{
    [Key]
    public int OrderID { get; set; }

    [StringLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required, StringLength(15)]
    public required string UserId { get; set; }

    [Required]
    public CanteenUserType UserType { get; set; }

    [Required, Column(TypeName = "decimal(10,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    public CanteenOrderStatus Status { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.Now;
    public DateTime? DeliveredDate { get; set; }

    // ─── Pre-order / order-ahead (P2) ─────────────────────────────────────
    /// <summary>
    /// When the customer plans to pick up. NULL = walk-up order (the default
    /// keypad flow). Set when the order is placed via the parent portal or
    /// QR-on-table flow for a future meal slot.
    /// </summary>
    public DateTime? PickupAtUtc { get; set; }

    /// <summary>True iff PickupAtUtc is in the future at placement time. Frozen at order time so a late delivery doesn't reclassify retroactively.</summary>
    public bool IsPreOrder { get; set; }

    /// <summary>For QR-on-table flows — labels the order with a table number for the kitchen ticket.</summary>
    [StringLength(20)]
    public string? TableNumber { get; set; }

    [StringLength(100)]
    public string? InputSequence { get; set; }

    [StringLength(64)]
    public string? IdempotencyKey { get; set; }

    public byte[]? RowVersion { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    [ForeignKey("UserId")]
    public UserBalance? UserBalance { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;
using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Domain.Menu;

[Table("CanteenFoodItems")]
public class FoodItem : IAggregateRoot, ITenantOwned
{
    [Key]
    public int FoodItemID { get; set; }

    [Required, StringLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required, Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    public CanteenMealType? Category { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; } = true;
    public bool IsActive { get; set; } = true;

    // ─── Dietary metadata (P2) ─────────────────────────────────────────────
    // Optional fields the UI uses to render coloured chips on the menu board
    // and the consumer-facing kiosk + parent portal. The display board can
    // filter by these flags so parents with allergies / dietary restrictions
    // see only items their child can eat.

    /// <summary>Pure-vegetarian (no meat/fish/egg).</summary>
    public bool IsVegetarian { get; set; }

    /// <summary>Halal-certified.</summary>
    public bool IsHalal { get; set; }

    /// <summary>Comma-separated allergen keywords ("nuts, dairy, gluten, egg, soy"). Lower-case, no spaces.</summary>
    [StringLength(200)]
    public string? Allergens { get; set; }

    /// <summary>Approximate kcal per serving. NULL when unknown.</summary>
    public int? KCalories { get; set; }

    // ─── Kitchen + inventory metadata (2026-06-05) ────────────────────────

    /// <summary>Average preparation time in seconds. KDS uses this to project
    /// queue completion + show estimates on the menu board. NULL = "unknown".</summary>
    public int? PrepTimeSeconds { get; set; }

    /// <summary>Per-item override for the low-stock alert threshold. NULL falls
    /// back to <c>Inventory.LowStockThreshold</c> tenant setting (default 5).</summary>
    public int? LowStockThreshold { get; set; }

    /// <summary>Shelf life in hours from prep. The /admin/inventory/expiry page lists
    /// items past their shelf life so the kitchen can pull them. NULL = no expiry.</summary>
    public int? ShelfLifeHours { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

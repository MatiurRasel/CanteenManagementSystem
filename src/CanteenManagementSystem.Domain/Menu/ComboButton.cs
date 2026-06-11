// =============================================================================
// ComboButton  (CanteenManagementSystem.Domain.Menu)
// -----------------------------------------------------------------------------
// Counter quick-button definition. A combo is N FoodItem ids stitched together
// with a friendly display name + colour swatch. The counter renders them as a
// strip of large touch targets above the keypad — one tap places the combo as
// a single order with all its line items.
//
// SCHEMA NOTES
//   * Code is the short alphanumeric identifier shown on the button (e.g. "L1",
//     "BFAST"). Unique per tenant.
//   * FoodItemIdsCsv is a comma-separated list of FoodItemID values. The
//     consumer parses on-demand; we don't FK to keep the row trivially
//     reorderable + deleteable.
//   * Color is a CSS-compatible value ("#ff5500" / "var(--accent)" / etc.) so
//     operators can colour-code combos for fast visual hits.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace CanteenManagementSystem.Domain.Menu;

[Table("CanteenComboButtons")]
public class ComboButton : ITenantOwned
{
    [Key] public int ComboButtonId { get; set; }

    [Required, StringLength(20)] public string Code        { get; set; } = string.Empty;
    [Required, StringLength(80)] public string DisplayName { get; set; } = string.Empty;

    /// <summary>Comma-separated FoodItem ids that make up this combo.</summary>
    [Required, StringLength(500)] public string FoodItemIdsCsv { get; set; } = string.Empty;

    [StringLength(20)] public string? Color { get; set; }

    public int  SortOrder { get; set; }
    public bool IsActive  { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

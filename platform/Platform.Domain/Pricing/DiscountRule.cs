// =============================================================================
// DiscountRule  (Platform.Domain.Pricing)
// -----------------------------------------------------------------------------
// Tenant-scoped pricing rules. The DiscountEngine evaluates rules at order
// placement time, picks the best one (or the configured stack), and emits
// the applied discount onto Order.TotalAmount + an audit row.
//
// CONDITION + EFFECT are JSON so we can ship one row schema with many rule
// types. Examples:
//
//   { "type": "DayOfWeekRole",  "day": "Tuesday", "roles": ["TEACHER"] }
//   { "type": "ComboItems",     "foodItemIds": [12, 15] }
//   { "type": "TimeOfDay",      "from": "13:30", "to": "14:00" }
//
//   { "type": "Percent",        "amount": 10 }                  // 10% off
//   { "type": "Flat",           "amount": 20 }                  // ৳20 off
//   { "type": "FixedTotal",     "amount": 120 }                 // combo = ৳120
//
// SHIPPING WHAT
//   The platform ships the *entity* + the *engine seam*. Concrete rule
//   evaluation lives in DiscountEngine — products extend by registering
//   IDiscountConditionEvaluator implementations.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Pricing;

[Table("DiscountRules")]
public class DiscountRule : ITenantOwned
{
    [Key] public int RuleId { get; set; }

    [Required, StringLength(64)]
    public string RuleCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>JSON condition — see ConditionEvaluator implementations.</summary>
    [Required, Column(TypeName = "nvarchar(max)")]
    public string ConditionJson { get; set; } = "{}";

    /// <summary>JSON effect — see EffectApplier implementations.</summary>
    [Required, Column(TypeName = "nvarchar(max)")]
    public string EffectJson { get; set; } = "{}";

    public int Priority { get; set; }                 // higher = wins when stacked rules conflict
    public bool IsActive { get; set; } = true;

    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

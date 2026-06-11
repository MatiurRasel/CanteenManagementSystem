// =============================================================================
// TenantSetting  (Domain.Tenancy)
// -----------------------------------------------------------------------------
// A per-tenant key/value store for *everything that should be configurable
// without a redeploy*. Examples: bKash credentials, Twilio API key, monthly
// limit overrides, VAT rate, sender ids, branding overrides.
//
// CONFIG PRIORITY (resolved by ITenantSettings)
//   1. DB row in [CanteenTenantSettings] for the current tenant   (HIGHEST)
//   2. appsettings.json key (e.g. "Payments:Bkash:AppKey")
//   3. hard-coded fallback default                                (LOWEST)
//
// WHY a single bag, not strongly-typed tables per concern?
//   - Adding a new tuning knob shouldn't require a migration.
//   - Operators can edit values via the admin UI without dev intervention.
//   - Sensitive values (API keys) live encrypted at rest (IsSecret flag).
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Tenancy;

[Table("CanteenTenantSettings")]
public class TenantSetting : ITenantOwned
{
    [Key]
    public long SettingId { get; set; }

    /// <summary>Dot-delimited key. e.g. "Payments.Bkash.AppKey", "VAT.Rate", "Sms.SenderId".</summary>
    [Required, StringLength(200)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Raw value stored as a string. Typed accessors live in <c>ITenantSettings</c>.</summary>
    [StringLength(4000)]
    public string? Value { get; set; }

    /// <summary>Free-text purpose so the admin UI can show context next to each row.</summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>Mark API keys, secrets, tokens. Encrypted at rest via the value protector.</summary>
    public bool IsSecret { get; set; }

    /// <summary>Last modified at — used by audit and cache invalidation tag triggers.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? UpdatedBy { get; set; }
}

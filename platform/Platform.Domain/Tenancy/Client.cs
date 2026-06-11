// =============================================================================
// Client  (Platform.Domain.Tenancy)
// -----------------------------------------------------------------------------
// The tenant table. Every tenant-scoped entity (ITenantOwned) carries a
// ClientId shadow column that points at Client.ClientId here. Replaces the
// legacy UTClientInfo / UTClientSettings pair that came from the cc24 school
// portal — branding + Azure photo container path now belong in
// CanteenTenantSettings (via ITenantSettings).
//
// MULTI-TENANCY MODEL
//   * One row per organisation that uses the SaaS.
//   * ClientCode is the public, URL-safe identifier ("acme.canteen.example.com"
//     → ClientCode "acme"). Unique constraint enforces that.
//   * ClientId is the internal numeric tenant id stamped onto every row.
//   * IsolationMode picks how this tenant's data is stored:
//       Shared    — current default; data lives in the platform-shared DB with
//                   a ClientId discriminator and EF global query filters.
//       Dedicated — reserved for the future "enterprise" tier. The
//                   ITenantConnectionResolver routes this tenant to its own
//                   database; the shared-DB row remains a directory record.
//
// WHY this lives in Platform, not Canteen
//   The canteen, rent and clinic products each have their own DB, but every
//   product needs a tenant directory. Keeping Client in Platform.Domain
//   guarantees a uniform shape so cross-cutting code (ITenantContext, the EF
//   filter, the seed pipeline, the admin UI) stays identical across products.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Domain.Tenancy;

/// <summary>
/// Tenancy storage strategy for one Client row.
/// See <c>docs/ADR/0001-multi-tenancy.md</c> for the design.
/// </summary>
public enum TenantIsolationMode
{
    /// <summary>Default — shared DB with ClientId discriminator + EF global query filter.</summary>
    Shared = 0,

    /// <summary>Reserved for the enterprise tier — dedicated DB per tenant via ITenantConnectionResolver.</summary>
    Dedicated = 1,
}

[Table("Clients")]
public class Client
{
    [Key]
    public int ClientId { get; set; }

    [Required, StringLength(50)]
    public string ClientCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string ClientName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ShortName { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [StringLength(200)]
    public string? Email { get; set; }

    [StringLength(200)]
    public string? WebsiteUrl { get; set; }

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Storage strategy — defaults to <see cref="TenantIsolationMode.Shared"/>.</summary>
    public TenantIsolationMode IsolationMode { get; set; } = TenantIsolationMode.Shared;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // ─── Soft-delete + hold-period ────────────────────────────────────────
    // A tenant marked DeletedAtUtc is immediately inaccessible (UseTenant
    // middleware treats it as 404). The hard-delete sweeper waits until
    // HoldUntilUtc has passed before cascading the row + all tenant-scoped
    // data to disk. This gives admins a recovery window after a mistake.

    public DateTime? DeletedAtUtc { get; set; }
    public DateTime? HoldUntilUtc { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string? DeletionReason { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(64)]
    public string? DeletedBy { get; set; }
}

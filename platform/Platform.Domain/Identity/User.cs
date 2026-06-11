// =============================================================================
// User  (Platform.Domain.Identity)
// -----------------------------------------------------------------------------
// Application-level user (operator / admin / student / parent). NOT the same
// as a StudentInfo / EmployeeInfo row pulled from the school's source-of-truth
// systems — those identify the *person*. This row authenticates an *account*
// that may belong to a person identified by a LinkedPersonId.
//
// Authentication:  PasswordHash (BCrypt) + RefreshToken rotation.
// Authorisation:   collection of Roles, each with Permissions.
// Tenancy:         EXPLICIT ClientId FK to Clients.
//                  NULL  = cross-tenant SystemAdmin (can act on any tenant)
//                  set   = the only tenant this user can sign into / manage.
//
// Why NOT ITenantOwned:
//   Login looks up by UserName BEFORE any tenant context exists for the request.
//   If User were globally filtered by tenant, a TenantAdmin from tenant 2 would
//   be invisible when the default-resolved tenant context is 1 — they could
//   never log in. ClientId stays explicit; AuthService scopes downstream
//   request-tenant context off this field.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;
using Platform.Domain.Tenancy;

namespace Platform.Domain.Identity;

[Table("AppUsers")]
public class User : IAggregateRoot
{
    [Key]
    public int UserId { get; set; }

    /// <summary>
    /// Tenant this user belongs to. NULL means "cross-tenant" (SystemAdmin only).
    /// FK to Clients.ClientId.
    /// </summary>
    public int? ClientId { get; set; }

    [ForeignKey(nameof(ClientId))]
    public Client? Tenant { get; set; }

    [Required, StringLength(64)]
    public string UserName { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(256)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Required, StringLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Link to the canonical person record in a product DB (e.g. StudentInfo.StudentID).</summary>
    [StringLength(32)]
    public string? LinkedPersonId { get; set; }

    /// <summary>Discriminator: "Operator", "Admin", "Student", "Parent", "Teacher". Free-text so products extend.</summary>
    [Required, StringLength(32)]
    public string UserKind { get; set; } = "Operator";

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public int  FailedLoginCount { get; set; }
    public DateTime? LockedOutUntilUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    [StringLength(45)] public string? LastLoginIp { get; set; }

    // ─── Multi-factor auth (TOTP / RFC 6238) ──────────────────────────────
    /// <summary>Base32-encoded shared secret used to derive 6-digit codes. NULL = MFA not enrolled.</summary>
    [StringLength(64)]
    public string? TotpSecret { get; set; }

    /// <summary>When true, login REQUIRES a valid TOTP code in addition to the password.</summary>
    public bool MfaEnabled { get; set; }

    // ─── Parent-child linkage (Parent portal) ─────────────────────────────
    /// <summary>
    /// Comma-separated list of child <c>ExternalId</c>s a Parent user is
    /// allowed to view / top-up. Read by the Parent portal; ignored otherwise.
    /// </summary>
    [StringLength(2000)]
    public string? LinkedChildrenCsv { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public byte[]? RowVersion { get; set; }

    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

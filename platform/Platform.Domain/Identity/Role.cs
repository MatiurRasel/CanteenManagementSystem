// =============================================================================
// Role / Permission / UserRole / RolePermission  (Platform.Domain.Identity)
// -----------------------------------------------------------------------------
// RBAC dictionary tables — GLOBAL across tenants. A tenant doesn't define
// their own "Operator" role; everyone sees the same role catalogue. What
// differs PER USER is the rows in AppUserRoles (which user has which roles)
// and the user's tenant on the User entity itself.
//
// SEEDED DEFAULTS (PermissionsSeed + RolesSeed in Platform.Infrastructure)
//   Role         Permissions
//   ─────────    ──────────────────────────────────────────
//   SystemAdmin  ALL (wildcard)
//   TenantAdmin  Wallet.Manage, Order.View, Menu.Manage, Card.Manage,
//                Reports.View, Audit.View, Gateway.Manage, ApiKey.Manage,
//                User.Manage
//   Operator     Wallet.View, Order.Place, Order.Deliver, Order.View,
//                Card.Read, Verification.Run
//   Cashier      Order.Place, Order.Deliver, Wallet.View
//   Auditor      Reports.View, Audit.View, Order.View
//   Parent       Wallet.View, Order.View
//   Student      Wallet.View, Order.Place
//
// NOT ITenantOwned — same row visible to every tenant.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Identity;

[Table("AppRoles")]
public class Role
{
    [Key]
    public int RoleId { get; set; }

    [Required, StringLength(64)]
    public string RoleCode { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}

[Table("AppPermissions")]
public class Permission
{
    [Key]
    public int PermissionId { get; set; }

    /// <summary>Hierarchical code: "Order.Place", "Wallet.Manage", "Reports.View".</summary>
    [Required, StringLength(64)]
    public string PermissionCode { get; set; } = string.Empty;

    [StringLength(200)] public string? DisplayName { get; set; }
    [StringLength(500)] public string? Description { get; set; }
}

[Table("AppUserRoles")]
public class UserRole
{
    [Key]
    public int UserRoleId { get; set; }
    public int UserId { get; set; }
    public int RoleId { get; set; }

    [ForeignKey(nameof(UserId))] public User? User { get; set; }
    [ForeignKey(nameof(RoleId))] public Role? Role { get; set; }

    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    [StringLength(100)] public string? AssignedBy { get; set; }
}

[Table("AppRolePermissions")]
public class RolePermission
{
    [Key]
    public int RolePermissionId { get; set; }
    public int RoleId { get; set; }
    public int PermissionId { get; set; }

    [ForeignKey(nameof(RoleId))]       public Role? Role { get; set; }
    [ForeignKey(nameof(PermissionId))] public Permission? Permission { get; set; }
}

// =============================================================================
// Employee  (CanteenManagementSystem.Domain.Users)
// -----------------------------------------------------------------------------
// Local, canteen-owned copy of one staff member. Mirror of <see cref="Student"/>
// — same sync pipeline, different source view / API endpoint.
//
// BUSINESS KEY
//   (ClientId, ExternalId) is unique. ExternalId is the source-system staff id.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace CanteenManagementSystem.Domain.Users;

[Table("Employees")]
public class Employee : IAggregateRoot, ITenantOwned
{
    [Key]
    public Guid EmployeeId { get; set; } = Guid.NewGuid();

    [Required, StringLength(50)]
    public string ExternalId { get; set; } = string.Empty;

    [StringLength(50)]
    public string? CardIdentifier { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Gender { get; set; }

    [StringLength(30)]
    public string? ContactNo { get; set; }

    [StringLength(500)]
    public string? PhotoPath { get; set; }

    [StringLength(100)]
    public string? Designation { get; set; }

    /// <summary>"TEACHER" / "STAFF" / "ADMIN" — drives wallet limit calc.</summary>
    [StringLength(50)]
    public string? EmployeeType { get; set; }

    /// <summary>Comma-separated allergen keywords matching the FoodItem.Allergens
    /// vocabulary. Counter scan flags conflicts.</summary>
    [StringLength(200)]
    public string? Allergies { get; set; }

    /// <summary>Free-text dietary note. Displayed on the counter scan screen.</summary>
    [StringLength(500)]
    public string? DietaryNotes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;

    [Required, StringLength(64)]
    public string SourceHash { get; set; } = string.Empty;

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

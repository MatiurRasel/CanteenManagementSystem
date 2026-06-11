// =============================================================================
// Student  (CanteenManagementSystem.Domain.Users)
// -----------------------------------------------------------------------------
// Local, canteen-owned copy of one student. Populated by DirectorySyncService
// from the tenant's configured source (school DB view, school portal API, or
// uploaded CSV). The counter scan hot path reads from here — never crosses to
// the source DB / API at request time.
//
// BUSINESS KEY
//   (ClientId, ExternalId) is unique. ExternalId is whatever the source uses
//   to identify a student (typically the SchoolID like "STD-CCPC-2026-0123").
//
// CHANGE DETECTION
//   SourceHash is SHA-256 of the normalised payload. The sync writes a row
//   only when hash differs, so reports / audit see meaningful UPDATEs.
//
// REPLACING StudentInfo (the view-backed model)
//   StudentInfo maps to vw_StudentInfo_Canteen in the school DB. While we
//   bridge, both coexist. Once every tenant has run a sync at least once,
//   StudentInfo + the view can be deleted.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace CanteenManagementSystem.Domain.Users;

[Table("Students")]
public class Student : IAggregateRoot, ITenantOwned
{
    [Key]
    public Guid StudentId { get; set; } = Guid.NewGuid();

    /// <summary>The source-system identifier (school's StudentID, API id, CSV row key).</summary>
    [Required, StringLength(50)]
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>Card-printed identifier (optional). When present, scans match against this.</summary>
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
    public string? Program { get; set; }

    [StringLength(50)]
    public string? Class { get; set; }

    [StringLength(50)]
    public string? Section { get; set; }

    [StringLength(50)]
    public string? Session { get; set; }

    [StringLength(100)]
    public string? Version { get; set; }

    /// <summary>Comma-separated allergen keywords matching the FoodItem.Allergens
    /// vocabulary ("nuts, dairy, gluten, egg, soy"). The counter scan warns the
    /// operator when any ordered item's allergens intersect this list.</summary>
    [StringLength(200)]
    public string? Allergies { get; set; }

    /// <summary>Free-text dietary note (e.g. "vegetarian only", "no spicy food").
    /// Displayed alongside the allergy chip on the counter scan screen.</summary>
    [StringLength(500)]
    public string? DietaryNotes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>SHA-256 hex of the normalised source payload. Used to skip no-op updates.</summary>
    [Required, StringLength(64)]
    public string SourceHash { get; set; } = string.Empty;

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

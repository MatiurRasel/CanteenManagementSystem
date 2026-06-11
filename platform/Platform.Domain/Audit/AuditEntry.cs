using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Audit;

/// Immutable trail for every sensitive action. Mandated by §13.1 of the source
/// of truth — every financial or administrative action must be attributable.
[Table("CanteenAuditEntries")]
public class AuditEntry : ITenantOwned
{
    [Key]
    public long AuditID { get; set; }

    [Required, StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [StringLength(100)]
    public string? EntityType { get; set; }

    [StringLength(50)]
    public string? EntityId { get; set; }

    [StringLength(100)]
    public string? PerformedBy { get; set; }

    [StringLength(50)]
    public string? PerformedByRole { get; set; }

    [StringLength(4000)]
    public string? PayloadJson { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(64)]
    public string? CorrelationId { get; set; }

    [Required]
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

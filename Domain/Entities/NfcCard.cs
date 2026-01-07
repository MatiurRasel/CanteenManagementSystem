using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// NFC card management
    /// </summary>
    [Table("NfcCards")]
    public class NfcCard
    {
        [Key]
        public Guid CardId { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100)]
        public string CardNumber { get; set; } = string.Empty;

        [Required]
        public Guid ClientId { get; set; }

        public Guid? UserId { get; set; } // Nullable for unassigned cards

        // Status
        [Required]
        [StringLength(20)]
        public string CardStatus { get; set; } = "UNASSIGNED"; // ACTIVE, BLOCKED, LOST, EXPIRED, UNASSIGNED

        // Type
        public bool IsBackupCard { get; set; } = false;

        // Dates
        public DateTime? AssignedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }

        // Security
        public int FailedAttempts { get; set; } = 0;
        public DateTime? LockedUntil { get; set; }

        // Audit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ClientId")]
        public virtual Client Client { get; set; } = null!;
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}


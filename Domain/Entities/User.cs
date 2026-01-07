using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// User entity with role-based access (maps to CanteenUsers)
    /// </summary>
    [Table("CanteenUsers")]
    public class User
    {
        [Key]
        public Guid UserId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ClientId { get; set; }

        [Required]
        [StringLength(255)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Email { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? Username { get; set; }

        public string? PasswordHash { get; set; }

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "STUDENT"; // SUPER_ADMIN, CLIENT_ADMIN, OPERATOR, TEACHER, STAFF, STUDENT, EMPLOYEE, PARENT, CUSTOMER

        // Educational Specific
        [StringLength(50)]
        public string? RollNumber { get; set; }
        [StringLength(50)]
        public string? Class { get; set; }
        [StringLength(10)]
        public string? Section { get; set; }
        public Guid? ParentUserId { get; set; }

        // Corporate Specific
        [StringLength(50)]
        public string? EmployeeId { get; set; }
        [StringLength(100)]
        public string? Department { get; set; }
        [StringLength(100)]
        public string? Designation { get; set; }

        // Profile
        public string? ProfilePhotoUrl { get; set; }
        public DateTime? DateOfBirth { get; set; }
        [StringLength(10)]
        public string? Gender { get; set; }

        // Settings (JSON)
        public string DietaryPreferencesJson { get; set; } = "{}";
        public string NotificationPreferencesJson { get; set; } = "{}";
        [Column(TypeName = "decimal(10,2)")]
        public decimal? DailySpendingLimit { get; set; }

        // Status
        [StringLength(20)]
        public string AccountStatus { get; set; } = "ACTIVE"; // ACTIVE, SUSPENDED, PENDING, BLOCKED
        public bool EmailVerified { get; set; } = false;
        public bool PhoneVerified { get; set; } = false;

        // Tracking
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ClientId")]
        public virtual Client Client { get; set; } = null!;
        public virtual UserBalance? UserBalance { get; set; }
        public virtual ICollection<NfcCard> NfcCards { get; set; } = new List<NfcCard>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}


using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// Multi-tenant client configuration (maps to CanteenClients table)
    /// </summary>
    [Table("CanteenClients")]
    public class Client
    {
        [Key]
        public Guid ClientId { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(255)]
        public string ClientName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string ClientCode { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string ClientShortName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string ClientType { get; set; } = "EDUCATIONAL"; // EDUCATIONAL, CORPORATE, RESTAURANT

        [Required]
        [StringLength(100)]
        public string Subdomain { get; set; } = string.Empty;

        // Branding
        public string? LogoUrl { get; set; }

        [StringLength(7)]
        public string PrimaryColor { get; set; } = "#FF5722";

        [StringLength(7)]
        public string SecondaryColor { get; set; } = "#FFC107";

        [StringLength(100)]
        public string AppName { get; set; } = "E-Canteen";

        // External API configuration (for student/employee master)
        /// <summary>
        /// Base API endpoint, e.g. http://localhost:36524
        /// </summary>
        [StringLength(255)]
        public string? ApiBaseUrl { get; set; }

        /// <summary>
        /// Relative method path, e.g. /getStudentOrEmployee
        /// </summary>
        [StringLength(255)]
        public string? ApiMethodPath { get; set; }

        /// <summary>
        /// Authentication method: app-secretkey, userIdPasswordWithToken, etc.
        /// </summary>
        [StringLength(50)]
        public string? ApiAuthMethod { get; set; }

        /// <summary>
        /// Header name for app key, e.g. X-App-Key
        /// </summary>
        [StringLength(100)]
        public string? ApiKeyHeaderName { get; set; }

        /// <summary>
        /// Header value for app key
        /// </summary>
        [StringLength(255)]
        public string? ApiKeyHeaderValue { get; set; }

        /// <summary>
        /// Header name for client identifier, e.g. X-App-Client
        /// </summary>
        [StringLength(100)]
        public string? ApiClientHeaderName { get; set; }

        /// <summary>
        /// Header value for client identifier
        /// </summary>
        [StringLength(255)]
        public string? ApiClientHeaderValue { get; set; }

        /// <summary>
        /// Additional headers (JSON: { \"Header-Name\": \"value\" }) or token/userId/password payload
        /// </summary>
        public string ApiExtraHeadersJson { get; set; } = "{}";

        /// <summary>
        /// Auth payload or credential config (JSON), used for userId/password or token flows
        /// </summary>
        public string ApiAuthConfigJson { get; set; } = "{}";

        // Operational Configuration (JSON stored as string, can be deserialized)
        public string OperationalConfigJson { get; set; } = "{}";
        public string OperatingHoursJson { get; set; } = "{}";
        public string BillingConfigJson { get; set; } = "{}";
        public string NotificationConfigJson { get; set; } = "{}";
        public string PaymentGatewayConfigJson { get; set; } = "{}";

        // Status
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<User> Users { get; set; } = new List<User>();
        public virtual ICollection<MenuCategory> MenuCategories { get; set; } = new List<MenuCategory>();
        public virtual ICollection<NfcCard> NfcCards { get; set; } = new List<NfcCard>();
    }
}


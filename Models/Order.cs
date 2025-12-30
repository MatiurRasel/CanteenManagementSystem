using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CanteenManagementSystem.Models
{
    [Table("CanteenOrders")]
    public class Order
    {
        [Key]
        public int OrderID { get; set; }

        [StringLength(50)]
        public string OrderNumber { get; set; } = string.Empty;
        [Required]
        [StringLength(15)]
        public string UserId { get; set; }

        [Required]
        public CanteenUserType UserType { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        public CanteenOrderStatus Status { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime? DeliveredDate { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        [ForeignKey("UserId")]
        public UserBalance UserBalance { get; set; }
    }
}

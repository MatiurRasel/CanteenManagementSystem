using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CanteenManagementSystem.Models
{
    [Table("CanteenOrderItems")]
    public class OrderItem
    {
        [Key]
        public int OrderItemID { get; set; }

        public int OrderID { get; set; }
        [ForeignKey("OrderID")]
        public Order Order { get; set; } = null!;

        public int FoodItemID { get; set; }
        [ForeignKey("FoodItemID")]
        public FoodItem FoodItem { get; set; } = null!;

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalPrice { get; set; }
    }
}

using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CanteenManagementSystem.Models
{
    [Table("CanteenFoodItems")]
    public class FoodItem
    {
        [Key]
        public int FoodItemID { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        public CanteenMealType? Category { get; set; } 

        [StringLength(500)]
        public string ImageUrl { get; set; }

        public bool IsAvailable { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}

using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CanteenManagementSystem.Models
{
    [Table("CanteenDailyMenus")]
    public class DailyMenu
    {
        [Key]
        public int DailyMenuID { get; set; }

        public int FoodItemID { get; set; }
        [ForeignKey("FoodItemID")]
        public FoodItem FoodItem { get; set; } = null!;

        [Required]
        public DateTime MenuDate { get; set; }

        public CanteenMealType? MealType { get; set; }

        public bool IsAvailable { get; set; } = true;
        // NEW: Quantity Management
        [Required]
        public int AvailableQuantity { get; set; } = 0;

        [Required]
        public int InitialQuantity { get; set; } = 0;
        public int DisplayOrder { get; set; } = 0;
        [NotMapped]
        public int OrderedQuantity => InitialQuantity - AvailableQuantity;

        [NotMapped]
        public bool InStock => AvailableQuantity > 0;
    }
}

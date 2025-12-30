using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CanteenManagementSystem.Models
{
    [Table("CanteenWeeklyMenuTemplates")]
    public class WeeklyMenuTemplate
    {
        [Key]
        public int TemplateID { get; set; }

        [Required]
        [StringLength(200)]
        public string TemplateName { get; set; } = string.Empty;

        [Required]
        public int DayOfWeek { get; set; } // 0=Sunday, 1=Monday, etc.

        public int FoodItemID { get; set; }
        [ForeignKey("FoodItemID")]
        public FoodItem FoodItem { get; set; } = null!;

        public CanteenMealType? MealType { get; set; }

        [Required]
        public int DefaultQuantity { get; set; }

        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;
using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Domain.Menu;

[Table("CanteenWeeklyMenuTemplates")]
public class WeeklyMenuTemplate : ITenantOwned
{
    [Key]
    public int TemplateID { get; set; }

    [Required, StringLength(200)]
    public string TemplateName { get; set; } = string.Empty;

    [Required]
    public int DayOfWeek { get; set; }

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

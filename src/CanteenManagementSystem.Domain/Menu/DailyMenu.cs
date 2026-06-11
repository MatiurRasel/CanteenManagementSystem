using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;
using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Domain.Menu;

[Table("CanteenDailyMenus")]
public class DailyMenu : ITenantOwned
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

    [Required]
    public int AvailableQuantity { get; set; } = 0;

    [Required]
    public int InitialQuantity { get; set; } = 0;

    public int ReservedQuantity { get; set; } = 0;

    public int DisplayOrder { get; set; } = 0;

    public byte[]? RowVersion { get; set; }

    [NotMapped]
    public int OrderedQuantity => InitialQuantity - AvailableQuantity;

    [NotMapped]
    public bool InStock => AvailableQuantity > 0;
}

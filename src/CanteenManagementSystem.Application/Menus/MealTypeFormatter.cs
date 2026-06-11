using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Application.Menus;

/// Maps CanteenMealType to its Bangla display name. Centralised here so that
/// services, view-models, and Razor views all agree on the canonical label.
public static class MealTypeFormatter
{
    public static string ToBangla(CanteenMealType mealType) => mealType switch
    {
        CanteenMealType.Breakfast => "সকালের নাস্তা",
        CanteenMealType.Lunch => "লাঞ্চ",
        CanteenMealType.Snacks => "স্ন্যাক্স",
        CanteenMealType.Drinks => "ড্রিংকস",
        CanteenMealType.Dinner => "ডিনার",
        _ => "অন্যান্য"
    };

    public static string ToBangla(CanteenMealType? mealType)
        => mealType.HasValue ? ToBangla(mealType.Value) : "অন্যান্য";

    public static CanteenMealType Parse(string mealType)
    {
        if (Enum.TryParse<CanteenMealType>(mealType, ignoreCase: true, out var result))
        {
            return result;
        }

        return mealType switch
        {
            "সকালের নাস্তা" => CanteenMealType.Breakfast,
            "লাঞ্চ" => CanteenMealType.Lunch,
            "স্ন্যাক্স" => CanteenMealType.Snacks,
            "ড্রিংকস" => CanteenMealType.Drinks,
            "ডিনার" => CanteenMealType.Dinner,
            _ => CanteenMealType.Lunch
        };
    }
}

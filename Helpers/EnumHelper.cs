using CanteenManagementSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CanteenManagementSystem.Helpers
{
    public static class MealTypeHelper
    {
        public static string GetMealTypeName(CanteenMealType mealType)
        {
            return mealType switch
            {
                CanteenMealType.Breakfast => "সকালের নাস্তা",
                CanteenMealType.Lunch => "লাঞ্চ",
                CanteenMealType.Snacks => "স্ন্যাক্স",
                CanteenMealType.Drinks => "ড্রিংকস",
                CanteenMealType.Dinner => "ডিনার",
                _ => "অন্যান্য"
            };
        }

        // Add overload for nullable enum
        public static string GetMealTypeName(CanteenMealType? mealType)
        {
            if (!mealType.HasValue)
                return "অন্যান্য";

            return GetMealTypeName(mealType.Value);
        }

        public static List<SelectListItem> GetMealTypeOptions()
        {
            return new List<SelectListItem>
        {
            new SelectListItem { Value = "Breakfast", Text = "সকালের নাস্তা" },
            new SelectListItem { Value = "Lunch", Text = "লাঞ্চ" },
            new SelectListItem { Value = "Snacks", Text = "স্ন্যাক্স" },
            new SelectListItem { Value = "Drinks", Text = "ড্রিংকস" },
            new SelectListItem { Value = "Dinner", Text = "ডিনার" }
        };
        }
    }
}

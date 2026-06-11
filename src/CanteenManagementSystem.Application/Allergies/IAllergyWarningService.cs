// =============================================================================
// IAllergyWarningService  (CanteenManagementSystem.Application.Allergies)
// -----------------------------------------------------------------------------
// Cross-references a scanned user's <c>Allergies</c> list against the allergens
// declared on the items in a candidate order. Returns the set of conflicting
// allergens so the counter UI can flash a warning before the operator confirms.
//
// VOCABULARY
//   Both FoodItem.Allergens and Student/Employee.Allergies use a comma-separated
//   list of lower-case keywords ("nuts, dairy, gluten, egg, soy"). Match is
//   case-insensitive, whitespace-tolerant, and exact-keyword (substring "egg"
//   would NOT match "eggplant" — matches are on whole tokens only).
// =============================================================================

using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Application.Allergies;

public sealed record AllergyConflict(
    int FoodItemId,
    string ItemName,
    IReadOnlyList<string> Allergens);

public sealed record AllergyCheckResult(
    IReadOnlyList<string> UserAllergies,
    string? UserDietaryNotes,
    IReadOnlyList<AllergyConflict> Conflicts)
{
    public bool HasConflicts => Conflicts.Count > 0;
}

public interface IAllergyWarningService
{
    /// <summary>Check whether any of the food items in the candidate basket conflict with the scanned user's saved allergies.</summary>
    Task<AllergyCheckResult> CheckAsync(
        string userId,
        CanteenUserType userType,
        IReadOnlyList<int> foodItemIds,
        CancellationToken cancellationToken = default);
}

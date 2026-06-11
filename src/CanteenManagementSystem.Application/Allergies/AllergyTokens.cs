// =============================================================================
// AllergyTokens  (CanteenManagementSystem.Application.Allergies)
// -----------------------------------------------------------------------------
// Pure-logic helpers extracted from AllergyWarningService so the comma-CSV
// vocabulary can be unit-tested without standing up EF + repositories.
//
// The CSV format ("nuts, dairy, gluten") is shared between
//   * FoodItem.Allergens         (per-item allergen list)
//   * Student/Employee.Allergies (per-user allergy list)
// =============================================================================

namespace CanteenManagementSystem.Application.Allergies;

public static class AllergyTokens
{
    /// <summary>Parse a comma-separated allergen string into a distinct, lower-case set of tokens.</summary>
    public static IReadOnlyList<string> Split(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(t => t.ToLowerInvariant())
                 .Distinct()
                 .ToList();

    /// <summary>Return the intersection of two CSV allergen strings (case-insensitive, token-exact).</summary>
    public static IReadOnlyList<string> Intersect(string? userCsv, string? itemCsv)
    {
        var u = Split(userCsv);
        if (u.Count == 0) return Array.Empty<string>();
        var i = Split(itemCsv);
        if (i.Count == 0) return Array.Empty<string>();
        return u.Intersect(i, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

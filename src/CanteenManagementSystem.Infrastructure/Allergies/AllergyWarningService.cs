// =============================================================================
// AllergyWarningService  (CanteenManagementSystem.Infrastructure.Allergies)
// -----------------------------------------------------------------------------
// Implements <see cref="IAllergyWarningService"/>. ADR 0004: IReadOnlyRepository.
// =============================================================================

using CanteenManagementSystem.Application.Allergies;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Allergies;

internal sealed class AllergyWarningService : IAllergyWarningService
{
    private readonly IReadOnlyRepository<Student> _students;
    private readonly IReadOnlyRepository<Employee> _employees;
    private readonly IReadOnlyRepository<FoodItem> _foods;

    public AllergyWarningService(
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        IReadOnlyRepository<FoodItem> foods)
    {
        _students = students; _employees = employees; _foods = foods;
    }

    public async Task<AllergyCheckResult> CheckAsync(
        string userId,
        CanteenUserType userType,
        IReadOnlyList<int> foodItemIds,
        CancellationToken cancellationToken = default)
    {
        string? userAllergies, userNotes;
        if (userType == CanteenUserType.Student)
        {
            var s = await _students.NoTrackingQuery()
                .Where(s => s.ExternalId == userId)
                .Select(s => new { s.Allergies, s.DietaryNotes })
                .FirstOrDefaultAsync(cancellationToken);
            userAllergies = s?.Allergies;
            userNotes = s?.DietaryNotes;
        }
        else
        {
            var e = await _employees.NoTrackingQuery()
                .Where(e => e.ExternalId == userId)
                .Select(e => new { e.Allergies, e.DietaryNotes })
                .FirstOrDefaultAsync(cancellationToken);
            userAllergies = e?.Allergies;
            userNotes = e?.DietaryNotes;
        }

        var userTokens = SplitTokens(userAllergies);
        if (userTokens.Count == 0 || foodItemIds.Count == 0)
            return new AllergyCheckResult(userTokens, userNotes, Array.Empty<AllergyConflict>());

        var items = await _foods.NoTrackingQuery()
            .Where(f => foodItemIds.Contains(f.FoodItemID))
            .Select(f => new { f.FoodItemID, f.ItemName, f.Allergens })
            .ToListAsync(cancellationToken);

        var conflicts = new List<AllergyConflict>();
        foreach (var item in items)
        {
            var matches = AllergyTokens.Intersect(userAllergies, item.Allergens);
            if (matches.Count > 0)
            {
                conflicts.Add(new AllergyConflict(item.FoodItemID, item.ItemName, matches));
            }
        }

        return new AllergyCheckResult(userTokens, userNotes, conflicts);
    }

    private static IReadOnlyList<string> SplitTokens(string? csv) => AllergyTokens.Split(csv);
}

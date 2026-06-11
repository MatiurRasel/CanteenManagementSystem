using CanteenManagementSystem.Application.Menus;
using CanteenManagementSystem.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Menus;

internal sealed class ComboButtonService : IComboButtonService
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<FoodItem> _foods;

    public ComboButtonService(IUnitOfWork uow, IReadOnlyRepository<FoodItem> foods)
    {
        _uow = uow; _foods = foods;
    }

    private IRepository<ComboButton> Combos => _uow.Repository<ComboButton>();

    public async Task<IReadOnlyList<ComboButtonListItem>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var q = Combos.NoTrackingQuery();
        if (!includeInactive) q = q.Where(c => c.IsActive);
        var rows = await q.OrderBy(c => c.SortOrder).ThenBy(c => c.Code).ToListAsync(ct);
        return rows.Select(c => new ComboButtonListItem(
            c.ComboButtonId, c.Code, c.DisplayName, c.Color, c.SortOrder, c.IsActive,
            ParseIds(c.FoodItemIdsCsv).Count)).ToList();
    }

    public Task<ComboButton?> GetAsync(int id, CancellationToken ct = default)
        => Combos.FirstOrDefaultAsync(c => c.ComboButtonId == id, ct);

    public async Task<ComboButton> CreateAsync(ComboButtonInput input, CancellationToken ct = default)
    {
        var row = new ComboButton
        {
            Code = input.Code.Trim().ToUpperInvariant(),
            DisplayName = input.DisplayName.Trim(),
            Color = input.Color,
            FoodItemIdsCsv = string.Join(',', input.FoodItemIds.Distinct()),
            SortOrder = input.SortOrder,
            IsActive = input.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };
        await Combos.AddAsync(row, ct);
        await _uow.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> UpdateAsync(int id, ComboButtonInput input, CancellationToken ct = default)
    {
        var row = await Combos.FirstOrDefaultAsync(c => c.ComboButtonId == id, ct);
        if (row is null) return false;
        row.Code = input.Code.Trim().ToUpperInvariant();
        row.DisplayName = input.DisplayName.Trim();
        row.Color = input.Color;
        row.FoodItemIdsCsv = string.Join(',', input.FoodItemIds.Distinct());
        row.SortOrder = input.SortOrder;
        row.IsActive = input.IsActive;
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var row = await Combos.FirstOrDefaultAsync(c => c.ComboButtonId == id, ct);
        if (row is null) return false;
        Combos.Remove(row);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ResolvedCombo>> ResolveActiveAsync(CancellationToken ct = default)
    {
        var combos = await Combos.NoTrackingQuery()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
            .ToListAsync(ct);
        if (combos.Count == 0) return Array.Empty<ResolvedCombo>();

        var allIds = combos.SelectMany(c => ParseIds(c.FoodItemIdsCsv)).Distinct().ToList();
        var foodMap = await _foods.NoTrackingQuery()
            .Where(f => allIds.Contains(f.FoodItemID))
            .ToDictionaryAsync(f => f.FoodItemID, ct);

        return combos.Select(c =>
        {
            var ids = ParseIds(c.FoodItemIdsCsv);
            var items = ids
                .Where(foodMap.ContainsKey)
                .Select(id => foodMap[id])
                .Select(f => new ResolvedComboItem(f.FoodItemID, f.ItemName, f.Price))
                .ToList();
            return new ResolvedCombo(c.ComboButtonId, c.Code, c.DisplayName, items, items.Sum(i => i.Price));
        }).ToList();
    }

    private static List<int> ParseIds(string csv)
        => string.IsNullOrWhiteSpace(csv)
            ? new List<int>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(s => int.TryParse(s, out var n) ? n : 0)
                 .Where(n => n > 0)
                 .ToList();
}

// =============================================================================
// DiscountEngine  (Platform.Infrastructure.Pricing)
// -----------------------------------------------------------------------------
// Starter implementation. Reads active DiscountRule rows for the current
// tenant (EF global query filter scopes automatically), evaluates a small
// vocabulary of conditions, applies a small vocabulary of effects, picks
// the rule with the highest Priority that matches, and returns the quote.
//
// CONDITION VOCAB (this version)
//   {"type":"DayOfWeekRole","day":"Tuesday","roles":["TEACHER"]}
//   {"type":"ComboItems",   "foodItemIds":[12,15]}
//
// EFFECT VOCAB (this version)
//   {"type":"Percent","amount":10}      → 10% off subtotal
//   {"type":"Flat",   "amount":20}      → ৳20 off (clamped to subtotal)
//
// New rule types drop in as switch cases in TryMatch / Apply — keep the
// data shape the same and existing rows keep working.
//
// ADR 0004: IReadOnlyRepository<DiscountRule> — pure read.
// =============================================================================

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Pricing;
using Platform.Application.Persistence;
using Platform.Domain.Pricing;

namespace Platform.Infrastructure.Pricing;

public sealed class DiscountEngine : IDiscountEngine
{
    private readonly IReadOnlyRepository<DiscountRule> _rules;
    public DiscountEngine(IReadOnlyRepository<DiscountRule> rules) => _rules = rules;

    public async Task<DiscountQuote> QuoteAsync(DiscountContext ctx, CancellationToken ct = default)
    {
        if (ctx.Subtotal <= 0)
            return new DiscountQuote(0m, ctx.Subtotal, null, null);

        var rules = await _rules.NoTrackingQuery()
            .Where(r => r.IsActive
                     && (r.ValidFromUtc  == null || r.ValidFromUtc  <= ctx.OrderTimeUtc)
                     && (r.ValidUntilUtc == null || r.ValidUntilUtc >= ctx.OrderTimeUtc))
            .OrderByDescending(r => r.Priority)
            .ToListAsync(ct);

        foreach (var rule in rules)
        {
            if (!TryMatch(rule.ConditionJson, ctx)) continue;
            var (discount, applied) = Apply(rule.EffectJson, ctx.Subtotal);
            if (!applied || discount <= 0) continue;
            discount = Math.Min(discount, ctx.Subtotal);
            return new DiscountQuote(discount, ctx.Subtotal - discount, rule.RuleCode, rule.DisplayName);
        }
        return new DiscountQuote(0m, ctx.Subtotal, null, null);
    }

    private static bool TryMatch(string conditionJson, DiscountContext ctx)
    {
        try
        {
            using var doc = JsonDocument.Parse(conditionJson);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            return type switch
            {
                "DayOfWeekRole" => MatchDayOfWeekRole(root, ctx),
                "ComboItems"    => MatchComboItems(root, ctx),
                _ => false
            };
        }
        catch { return false; }
    }

    private static bool MatchDayOfWeekRole(JsonElement root, DiscountContext ctx)
    {
        var dayName = root.TryGetProperty("day", out var d) ? d.GetString() : null;
        if (string.IsNullOrWhiteSpace(dayName) ||
            !Enum.TryParse<DayOfWeek>(dayName, ignoreCase: true, out var dayOfWeek) ||
            ctx.OrderTimeUtc.DayOfWeek != dayOfWeek) return false;

        if (root.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
        {
            if (string.IsNullOrWhiteSpace(ctx.UserKind)) return false;
            foreach (var r in roles.EnumerateArray())
            {
                var s = r.GetString();
                if (!string.IsNullOrWhiteSpace(s) && string.Equals(s, ctx.UserKind, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        return true;     // day-only match
    }

    private static bool MatchComboItems(JsonElement root, DiscountContext ctx)
    {
        if (!root.TryGetProperty("foodItemIds", out var arr) || arr.ValueKind != JsonValueKind.Array) return false;
        var required = arr.EnumerateArray().Select(e => e.GetInt32()).ToHashSet();
        return required.All(id => ctx.FoodItemIds.Contains(id));
    }

    private static (decimal discount, bool applied) Apply(string effectJson, decimal subtotal)
    {
        try
        {
            using var doc = JsonDocument.Parse(effectJson);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            var amount = root.TryGetProperty("amount", out var a) ? a.GetDecimal() : 0m;
            return type switch
            {
                "Percent" => (subtotal * amount / 100m, true),
                "Flat"    => (amount, true),
                _         => (0m, false)
            };
        }
        catch { return (0m, false); }
    }
}

// =============================================================================
// IDiscountEngine + IDiscountRule helpers  (Platform.Application.Abstractions.Pricing)
// -----------------------------------------------------------------------------
// Strategy seam for evaluating discount rules at order placement. The engine
// resolves all active `DiscountRule` rows for the current tenant, runs the
// JSON-defined conditions, picks the highest-priority match, applies the
// effect to a `DiscountQuote`, and returns it. PlaceOrderCommandHandler then
// subtracts `Quote.Discount` from `Order.TotalAmount` and writes an audit row.
//
// SHIPPING WHAT
//   This sprint provides the seam + a simple in-process default that
//   understands two rule types: "DayOfWeekRole" + "Percent". Bigger rule
//   languages (ComboItems, FixedTotal, TimeOfDay) drop in as additional
//   condition / effect evaluators registered in DI.
// =============================================================================

namespace Platform.Application.Abstractions.Pricing;

public interface IDiscountEngine
{
    /// <summary>
    /// Evaluate all active rules against the supplied context and return the
    /// best-matching quote. <c>Discount=0</c> when no rule matches.
    /// </summary>
    Task<DiscountQuote> QuoteAsync(DiscountContext context, CancellationToken cancellationToken = default);
}

/// <summary>Inputs the engine compares rules against.</summary>
public sealed record DiscountContext(
    DateTime OrderTimeUtc,
    string UserExternalId,
    string? UserKind,             // e.g. "TEACHER", "STAFF", "STUDENT" (uppercase)
    decimal Subtotal,
    IReadOnlyList<int> FoodItemIds);

/// <summary>What the engine returns. AppliedRuleCode is empty when no rule fires.</summary>
public sealed record DiscountQuote(
    decimal Discount,
    decimal NetTotal,
    string? AppliedRuleCode,
    string? AppliedRuleName);

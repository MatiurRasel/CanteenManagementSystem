// =============================================================================
// IBillingService  (Platform.Application.Abstractions.Billing)
// -----------------------------------------------------------------------------
// Per-tenant SaaS-subscription billing. Wrapped behind an interface so the
// concrete impl can be swapped between Stripe Billing, Lemon Squeezy, or a
// noop default. The stub impl returns plausible read-only data so the admin
// UI works end-to-end before the tenant onboards with a real billing
// provider — flip the DI registration to the real impl when credentials land.
//
// SETTINGS  (tenant)
//   Billing.Provider              "Stripe" | "None" (default)
//   Billing.Stripe.ApiKey         (secret)  test/sk_live key
//   Billing.Stripe.PriceId        (plan)    price_xxx
//   Billing.Stripe.CustomerId     (linked customer id, set on first checkout)
//   Billing.Stripe.SubscriptionId (linked subscription id, set on checkout completion)
//
// FLOWS
//   GetStatusAsync()    — pulls current plan + period end + last invoice
//   StartCheckoutAsync()— returns a hosted-checkout URL the admin can redirect to
//   CancelAsync()       — marks the subscription as ending on the period boundary
// =============================================================================

namespace Platform.Application.Abstractions.Billing;

public enum BillingProvider { None = 0, Stripe = 1 }

public enum SubscriptionState { Inactive = 0, Trialing = 1, Active = 2, PastDue = 3, Cancelled = 4 }

public sealed record BillingStatus(
    BillingProvider Provider,
    SubscriptionState State,
    string? PlanCode,
    string? PlanName,
    decimal? AmountPerCycle,
    string? Currency,
    DateTime? CurrentPeriodEndUtc,
    string? LastInvoiceUrl);

public sealed record CheckoutStartResult(bool Success, string? RedirectUrl, string? Message);

public interface IBillingService
{
    Task<BillingStatus> GetStatusAsync(CancellationToken ct = default);
    Task<CheckoutStartResult> StartCheckoutAsync(string priceCode, string? successUrl, string? cancelUrl, CancellationToken ct = default);
    Task<bool> CancelAsync(CancellationToken ct = default);
}

// =============================================================================
// SandboxBillingService  (Platform.Infrastructure.Billing)
// -----------------------------------------------------------------------------
// Test-data IBillingService impl. Acts indistinguishably from a real billing
// provider from the UI's perspective — plans, periods, invoices, and state
// transitions all simulated end-to-end so an admin can demo billing without
// onboarding Stripe.
//
// SWITCH TO LIVE
//   Flip the DI line in CanteenInfrastructure.DependencyInjection:
//     services.AddScoped<IBillingService, StripeBillingService>();
//   …and set Billing.Provider = "Stripe" + tenant credentials. No other
//   change required: every controller / view talks to IBillingService.
//
// SIMULATED FEATURES
//   * Three plans: Starter / Pro / Enterprise — distinct prices.
//   * Subscription state persists per-tenant via TenantSettings.
//   * Synthetic invoice URLs ("/admin/billing/sandbox/invoice/{id}").
//   * Cancel sets state=Cancelled + records CurrentPeriodEnd 14d out so the
//     UI can still show "ends on X".
//   * State-transition test hooks at /admin/billing/sandbox/*.
// =============================================================================

using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Billing;
using Platform.Application.Abstractions.Configuration;

namespace Platform.Infrastructure.Billing;

public sealed class SandboxBillingService : IBillingService
{
    public static readonly IReadOnlyDictionary<string, (string Name, decimal Amount, string Currency)> Plans =
        new Dictionary<string, (string, decimal, string)>
        {
            ["starter"]    = ("Starter",    29m,  "USD"),
            ["pro"]        = ("Pro",        99m,  "USD"),
            ["enterprise"] = ("Enterprise", 299m, "USD")
        };

    private readonly ITenantSettings _settings;
    private readonly ILogger<SandboxBillingService> _logger;

    public SandboxBillingService(ITenantSettings settings, ILogger<SandboxBillingService> logger)
    {
        _settings = settings; _logger = logger;
    }

    public async Task<BillingStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var planCode = await _settings.GetAsync("Billing.Sandbox.PlanCode", "starter", ct) ?? "starter";
        var stateRaw = await _settings.GetAsync("Billing.Sandbox.State",    "Inactive", ct) ?? "Inactive";
        var endIso   = await _settings.GetAsync("Billing.Sandbox.PeriodEndUtc", defaultValue: null, ct);

        var state = Enum.TryParse<SubscriptionState>(stateRaw, true, out var s) ? s : SubscriptionState.Inactive;
        DateTime? endUtc = DateTime.TryParse(endIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;

        if (!Plans.TryGetValue(planCode, out var plan))
        {
            plan = Plans["starter"];
        }

        var invoiceId = await _settings.GetAsync("Billing.Sandbox.LastInvoiceId", defaultValue: null, ct);
        var invoiceUrl = string.IsNullOrEmpty(invoiceId)
            ? null
            : $"/admin/billing/sandbox/invoice/{invoiceId}";

        return new BillingStatus(
            Provider:            BillingProvider.None,    // honestly: this isn't Stripe
            State:               state,
            PlanCode:            planCode,
            PlanName:            plan.Name + " (sandbox)",
            AmountPerCycle:      plan.Amount,
            Currency:            plan.Currency,
            CurrentPeriodEndUtc: endUtc,
            LastInvoiceUrl:      invoiceUrl);
    }

    public async Task<CheckoutStartResult> StartCheckoutAsync(string priceCode, string? successUrl, string? cancelUrl, CancellationToken ct = default)
    {
        if (!Plans.ContainsKey(priceCode))
        {
            return new CheckoutStartResult(false, null, $"Unknown sandbox plan '{priceCode}'. Pick one of: {string.Join(", ", Plans.Keys)}.");
        }
        // Stash the plan choice + flip to Active immediately (real checkout would await provider callback).
        await _settings.SetAsync("Billing.Sandbox.PlanCode", priceCode, cancellationToken: ct);
        await _settings.SetAsync("Billing.Sandbox.State",    SubscriptionState.Active.ToString(), cancellationToken: ct);
        await _settings.SetAsync("Billing.Sandbox.PeriodEndUtc", DateTime.UtcNow.AddDays(30).ToString("O"), cancellationToken: ct);
        var invoiceId = $"sbx_{DateTime.UtcNow:yyyyMMddHHmmss}";
        await _settings.SetAsync("Billing.Sandbox.LastInvoiceId", invoiceId, cancellationToken: ct);

        _logger.LogInformation("Sandbox billing checkout: plan={Plan} → Active for 30d (invoice {Invoice}).", priceCode, invoiceId);
        // Mirror Stripe's redirect-to-hosted-checkout, but the URL just bounces back.
        var url = successUrl ?? "/admin/billing?checkout=success";
        return new CheckoutStartResult(true, url, $"Sandbox checkout complete — plan '{priceCode}' active.");
    }

    public async Task<bool> CancelAsync(CancellationToken ct = default)
    {
        var endIso = await _settings.GetAsync("Billing.Sandbox.PeriodEndUtc", defaultValue: null, ct);
        var endUtc = DateTime.TryParse(endIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt
            : DateTime.UtcNow.AddDays(14);

        await _settings.SetAsync("Billing.Sandbox.State", SubscriptionState.Cancelled.ToString(), cancellationToken: ct);
        await _settings.SetAsync("Billing.Sandbox.PeriodEndUtc", endUtc.ToString("O"), cancellationToken: ct);
        _logger.LogInformation("Sandbox billing cancelled — runs until {End:u}.", endUtc);
        return true;
    }

    /// <summary>
    /// Test-only state hooks — controllers call these from /admin/billing/sandbox/* to drive the demo.
    /// </summary>
    public Task SimulateAsync(SubscriptionState state, CancellationToken ct = default)
        => _settings.SetAsync("Billing.Sandbox.State", state.ToString(), cancellationToken: ct);
}

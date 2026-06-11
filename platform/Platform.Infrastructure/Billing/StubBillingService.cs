// =============================================================================
// StubBillingService  (Platform.Infrastructure.Billing)
// -----------------------------------------------------------------------------
// Default IBillingService impl until a tenant onboards with a real provider.
// Reads "as configured" data from TenantSetting so the admin UI shows something
// realistic on day one — but never calls an external API. Flip the DI
// registration to StripeBillingService (TBD) once credentials are in place.
//
// READS (tenant settings)
//   Billing.Provider               "Stripe" | "None"
//   Billing.PlanCode               human plan slug
//   Billing.PlanName               display name
//   Billing.AmountPerCycle         decimal (e.g. "29.00")
//   Billing.Currency               "USD" | "BDT" | …
//   Billing.CurrentPeriodEndUtc    ISO-8601 timestamp
//   Billing.LastInvoiceUrl         hosted-invoice link
//   Billing.State                  "Inactive" | "Trialing" | "Active" | "PastDue" | "Cancelled"
//
// Returning all settings as inert reads keeps the admin UI testable without
// reaching out to Stripe in dev / CI.
// =============================================================================

using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Billing;
using Platform.Application.Abstractions.Configuration;

namespace Platform.Infrastructure.Billing;

public sealed class StubBillingService : IBillingService
{
    private readonly ITenantSettings _settings;
    private readonly ILogger<StubBillingService> _logger;

    public StubBillingService(ITenantSettings settings, ILogger<StubBillingService> logger)
    {
        _settings = settings; _logger = logger;
    }

    public async Task<BillingStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var provider = await _settings.GetAsync("Billing.Provider", "None", ct);
        var planCode = await _settings.GetAsync("Billing.PlanCode", defaultValue: null, ct);
        var planName = await _settings.GetAsync("Billing.PlanName", defaultValue: null, ct);
        var amount   = await _settings.GetDecimalAsync("Billing.AmountPerCycle", 0m, ct);
        var currency = await _settings.GetAsync("Billing.Currency", "USD", ct);
        var endIso   = await _settings.GetAsync("Billing.CurrentPeriodEndUtc", defaultValue: null, ct);
        var invoice  = await _settings.GetAsync("Billing.LastInvoiceUrl", defaultValue: null, ct);
        var stateRaw = await _settings.GetAsync("Billing.State", "Inactive", ct);

        var providerEnum = Enum.TryParse<BillingProvider>(provider, true, out var p) ? p : BillingProvider.None;
        var stateEnum    = Enum.TryParse<SubscriptionState>(stateRaw, true, out var s) ? s : SubscriptionState.Inactive;
        DateTime? endUtc = DateTime.TryParse(endIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;

        return new BillingStatus(providerEnum, stateEnum, planCode, planName,
            amount > 0 ? amount : null, currency, endUtc, invoice);
    }

    public Task<CheckoutStartResult> StartCheckoutAsync(string priceCode, string? successUrl, string? cancelUrl, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "StubBillingService.StartCheckoutAsync called — no real provider wired. Configure Billing.Provider=Stripe + Billing.Stripe.ApiKey + Billing.Stripe.PriceId to enable.");
        return Task.FromResult(new CheckoutStartResult(false, null,
            "Billing is in stub mode. Ask your platform admin to configure Stripe credentials."));
    }

    public Task<bool> CancelAsync(CancellationToken ct = default)
    {
        _logger.LogWarning("StubBillingService.CancelAsync called — noop in stub mode.");
        return Task.FromResult(false);
    }
}

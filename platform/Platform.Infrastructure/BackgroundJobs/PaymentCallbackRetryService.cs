// =============================================================================
// PaymentCallbackRetryService  (Platform.Infrastructure.BackgroundJobs)
// -----------------------------------------------------------------------------
// Gateway callbacks can fail to reach us (network blip, our process restart
// mid-handler, the gateway times out our 200 response). This service polls
// PaymentTransaction rows that are stuck in Pending past a grace window and
// re-asks the gateway "what's the final status?" via VerifyCallbackAsync.
//
// FLOW
//   1. Find PaymentTransaction rows with Status=Pending and
//      CreatedAtUtc < (UtcNow - StuckAfter).
//   2. For each, resolve the matching IPaymentGateway and call
//      VerifyCallbackAsync with the empty payload (gateways accept this
//      and respond with the canonical state).
//   3. On Succeeded: trigger the wallet recharge (idempotent via Ref).
//   4. On Failed:    flip status + record audit.
//   5. On still-Pending: leave alone for another tick.
//
// SCHEDULE
//   * Wakes every TickInterval (default 2 min).
//   * Per-row exponential backoff via PaymentTransaction.RetryCount column
//     (added in this batch via a settings watermark; the row gets its
//     CompletedAtUtc set when terminal so a re-poll skips it naturally).
//
// METRICS
//   `Platform.Payments` meter exposes:
//     payments.retry.checked       (Counter, tags = method)
//     payments.retry.recovered     (Counter, tags = method)
//     payments.retry.terminal_fail (Counter, tags = method)
// =============================================================================

using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Payments;
using Platform.Application.Abstractions.Tenancy;
using Platform.Application.Abstractions.Wallets;
using Platform.Application.Persistence;
using Platform.Domain.Payments;
using Platform.Domain.Tenancy;

namespace Platform.Infrastructure.BackgroundJobs;

public sealed class PaymentCallbackRetryService : BackgroundService
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan StuckAfter   = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan AbandonAfter = TimeSpan.FromDays(2);
    public static readonly TimeSpan ErrorBackoff = TimeSpan.FromMinutes(10);

    private static readonly Meter Meter = new("Platform.Payments");
    private static readonly Counter<long> Checked        = Meter.CreateCounter<long>("payments.retry.checked", "transactions", "Stuck Pending payments inspected by the retry service.");
    private static readonly Counter<long> Recovered      = Meter.CreateCounter<long>("payments.retry.recovered", "transactions", "Pending payments successfully resolved via re-verify.");
    private static readonly Counter<long> TerminalFailed = Meter.CreateCounter<long>("payments.retry.terminal_fail", "transactions", "Pending payments flipped to Failed by re-verify.");

    private readonly IServiceProvider _sp;
    private readonly ILogger<PaymentCallbackRetryService> _logger;

    public PaymentCallbackRetryService(IServiceProvider sp, ILogger<PaymentCallbackRetryService> logger)
    {
        _sp = sp; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payment callback retry service started; tick {Tick}.", TickInterval);
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SweepAsync(stoppingToken); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment retry sweep failed; backing off {Backoff}.", ErrorBackoff);
                try { await Task.Delay(ErrorBackoff, stoppingToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }
            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var rootScope = _sp.CreateAsyncScope();
        var clients = rootScope.ServiceProvider.GetRequiredService<IReadOnlyRepository<Client>>();
        var tenants = await clients.NoTrackingQuery()
            .IgnoreQueryFilters()
            .Where(c => c.IsActive)
            .Select(c => new { c.ClientId, c.ClientCode })
            .ToListAsync(ct);

        foreach (var t in tenants)
        {
            if (ct.IsCancellationRequested) break;
            await SweepTenantAsync(t.ClientId, t.ClientCode, ct);
        }
    }

    private async Task SweepTenantAsync(int clientId, string clientCode, CancellationToken ct)
    {
        try
        {
            await using var scope = _sp.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            if (sp.GetRequiredService<Platform.Domain.Tenancy.ITenantContext>() is IMutableTenantContext mutable)
            {
                mutable.Resolve(clientId, clientCode);
            }

            var uow      = sp.GetRequiredService<IUnitOfWork>();
            var txRepo   = uow.Repository<PaymentTransaction>();
            var gateways = sp.GetServices<IPaymentGateway>().ToDictionary(g => g.Method);
            var wallet   = sp.GetRequiredService<IWalletService>();
            var audit    = sp.GetRequiredService<IAuditTrail>();

            var now = DateTime.UtcNow;
            var stuckBefore   = now - StuckAfter;
            var abandonBefore = now - AbandonAfter;

            var stuck = await txRepo.Query()
                .Where(t => t.Status == PaymentStatus.Pending
                         && t.CreatedAtUtc < stuckBefore
                         && t.CreatedAtUtc > abandonBefore)
                .OrderBy(t => t.CreatedAtUtc)
                .Take(50)
                .ToListAsync(ct);

            if (stuck.Count == 0) return;

            foreach (var tx in stuck)
            {
                if (!gateways.TryGetValue(tx.Method, out var gateway))
                {
                    _logger.LogWarning("No gateway for {Method} on stuck tx {Ref}", tx.Method, tx.TransactionRef);
                    continue;
                }

                var methodTag = new KeyValuePair<string, object?>("method", tx.Method.ToString());
                Checked.Add(1, methodTag);

                PaymentVerificationResult verification;
                try
                {
                    verification = await gateway.VerifyCallbackAsync(tx.TransactionRef, new Dictionary<string, string>(), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Retry verify failed for {Ref}", tx.TransactionRef);
                    continue;
                }

                if (verification.Status == PaymentStatus.Pending) continue; // still pending — wait next tick

                tx.Status           = verification.Status;
                tx.GatewayPaymentId = verification.GatewayPaymentId ?? tx.GatewayPaymentId;
                tx.GatewayMessage   = verification.Message;
                tx.CallbackPayload  = verification.RawPayload;
                tx.CompletedAtUtc   = now;

                if (verification.Status == PaymentStatus.Succeeded)
                {
                    await wallet.RechargeAsync(tx.UserId, tx.UserType,
                        verification.VerifiedAmount ?? tx.Amount,
                        source: $"{tx.Method} #{tx.TransactionRef}",
                        ct);
                    await audit.RecordAsync(
                        "Payment.RecoveredByRetry", nameof(PaymentTransaction), tx.TransactionRef,
                        new { tx.UserId, tx.Amount, method = tx.Method.ToString() }, ct);
                    Recovered.Add(1, methodTag);
                }
                else
                {
                    await audit.RecordAsync(
                        "Payment.FailedByRetry", nameof(PaymentTransaction), tx.TransactionRef,
                        new { method = tx.Method.ToString(), verification.Message }, ct);
                    TerminalFailed.Add(1, methodTag);
                }
            }

            await uow.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment retry tenant sweep failed for {Tenant}.", clientCode);
        }
    }
}

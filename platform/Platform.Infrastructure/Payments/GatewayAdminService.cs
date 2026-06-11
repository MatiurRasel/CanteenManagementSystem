// =============================================================================
// GatewayAdminService  (Platform.Infrastructure.Payments)
// -----------------------------------------------------------------------------
// IGatewayAdminService default impl. IUnitOfWork only — no IAppDbContext.
// Secret-aware Save: overwrites credential fields ONLY when the incoming
// value is non-null and not the masked placeholder ("***").
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Payments;
using Platform.Application.Persistence;
using Platform.Application.Results;
using Platform.Domain.Payments;

namespace Platform.Infrastructure.Payments;

public sealed class GatewayAdminService : IGatewayAdminService
{
    private readonly IUnitOfWork _uow;
    public GatewayAdminService(IUnitOfWork uow) => _uow = uow;

    private IRepository<PaymentGatewayConfig> Repo => _uow.Repository<PaymentGatewayConfig>();

    public async Task<IReadOnlyList<PaymentGatewayConfig>> ListAsync(CancellationToken ct = default)
        => await Repo.Query()
            .Include(g => g.Channels)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.GatewayCode)
            .ToListAsync(ct);

    public Task<PaymentGatewayConfig?> GetByIdAsync(int gatewayConfigId, CancellationToken ct = default)
        => Repo.Query()
            .Include(g => g.Channels)
            .FirstOrDefaultAsync(g => g.GatewayConfigId == gatewayConfigId, ct)!;

    public async Task<Result<PaymentGatewayConfig>> SaveAsync(int gatewayConfigId, PaymentGatewayConfig form, string secretMask, string? performedBy, CancellationToken ct = default)
    {
        var row = await Repo.FirstOrDefaultAsync(g => g.GatewayConfigId == gatewayConfigId, ct);
        if (row is null) return Result.Failure<PaymentGatewayConfig>(Error.NotFound("Gateway config not found."));

        // Non-secret fields: write-through.
        row.Name           = form.Name;
        row.SubTitle       = form.SubTitle;
        row.GatewayGroup   = form.GatewayGroup;
        row.LogoUrl        = form.LogoUrl;
        row.SortOrder      = form.SortOrder;
        row.IsEnabled      = form.IsEnabled;
        row.IsSandbox      = form.IsSandbox;
        row.Currency       = string.IsNullOrWhiteSpace(form.Currency) ? "BDT" : form.Currency;
        row.NotifyOnSuccess = form.NotifyOnSuccess;
        row.NotifyOnFailure = form.NotifyOnFailure;
        row.NotifyOnRefund  = form.NotifyOnRefund;
        row.UpdatedAtUtc    = DateTime.UtcNow;
        row.UpdatedBy       = performedBy;

        void UpsertSecret(string? incoming, Action<string?> setter)
        {
            if (incoming is null || incoming == secretMask) return;
            setter(incoming);
        }

        UpsertSecret(form.LiveBaseUrl,           v => row.LiveBaseUrl           = v);
        UpsertSecret(form.LiveUsername,          v => row.LiveUsername          = v);
        UpsertSecret(form.LivePassword,          v => row.LivePassword          = v);
        UpsertSecret(form.LiveAppKey,            v => row.LiveAppKey            = v);
        UpsertSecret(form.LiveAppSecret,         v => row.LiveAppSecret         = v);
        UpsertSecret(form.LiveMerchantId,        v => row.LiveMerchantId        = v);
        UpsertSecret(form.LiveMerchantNumber,    v => row.LiveMerchantNumber    = v);
        UpsertSecret(form.LiveCallbackUrl,       v => row.LiveCallbackUrl       = v);
        UpsertSecret(form.LiveWebhookUrl,        v => row.LiveWebhookUrl        = v);
        UpsertSecret(form.LiveIpnUrl,            v => row.LiveIpnUrl            = v);
        UpsertSecret(form.LiveFailCallbackUrl,   v => row.LiveFailCallbackUrl   = v);

        UpsertSecret(form.SandboxBaseUrl,        v => row.SandboxBaseUrl        = v);
        UpsertSecret(form.SandboxUsername,       v => row.SandboxUsername       = v);
        UpsertSecret(form.SandboxPassword,       v => row.SandboxPassword       = v);
        UpsertSecret(form.SandboxAppKey,         v => row.SandboxAppKey         = v);
        UpsertSecret(form.SandboxAppSecret,      v => row.SandboxAppSecret      = v);
        UpsertSecret(form.SandboxMerchantId,     v => row.SandboxMerchantId     = v);
        UpsertSecret(form.SandboxMerchantNumber, v => row.SandboxMerchantNumber = v);
        UpsertSecret(form.SandboxCallbackUrl,    v => row.SandboxCallbackUrl    = v);
        UpsertSecret(form.SandboxWebhookUrl,     v => row.SandboxWebhookUrl     = v);
        UpsertSecret(form.SandboxIpnUrl,         v => row.SandboxIpnUrl         = v);
        UpsertSecret(form.SandboxFailCallbackUrl,v => row.SandboxFailCallbackUrl = v);

        Repo.Update(row);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(row);
    }

    public async Task<Result<PaymentGatewayConfig>> ToggleEnabledAsync(int gatewayConfigId, CancellationToken ct = default)
    {
        var row = await Repo.FirstOrDefaultAsync(g => g.GatewayConfigId == gatewayConfigId, ct);
        if (row is null) return Result.Failure<PaymentGatewayConfig>(Error.NotFound("Gateway config not found."));
        row.IsEnabled = !row.IsEnabled;
        Repo.Update(row);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(row);
    }
}

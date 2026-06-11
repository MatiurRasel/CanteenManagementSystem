// =============================================================================
// GatewayConfigService  (Infrastructure.Payments)
// -----------------------------------------------------------------------------
// Concrete IGatewayConfigService backed by the 2-table design:
//   * CanteenPaymentGatewayConfigs — per-tenant config rows (one per gateway).
//   * CanteenGatewayChannels       — sub-channels under each config.
//
// Cache strategy: per-tenant tag "payments-config". Every write (creds, env,
// channel CRUD, test) invalidates the tag.
//
// ADR 0004: IUnitOfWork + IRepository<T>.
// =============================================================================

using Platform.Application.Persistence;
using Platform.Application.Abstractions.Caching;
using Platform.Application.Abstractions.Identity;
using Platform.Application.Abstractions.Payments;
using Platform.Application.Abstractions.Time;
using Platform.Application.Caching;
using Platform.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace Platform.Infrastructure.Payments;

public sealed class GatewayConfigService : IGatewayConfigService
{
    private const string CacheTag = "payments-config";

    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;

    public GatewayConfigService(IUnitOfWork uow, ICacheService cache, IClock clock, ICurrentUser user)
    {
        _uow = uow; _cache = cache; _clock = clock; _user = user;
    }

    private IRepository<PaymentGatewayConfig> Configs  => _uow.Repository<PaymentGatewayConfig>();
    private IRepository<GatewayChannel>       Channels => _uow.Repository<GatewayChannel>();

    // -------------------- read paths ---------------------------------------
    public async Task<GatewayCredentials?> ResolveAsync(PaymentMethod method, CancellationToken cancellationToken = default)
    {
        var key = $"payments:cfg:{method}";
        return await _cache.GetOrSetAsync(key, async ct =>
        {
            var cfg = await LoadByMethodAsync(method, ct);
            return cfg is null ? null : Project(cfg);
        }, ttl: CacheTtl.Short, tags: new[] { CacheTag }, cancellationToken: cancellationToken);
    }

    public Task<PaymentGatewayConfig?> GetConfigAsync(PaymentMethod method, CancellationToken cancellationToken = default)
        => LoadByMethodAsync(method, cancellationToken, tracked: false);

    public async Task<IReadOnlyList<PaymentGatewayConfig>> ListAsync(CancellationToken cancellationToken = default)
        => await Configs.NoTrackingQuery()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<GatewayChannel>> GetChannelsAsync(int gatewayConfigId, CancellationToken cancellationToken = default)
        => await Channels.NoTrackingQuery()
            .Where(ch => ch.GatewayConfigId == gatewayConfigId && ch.IsActive)
            .OrderBy(ch => ch.SortOrder).ThenBy(ch => ch.ChannelName)
            .ToListAsync(cancellationToken);

    // -------------------- write paths --------------------------------------
    public async Task<bool> SetSandboxAsync(int gatewayConfigId, bool isSandbox, CancellationToken cancellationToken = default)
    {
        var cfg = await Configs.FirstOrDefaultAsync(c => c.GatewayConfigId == gatewayConfigId, cancellationToken);
        if (cfg is null) return false;
        cfg.IsSandbox = isSandbox;
        Stamp(cfg);
        await _uow.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateTagAsync(CacheTag, cancellationToken);
        return true;
    }

    public async Task<bool> SaveCredentialsAsync(int gatewayConfigId, bool isLive, GatewayCredentialsInput input, CancellationToken cancellationToken = default)
    {
        var cfg = await Configs.FirstOrDefaultAsync(c => c.GatewayConfigId == gatewayConfigId, cancellationToken);
        if (cfg is null) return false;

        if (isLive) ApplyLive(cfg, input); else ApplySandbox(cfg, input);
        if (!string.IsNullOrEmpty(input.Currency)) cfg.Currency = input.Currency;
        Stamp(cfg);
        await _uow.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateTagAsync(CacheTag, cancellationToken);
        return true;
    }

    public async Task<int> UpsertChannelAsync(int gatewayConfigId, GatewayChannelInput input, CancellationToken cancellationToken = default)
    {
        GatewayChannel? row;
        if (input.ChannelId is int id and > 0)
        {
            row = await Channels.FirstOrDefaultAsync(c => c.ChannelId == id, cancellationToken);
            if (row is null) return 0;
        }
        else
        {
            row = new GatewayChannel
            {
                GatewayConfigId = gatewayConfigId,
                CreatedAtUtc = _clock.UtcNow
            };
            await Channels.AddAsync(row, cancellationToken);
        }

        row.ChannelCode      = input.ChannelCode;
        row.ChannelName      = input.ChannelName;
        row.IsActive         = input.IsActive;
        row.SortOrder        = input.SortOrder;
        row.ChargeType       = input.ChargeType;
        row.TotalCharge      = input.TotalCharge;
        row.MinChargeAmount  = input.MinChargeAmount;
        row.MaxChargeAmount  = input.MaxChargeAmount;
        row.VatTaxPercent    = input.VatTaxPercent;
        row.OurMarkup        = input.OurMarkup;
        row.LogoUrl          = input.LogoUrl;
        row.UpdatedAtUtc     = _clock.UtcNow;

        await _uow.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateTagAsync(CacheTag, cancellationToken);
        return row.ChannelId;
    }

    public async Task<bool> DeleteChannelAsync(int channelId, CancellationToken cancellationToken = default)
    {
        var row = await Channels.FirstOrDefaultAsync(c => c.ChannelId == channelId, cancellationToken);
        if (row is null) return false;
        Channels.Remove(row);
        await _uow.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateTagAsync(CacheTag, cancellationToken);
        return true;
    }

    public async Task<bool> RecordTestAsync(int gatewayConfigId, bool success, string? message, CancellationToken cancellationToken = default)
    {
        var cfg = await Configs.FirstOrDefaultAsync(c => c.GatewayConfigId == gatewayConfigId, cancellationToken);
        if (cfg is null) return false;
        cfg.LastTestStatus = success ? CanteenGatewayStatus.Testing : CanteenGatewayStatus.Configured;
        cfg.LastTestedAtUtc = _clock.UtcNow;
        cfg.LastTestMessage = message;
        if (success && cfg.Status == CanteenGatewayStatus.Configured) cfg.Status = CanteenGatewayStatus.Testing;
        Stamp(cfg);
        await _uow.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateTagAsync(CacheTag, cancellationToken);
        return true;
    }

    public async Task<bool> ToggleEnabledAsync(int gatewayConfigId, CancellationToken cancellationToken = default)
    {
        var cfg = await Configs.FirstOrDefaultAsync(c => c.GatewayConfigId == gatewayConfigId, cancellationToken);
        if (cfg is null) return false;
        cfg.IsEnabled = !cfg.IsEnabled;
        cfg.Status = cfg.IsEnabled ? cfg.Status : CanteenGatewayStatus.Disabled;
        Stamp(cfg);
        await _uow.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateTagAsync(CacheTag, cancellationToken);
        return true;
    }

    // -------------------- helpers ------------------------------------------
    private async Task<PaymentGatewayConfig?> LoadByMethodAsync(PaymentMethod method, CancellationToken cancellationToken, bool tracked = false)
    {
        var q = tracked ? Configs.Query() : Configs.NoTrackingQuery();
        return await q.Where(c => c.Method == method && c.IsEnabled)
            .OrderByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private void Stamp(PaymentGatewayConfig cfg)
    {
        cfg.UpdatedAtUtc = _clock.UtcNow;
        cfg.UpdatedBy = _user.UserId;
    }

    private static GatewayCredentials Project(PaymentGatewayConfig cfg)
    {
        var s = cfg.IsSandbox;
        return new GatewayCredentials(
            GatewayConfigId: cfg.GatewayConfigId,
            Method:          cfg.Method,
            GatewayCode:     cfg.GatewayCode,
            IsSandbox:       s,
            Currency:        cfg.Currency ?? "BDT",
            BaseUrl:         s ? cfg.SandboxBaseUrl        : cfg.LiveBaseUrl,
            Username:        s ? cfg.SandboxUsername       : cfg.LiveUsername,
            Password:        s ? cfg.SandboxPassword       : cfg.LivePassword,
            AppKey:          s ? cfg.SandboxAppKey         : cfg.LiveAppKey,
            AppSecret:       s ? cfg.SandboxAppSecret      : cfg.LiveAppSecret,
            MerchantId:      s ? cfg.SandboxMerchantId     : cfg.LiveMerchantId,
            MerchantNumber:  s ? cfg.SandboxMerchantNumber : cfg.LiveMerchantNumber,
            PublicKey:       s ? cfg.SandboxPublicKey      : cfg.LivePublicKey,
            PrivateKey:      s ? cfg.SandboxPrivateKey     : cfg.LivePrivateKey,
            CallbackUrl:     s ? cfg.SandboxCallbackUrl    : cfg.LiveCallbackUrl,
            WebhookUrl:      s ? cfg.SandboxWebhookUrl     : cfg.LiveWebhookUrl,
            IpnUrl:          s ? cfg.SandboxIpnUrl         : cfg.LiveIpnUrl,
            FailCallbackUrl: s ? cfg.SandboxFailCallbackUrl: cfg.LiveFailCallbackUrl);
    }

    private static void ApplyLive(PaymentGatewayConfig cfg, GatewayCredentialsInput src)
    {
        if (src.BaseUrl is not null)         cfg.LiveBaseUrl = src.BaseUrl;
        if (src.Username is not null)        cfg.LiveUsername = src.Username;
        if (src.Password is not null)        cfg.LivePassword = src.Password;
        if (src.AppKey is not null)          cfg.LiveAppKey = src.AppKey;
        if (src.AppSecret is not null)       cfg.LiveAppSecret = src.AppSecret;
        if (src.MerchantId is not null)      cfg.LiveMerchantId = src.MerchantId;
        if (src.MerchantNumber is not null)  cfg.LiveMerchantNumber = src.MerchantNumber;
        if (src.PublicKey is not null)       cfg.LivePublicKey = src.PublicKey;
        if (src.PrivateKey is not null)      cfg.LivePrivateKey = src.PrivateKey;
        if (src.CallbackUrl is not null)     cfg.LiveCallbackUrl = src.CallbackUrl;
        if (src.WebhookUrl is not null)      cfg.LiveWebhookUrl = src.WebhookUrl;
        if (src.IpnUrl is not null)          cfg.LiveIpnUrl = src.IpnUrl;
        if (src.FailCallbackUrl is not null) cfg.LiveFailCallbackUrl = src.FailCallbackUrl;
    }

    private static void ApplySandbox(PaymentGatewayConfig cfg, GatewayCredentialsInput src)
    {
        if (src.BaseUrl is not null)         cfg.SandboxBaseUrl = src.BaseUrl;
        if (src.Username is not null)        cfg.SandboxUsername = src.Username;
        if (src.Password is not null)        cfg.SandboxPassword = src.Password;
        if (src.AppKey is not null)          cfg.SandboxAppKey = src.AppKey;
        if (src.AppSecret is not null)       cfg.SandboxAppSecret = src.AppSecret;
        if (src.MerchantId is not null)      cfg.SandboxMerchantId = src.MerchantId;
        if (src.MerchantNumber is not null)  cfg.SandboxMerchantNumber = src.MerchantNumber;
        if (src.PublicKey is not null)       cfg.SandboxPublicKey = src.PublicKey;
        if (src.PrivateKey is not null)      cfg.SandboxPrivateKey = src.PrivateKey;
        if (src.CallbackUrl is not null)     cfg.SandboxCallbackUrl = src.CallbackUrl;
        if (src.WebhookUrl is not null)      cfg.SandboxWebhookUrl = src.WebhookUrl;
        if (src.IpnUrl is not null)          cfg.SandboxIpnUrl = src.IpnUrl;
        if (src.FailCallbackUrl is not null) cfg.SandboxFailCallbackUrl = src.FailCallbackUrl;
    }
}

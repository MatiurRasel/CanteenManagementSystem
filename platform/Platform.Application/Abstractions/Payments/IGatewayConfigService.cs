// =============================================================================
// IGatewayConfigService  (Application abstraction)
// -----------------------------------------------------------------------------
// Single seam every IPaymentGateway impl uses to load its credentials and
// available channels. The concrete service:
//
//   1. Reads PaymentGatewayConfig by PaymentMethod for the current tenant.
//   2. Picks the Sandbox* OR Live* field set based on cfg.IsSandbox.
//   3. Loads the active GatewayChannel rows under that config (for charge
//      calculation + UI rendering).
//   4. Caches the result for 60 seconds; busts the cache on any save.
// =============================================================================

using Platform.Domain.Payments;

namespace Platform.Application.Abstractions.Payments;

public interface IGatewayConfigService
{
    /// <summary>Resolved credentials for the current tenant + method (env-picked).</summary>
    Task<GatewayCredentials?> ResolveAsync(PaymentMethod method, CancellationToken cancellationToken = default);

    /// <summary>Full config row for the admin UI.</summary>
    Task<PaymentGatewayConfig?> GetConfigAsync(PaymentMethod method, CancellationToken cancellationToken = default);

    /// <summary>List configs for the admin overview screen.</summary>
    Task<IReadOnlyList<PaymentGatewayConfig>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Active channels under a gateway, sorted.</summary>
    Task<IReadOnlyList<GatewayChannel>> GetChannelsAsync(int gatewayConfigId, CancellationToken cancellationToken = default);

    Task<bool> SetSandboxAsync(int gatewayConfigId, bool isSandbox, CancellationToken cancellationToken = default);

    Task<bool> SaveCredentialsAsync(int gatewayConfigId, bool isLive, GatewayCredentialsInput input, CancellationToken cancellationToken = default);

    Task<int>  UpsertChannelAsync(int gatewayConfigId, GatewayChannelInput input, CancellationToken cancellationToken = default);
    Task<bool> DeleteChannelAsync(int channelId, CancellationToken cancellationToken = default);

    Task<bool> RecordTestAsync(int gatewayConfigId, bool success, string? message, CancellationToken cancellationToken = default);

    Task<bool> ToggleEnabledAsync(int gatewayConfigId, CancellationToken cancellationToken = default);
}

/// <summary>Env-picked credentials handed to the IPaymentGateway impls.</summary>
public sealed record GatewayCredentials(
    int           GatewayConfigId,
    PaymentMethod Method,
    string        GatewayCode,
    bool          IsSandbox,
    string        Currency,
    string?       BaseUrl,
    string?       Username,
    string?       Password,
    string?       AppKey,
    string?       AppSecret,
    string?       MerchantId,
    string?       MerchantNumber,
    string?       PublicKey,
    string?       PrivateKey,
    string?       CallbackUrl,
    string?       WebhookUrl,
    string?       IpnUrl,
    string?       FailCallbackUrl);

/// <summary>Input bag for SaveCredentialsAsync. Null fields are left untouched.</summary>
public sealed class GatewayCredentialsInput
{
    public string? BaseUrl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? AppKey { get; set; }
    public string? AppSecret { get; set; }
    public string? MerchantId { get; set; }
    public string? MerchantNumber { get; set; }
    public string? PublicKey { get; set; }
    public string? PrivateKey { get; set; }
    public string? CallbackUrl { get; set; }
    public string? WebhookUrl { get; set; }
    public string? IpnUrl { get; set; }
    public string? FailCallbackUrl { get; set; }
    public string? Currency { get; set; }
}

/// <summary>Upsert payload for a GatewayChannel row.</summary>
public sealed class GatewayChannelInput
{
    public int?   ChannelId { get; set; }     // null = insert
    public string ChannelCode { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public bool   IsActive { get; set; } = true;
    public int    SortOrder { get; set; }
    public string ChargeType { get; set; } = "P";
    public decimal TotalCharge { get; set; }
    public decimal MinChargeAmount { get; set; }
    public decimal MaxChargeAmount { get; set; }
    public decimal VatTaxPercent { get; set; }
    public decimal OurMarkup { get; set; }
    public string? LogoUrl { get; set; }
}

// =============================================================================
// GatewaysSeed  (Platform.Infrastructure.Seeding.Defaults)
// -----------------------------------------------------------------------------
// Order = 40. Seeds the platform-supported payment gateways + their default
// channels. Sandbox mode is ON; tenants flip IsSandbox=false after pasting
// live credentials.
//
// EXTENDING WITH NEW PROVIDERS
//   Add a new Method enum value, a new gateway impl, and append a row here.
//   To override per tenant (different fee %), edit the row directly via
//   /api/v1/admin/gateways/* — never via this seed.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Seeding;
using Platform.Application.Persistence;
using Platform.Domain.Payments;

namespace Platform.Infrastructure.Seeding.Defaults;

public sealed class GatewaysSeed : ISeedContributor
{
    public int Order => 40;

    public static readonly IReadOnlyList<GatewayDefinition> Defaults = new[]
    {
        new GatewayDefinition(
            "cash", "Cash Counter", PaymentMethod.Cash, "cash", "BDT", IsSandbox: false,
            new[] { new ChannelDefinition("cash", "Cash", "F", 0m, 0m, 0m, 0m, 0m) }),

        new GatewayDefinition(
            "ssl", "SSLCommerz", PaymentMethod.SslCommerz, "agg", "BDT", IsSandbox: true,
            new[]
            {
                new ChannelDefinition("visa",   "Visa / MasterCard",  "P", 2.5m,  0m, 0m, 15m, 0m),
                new ChannelDefinition("bkash",  "bKash via SSL",       "P", 1.85m, 0m, 0m, 15m, 0m),
                new ChannelDefinition("nagad",  "Nagad via SSL",       "P", 1.8m,  0m, 0m, 15m, 0m),
                new ChannelDefinition("rocket", "Rocket",              "P", 1.8m,  0m, 0m, 15m, 0m),
            }),

        new GatewayDefinition(
            "bkash", "bKash Tokenized", PaymentMethod.Bkash, "mfs", "BDT", IsSandbox: true,
            new[] { new ChannelDefinition("bkash", "bKash", "P", 1.85m, 0m, 0m, 15m, 0m) }),

        new GatewayDefinition(
            "nagad", "Nagad", PaymentMethod.Nagad, "mfs", "BDT", IsSandbox: true,
            new[] { new ChannelDefinition("nagad", "Nagad", "P", 1.8m, 0m, 0m, 15m, 0m) }),

        new GatewayDefinition(
            "stripe", "Stripe", PaymentMethod.Stripe, "cards", "USD", IsSandbox: true,
            new[] { new ChannelDefinition("card", "Card", "P", 2.9m, 0m, 0m, 0m, 0m) }),
    };

    private readonly IAppDbContext _db;
    private readonly ILogger<GatewaysSeed> _logger;

    public GatewaysSeed(IAppDbContext db, ILogger<GatewaysSeed> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var gateways = _db.Set<PaymentGatewayConfig>();
        var channels = _db.Set<GatewayChannel>();

        var gatewaysInserted = 0;
        var channelsInserted = 0;

        foreach (var def in Defaults)
        {
            var gateway = await gateways.FirstOrDefaultAsync(g => g.GatewayCode == def.Code, cancellationToken);
            if (gateway is null)
            {
                gateway = new PaymentGatewayConfig
                {
                    GatewayCode = def.Code,
                    Name = def.Name,
                    Method = def.Method,
                    GatewayGroup = def.Group,
                    Currency = def.Currency,
                    IsSandbox = def.IsSandbox,
                    Status = CanteenGatewayStatus.Configured,
                    IsEnabled = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                gateways.Add(gateway);
                await _db.SaveChangesAsync(cancellationToken);
                gatewaysInserted++;
            }

            foreach (var ch in def.Channels)
            {
                var exists = await channels.AnyAsync(c => c.GatewayConfigId == gateway.GatewayConfigId && c.ChannelCode == ch.Code, cancellationToken);
                if (exists) continue;
                channels.Add(new GatewayChannel
                {
                    GatewayConfigId = gateway.GatewayConfigId,
                    ChannelCode = ch.Code,
                    ChannelName = ch.Name,
                    IsActive = true,
                    ChargeType = ch.ChargeType,
                    TotalCharge = ch.TotalCharge,
                    MinChargeAmount = ch.MinChargeAmount,
                    MaxChargeAmount = ch.MaxChargeAmount,
                    VatTaxPercent = ch.VatTaxPercent,
                    OurMarkup = ch.OurMarkup
                });
                channelsInserted++;
            }
        }

        if (gatewaysInserted > 0 || channelsInserted > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Gateways} gateway(s) and {Channels} channel(s).", gatewaysInserted, channelsInserted);
        }
    }

    public sealed record GatewayDefinition(
        string Code,
        string Name,
        PaymentMethod Method,
        string Group,
        string Currency,
        bool IsSandbox,
        IReadOnlyList<ChannelDefinition> Channels);

    public sealed record ChannelDefinition(
        string Code,
        string Name,
        string ChargeType,
        decimal TotalCharge,
        decimal MinChargeAmount,
        decimal MaxChargeAmount,
        decimal VatTaxPercent,
        decimal OurMarkup);
}

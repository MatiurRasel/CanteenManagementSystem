// =============================================================================
// NbrReceiptServiceProduction  (CanteenManagementSystem.Infrastructure.Receipts)
// -----------------------------------------------------------------------------
// PRODUCTION-READY skeleton for the BD NBR Mushak 6.3 e-receipt integration.
//
// NOT wired in DI by default — `StubNbrReceiptService` is the registered impl
// until a tenant onboards with NBR. To go live:
//   1. Tenant pastes Nbr.MerchantToken (secret) + Nbr.SigningKey (PEM secret).
//   2. Switch the DI registration to this class.
//   3. Adjust the XML envelope to match the exact NBR sandbox response.
//
// THIS FILE INTENTIONALLY contains the full HTTP + signing skeleton WITHOUT
// being registered, so a deployment engineer with NBR sandbox credentials
// can flip ONE DI line + tune one envelope template. Real-world NBR onboard-
// ing involves filed paperwork; the platform code is ready.
//
// REFERENCES
//   * NBR e-Mushak portal: https://emushak.nbr.gov.bd
//   * Mushak 6.3 format: NBR e-receipt schema docs (tenant receives URL on onboarding)
//
// ADR 0004: IReadOnlyRepository<Order> — pure read.
// =============================================================================

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Receipts;
using Platform.Application.Persistence;
using CanteenOrders = CanteenManagementSystem.Domain.Orders;

namespace CanteenManagementSystem.Infrastructure.Receipts;

public sealed class NbrReceiptServiceProduction : INbrReceiptService
{
    private readonly IHttpClientFactory _http;
    private readonly ITenantSettings _settings;
    private readonly IReadOnlyRepository<CanteenOrders.Order> _orders;
    private readonly ILogger<NbrReceiptServiceProduction> _logger;

    public NbrReceiptServiceProduction(
        IHttpClientFactory http, ITenantSettings settings,
        IReadOnlyRepository<CanteenOrders.Order> orders, ILogger<NbrReceiptServiceProduction> logger)
    {
        _http = http; _settings = settings; _orders = orders; _logger = logger;
    }

    public async Task<NbrSubmissionResult?> SubmitAsync(int orderId, CancellationToken cancellationToken = default)
    {
        // ── Gate by tenant settings ────────────────────────────────────────
        var enabled = await _settings.GetBoolAsync("Nbr.Enabled", false, cancellationToken);
        if (!enabled) return null;

        var bin       = await _settings.GetAsync("Nbr.Bin",            defaultValue: null, cancellationToken);
        var token     = await _settings.GetAsync("Nbr.MerchantToken",  defaultValue: null, cancellationToken);
        var signKey   = await _settings.GetAsync("Nbr.SigningKey",     defaultValue: null, cancellationToken);    // RSA PEM, IsSecret=true
        var baseUrl   = await _settings.GetAsync("Nbr.GatewayBaseUrl", defaultValue: null, cancellationToken);
        if (string.IsNullOrWhiteSpace(bin)   || string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(signKey) || string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("NBR.Production: missing required tenant settings — skipping order {Order}.", orderId);
            return null;
        }

        // ── Load order + items ────────────────────────────────────────────
        var order = await _orders.NoTrackingQuery()
            .Where(o => o.OrderID == orderId)
            .Select(o => new
            {
                o.OrderID, o.OrderNumber, o.OrderDate, o.TotalAmount, o.UserId,
                Items = o.OrderItems.Select(oi => new {
                    name = oi.FoodItem.ItemName, qty = oi.Quantity, price = oi.UnitPrice, line = oi.TotalPrice
                }).ToList()
            }).FirstOrDefaultAsync(cancellationToken);
        if (order is null) return null;

        // ── Build Mushak 6.3 envelope (XML; exact shape per NBR onboarding pack) ──
        var xml = new XElement("Mushak63",
            new XAttribute("version", "1.0"),
            new XElement("Bin", bin),
            new XElement("Invoice",
                new XElement("Number",   order.OrderNumber),
                new XElement("IssuedAt", order.OrderDate.ToString("yyyy-MM-ddTHH:mm:sszzz")),
                new XElement("Buyer",    order.UserId),
                new XElement("Total",    order.TotalAmount.ToString("0.00"))),
            new XElement("Items",
                order.Items.Select(i => new XElement("Item",
                    new XElement("Name",   i.name),
                    new XElement("Qty",    i.qty),
                    new XElement("Price",  i.price.ToString("0.00")),
                    new XElement("Total",  i.line.ToString("0.00"))))));

        var payload = xml.ToString(SaveOptions.DisableFormatting);

        // ── Sign the payload (RSA-SHA256 over the canonical bytes) ───────
        var signature = SignRsaSha256(payload, signKey!);

        // ── POST ──────────────────────────────────────────────────────────
        var http = _http.CreateClient("nbr.gateway");
        http.BaseAddress = new Uri(baseUrl!);
        http.Timeout     = TimeSpan.FromSeconds(20);

        using var req = new HttpRequestMessage(HttpMethod.Post, "submit");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.TryAddWithoutValidation("X-Signature", signature);
        req.Content = new StringContent(payload, Encoding.UTF8, "application/xml");

        try
        {
            using var res = await http.SendAsync(req, cancellationToken);
            var body = await res.Content.ReadAsStringAsync(cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("NBR submission failed ({Status}) for order {Order}: {Body}", res.StatusCode, orderId, Truncate(body, 600));
                return null;
            }

            // The exact response shape is dictated by NBR. The block below is a
            // best-guess parser; replace with the spec's JSON / XML shape once
            // your tenant has the sandbox response examples.
            string frn = TryParseFrn(body);
            var verifyUrl = $"{baseUrl!.TrimEnd('/')}/verify?frn={Uri.EscapeDataString(frn)}";
            _logger.LogInformation("NBR submission ok — FRN {Frn} for order {Order}.", frn, orderId);

            return new NbrSubmissionResult(frn, verifyUrl, DateTime.UtcNow, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NBR submission threw for order {Order}", orderId);
            return null;
        }
    }

    /// <summary>RSA-SHA256 sign the payload using a PEM private key from tenant settings.</summary>
    private static string SignRsaSha256(string payload, string pemPrivateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pemPrivateKey);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var sig = rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(sig);
    }

    /// <summary>Tolerant FRN extractor — supports JSON `{"frn":"..."}` OR XML `<Frn>…</Frn>`. Replace per the actual NBR contract.</summary>
    private static string TryParseFrn(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("frn", out var f) && f.ValueKind == JsonValueKind.String)
                return f.GetString()!;
        }
        catch { /* fall through to XML */ }
        try
        {
            var xml = XDocument.Parse(body);
            var f = xml.Descendants("Frn").FirstOrDefault()?.Value;
            if (!string.IsNullOrWhiteSpace(f)) return f!;
        }
        catch { /* ignore */ }
        return $"FRN-UNKNOWN-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

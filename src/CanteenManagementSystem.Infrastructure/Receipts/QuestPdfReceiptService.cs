// =============================================================================
// QuestPdfReceiptService  (Infrastructure)
// -----------------------------------------------------------------------------
// Renders BD-NBR compliant customer receipts and 80mm kitchen tickets using
// QuestPDF (MIT, free under $1M ARR — see https://www.questpdf.com).
//
// LAYOUT
//   A5 portrait. Header strip with tenant logo + name + BIN. Body table of
//   items (qty / name / unit / total). Footer with VAT line, total in words,
//   QR for verification (encodes "{baseUrl}/r/{orderNumber}"), and a small
//   thank-you. Kitchen ticket is 80mm wide, no VAT, big font for legibility.
//
// QR FLOW
//   - Customer scans QR -> hits /r/{orderNumber}
//   - Public endpoint shows order number, items, total. No login required.
//   - Confirms authenticity to auditors and customers.
//
// BRANDING
//   All cosmetic values come from BrandingConfiguration + tenant settings.
//   The "VAT.Rate", "VAT.Bin", "Tenant.Name", "Tenant.Address" override the
//   appsettings defaults — operators can change them without redeploy.
//
// ADR 0004: IReadOnlyRepository<Order> — pure read.
// =============================================================================

using System.Globalization;
using Platform.Application.Persistence;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Receipts;
using Platform.Application.Configuration;
using CanteenManagementSystem.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CanteenManagementSystem.Infrastructure.Receipts;

internal sealed class QuestPdfReceiptService : IReceiptService
{
    static QuestPdfReceiptService()
    {
        // Community license is fine for SaaS < $1M ARR.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private readonly IReadOnlyRepository<Order> _orders;
    private readonly ITenantSettings _settings;
    private readonly BrandingConfiguration _branding;

    public QuestPdfReceiptService(IReadOnlyRepository<Order> orders, ITenantSettings settings, IOptions<BrandingConfiguration> branding)
    {
        _orders = orders;
        _settings = settings;
        _branding = branding.Value;
    }

    public async Task<ReceiptPdfResult> GenerateOrderReceiptAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderId, cancellationToken);
        var (vatRate, bin, tenantName, tenantAddress, verifyBase) = await LoadBrandingAsync(cancellationToken);
        var qrPng = BuildQrPng($"{verifyBase}/r/{order.OrderNumber}");

        var subtotal = order.TotalAmount;
        var vatAmount = decimal.Round(subtotal * vatRate, 2);
        var grandTotal = subtotal + vatAmount;

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(tenantName).Bold().FontSize(16);
                        col.Item().Text(tenantAddress).FontSize(9).FontColor(Colors.Grey.Darken2);
                        if (!string.IsNullOrEmpty(bin)) col.Item().Text($"BIN: {bin}").FontSize(9).FontColor(Colors.Grey.Darken2);
                    });
                    row.ConstantItem(120).AlignRight().Column(col =>
                    {
                        col.Item().Text("RECEIPT").Bold().FontSize(14).FontColor(_branding.AccentColor);
                        col.Item().Text($"# {order.OrderNumber}").FontSize(10);
                        col.Item().Text(order.OrderDate.ToString("dd MMM yyyy hh:mm tt", CultureInfo.InvariantCulture)).FontSize(9);
                    });
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    col.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Text($"Customer: {order.UserId}");
                        r.ConstantItem(160).AlignRight().Text($"Type: {order.UserType}");
                    });

                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c => {
                            c.ConstantColumn(28);
                            c.RelativeColumn(3);
                            c.ConstantColumn(60);
                            c.ConstantColumn(70);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("#").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Item").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Qty × Price").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Total").Bold();
                        });
                        int i = 1;
                        foreach (var item in order.OrderItems)
                        {
                            table.Cell().Padding(4).Text(i++.ToString());
                            table.Cell().Padding(4).Text(item.FoodItem?.ItemName ?? "—");
                            table.Cell().Padding(4).AlignRight().Text($"{item.Quantity} × ৳{item.UnitPrice:N2}");
                            table.Cell().Padding(4).AlignRight().Text($"৳{item.TotalPrice:N2}");
                        }
                    });

                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                    col.Item().PaddingTop(6).AlignRight().Column(totals =>
                    {
                        totals.Item().Text(t => { t.Span("Subtotal: "); t.Span($"৳{subtotal:N2}").Bold(); });
                        if (vatRate > 0)
                        {
                            totals.Item().Text(t => { t.Span($"VAT ({vatRate:P1}): "); t.Span($"৳{vatAmount:N2}"); });
                        }
                        totals.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Total: ").Bold().FontSize(12);
                            t.Span($"৳{grandTotal:N2}").Bold().FontSize(12).FontColor(_branding.AccentColor);
                        });
                    });
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Thank you for your visit.").FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text("Verify this receipt by scanning the QR.").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                    row.ConstantItem(70).AlignRight().Image(qrPng);
                });
            });
        }).GeneratePdf();

        return new ReceiptPdfResult(pdf, $"receipt-{order.OrderNumber}.pdf", "application/pdf");
    }

    public async Task<ReceiptPdfResult> GenerateKitchenTicketAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderId, cancellationToken);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                // 80mm thermal printer; QuestPDF accepts custom point sizes.
                page.Size(80 * 2.83f, 200 * 2.83f); // pts (1mm = 2.83pt approx)
                page.Margin(8);
                page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(11));

                page.Content().Column(col =>
                {
                    col.Item().AlignCenter().Text("KITCHEN").Bold().FontSize(14);
                    col.Item().AlignCenter().Text($"# {order.OrderNumber}").Bold().FontSize(16);
                    col.Item().AlignCenter().Text(order.OrderDate.ToString("HH:mm")).FontSize(11);
                    col.Item().PaddingVertical(4).LineHorizontal(1);

                    foreach (var item in order.OrderItems)
                    {
                        col.Item().Row(r =>
                        {
                            r.ConstantItem(28).Text($"{item.Quantity}×").Bold();
                            r.RelativeItem().Text(item.FoodItem?.ItemName ?? "—").Bold();
                        });
                    }

                    col.Item().PaddingTop(6).LineHorizontal(1);
                    col.Item().AlignCenter().PaddingTop(4).Text($"Customer: {order.UserId}").FontSize(9);
                });
            });
        }).GeneratePdf();

        return new ReceiptPdfResult(pdf, $"kitchen-{order.OrderNumber}.pdf", "application/pdf");
    }

    private async Task<Order> LoadOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await _orders.NoTrackingQuery()
            .Include(o => o.OrderItems)
            .ThenInclude(i => i.FoodItem)
            .FirstOrDefaultAsync(o => o.OrderID == orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");
        return order;
    }

    private async Task<(decimal vatRate, string? bin, string tenantName, string tenantAddress, string verifyBase)> LoadBrandingAsync(CancellationToken cancellationToken)
    {
        var vatRate    = await _settings.GetDecimalAsync("VAT.Rate", 0m, cancellationToken);
        var bin        = await _settings.GetAsync("VAT.Bin", null, cancellationToken);
        var tenantName = await _settings.GetAsync("Tenant.Name", _branding.AppName, cancellationToken) ?? _branding.AppName;
        var address    = await _settings.GetAsync("Tenant.Address", "", cancellationToken) ?? "";
        var verifyBase = await _settings.GetAsync("Receipt.VerifyBaseUrl", "https://example.com", cancellationToken) ?? "https://example.com";
        return (vatRate, bin, tenantName, address, verifyBase);
    }

    private static byte[] BuildQrPng(string content)
    {
        using var qr = new QRCodeGenerator();
        var data = qr.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var png  = new PngByteQRCode(data);
        return png.GetGraphic(8);
    }
}

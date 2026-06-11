// =============================================================================
// EscPosNetworkPrinter  (CanteenManagementSystem.Infrastructure.Receipts)
// -----------------------------------------------------------------------------
// Concrete INetworkReceiptPrinter that sends ESC/POS byte sequences directly
// to a TCP socket. Works with the majority of thermal POS printers
// (Epson TM-T20/T82, Xprinter XP-58, Citizen CT-S310, Aclas PP6, etc.) that
// accept raw text on port 9100 by default.
//
// BYTE FORMAT
//   ESC @           initialise printer       (reset to default state)
//   ESC ! n         select character mode    (bit-mapped: bold/double-height/width)
//   ESC a n         justify                  (0 = left, 1 = center, 2 = right)
//   GS V 0          partial cut              (paper cut at end of print)
//
// We deliberately keep the ESC/POS layer LIGHT — no barcode, no QR, no images.
// The vast majority of canteen tickets are pure text + cut. Anything richer
// belongs in QuestPdfReceiptService.GenerateOrderReceiptAsync.
//
// MULTI-TENANT
//   Connection details come from ITenantSettings via DirectorySettingsKeys.* -
//   sorry, ReceiptPrinterSettingsKeys here. The factory is created PER CALL so
//   a tenant admin can re-paste the printer IP and have the next print honour it.
//
// ADR 0004: IReadOnlyRepository<Order> — pure read.
// =============================================================================

using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Receipts;
using Platform.Application.Persistence;
using Platform.Domain.Tenancy;
using CanteenOrders = CanteenManagementSystem.Domain.Orders;

namespace CanteenManagementSystem.Infrastructure.Receipts;

public static class ReceiptPrinterSettingsKeys
{
    public const string Host       = "Printer.Network.Host";
    public const string Port       = "Printer.Network.Port";
    public const string TimeoutMs  = "Printer.Network.TimeoutMs";
    public const string CodePage   = "Printer.Network.CodePage";
    public const string LineWidth  = "Printer.Network.LineWidth";   // 32 for 58 mm, 48 for 80 mm
}

public sealed class EscPosNetworkPrinter : INetworkReceiptPrinter
{
    private static readonly byte[] Esc     = { 0x1B };
    private static readonly byte[] Init    = { 0x1B, 0x40 };                 // ESC @
    private static readonly byte[] CutLine = { 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x00 };
    private static readonly byte[] BoldOn  = { 0x1B, 0x21, 0x08 };           // emphasis on
    private static readonly byte[] BoldOff = { 0x1B, 0x21, 0x00 };
    private static readonly byte[] CenterOn = { 0x1B, 0x61, 0x01 };
    private static readonly byte[] LeftOn   = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] DoubleOn = { 0x1B, 0x21, 0x30 };          // double width + height
    private static readonly byte[] DoubleOff= { 0x1B, 0x21, 0x00 };
    // ESC p m t1 t2 — m=0 (drawer pin 2), t1=50ms on-pulse, t2=50ms off-pulse.
    // Compatible with the vast majority of cash drawers wired to printer RJ11.
    private static readonly byte[] KickPin2 = { 0x1B, 0x70, 0x00, 0x32, 0x32 };

    private readonly ITenantSettings _settings;
    private readonly IReadOnlyRepository<CanteenOrders.Order> _orders;
    private readonly ITenantContext  _tenant;
    private readonly ILogger<EscPosNetworkPrinter> _logger;

    public EscPosNetworkPrinter(
        ITenantSettings settings,
        IReadOnlyRepository<CanteenOrders.Order> orders,
        ITenantContext tenant,
        ILogger<EscPosNetworkPrinter> logger)
    {
        _settings = settings; _orders = orders; _tenant = tenant; _logger = logger;
    }

    public async Task<PrintResult> PrintTestAsync(CancellationToken cancellationToken = default)
    {
        var width = await _settings.GetIntAsync(ReceiptPrinterSettingsKeys.LineWidth, 48, cancellationToken);
        using var ms = new MemoryStream();
        Write(ms, Init);
        Write(ms, CenterOn); Write(ms, BoldOn); Write(ms, DoubleOn);
        WriteLine(ms, "TEST PRINT");
        Write(ms, DoubleOff); Write(ms, BoldOff);
        WriteLine(ms, _tenant.ClientCode ?? string.Empty);
        WriteLine(ms, DateTime.Now.ToString("u"));
        Write(ms, LeftOn);
        WriteLine(ms, new string('-', Math.Min(width, 48)));
        WriteLine(ms, "If you can read this clearly, the printer is wired correctly.");
        Write(ms, CutLine);
        return await SendAsync(ms.ToArray(), cancellationToken);
    }

    public async Task<PrintResult> PrintReceiptAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orders.NoTrackingQuery()
            .Where(o => o.OrderID == orderId)
            .Select(o => new {
                o.OrderID, o.OrderNumber, o.OrderDate, o.TotalAmount, o.UserId, o.UserType,
                Items = o.OrderItems.Select(oi => new { oi.FoodItem.ItemName, oi.Quantity, oi.UnitPrice, oi.TotalPrice }).ToList()
            }).FirstOrDefaultAsync(cancellationToken);
        if (order is null) return PrintResult.Fail("Order not found.");

        var width = await _settings.GetIntAsync(ReceiptPrinterSettingsKeys.LineWidth, 48, cancellationToken);
        using var ms = new MemoryStream();
        Write(ms, Init);
        Write(ms, CenterOn); Write(ms, BoldOn);
        WriteLine(ms, _tenant.ClientCode ?? "CANTEEN");
        Write(ms, BoldOff); Write(ms, LeftOn);
        WriteLine(ms, new string('-', width));
        WriteLine(ms, $"Order #: {order.OrderNumber}");
        WriteLine(ms, $"Date   : {order.OrderDate:yyyy-MM-dd HH:mm}");
        WriteLine(ms, $"User   : {order.UserId} ({order.UserType})");
        WriteLine(ms, new string('-', width));

        foreach (var i in order.Items)
        {
            var left  = TwoCols($"{i.Quantity} x {i.ItemName}", $"{i.TotalPrice:N0}", width);
            WriteLine(ms, left);
        }
        WriteLine(ms, new string('-', width));
        Write(ms, BoldOn);
        WriteLine(ms, TwoCols("TOTAL", $"BDT {order.TotalAmount:N0}", width));
        Write(ms, BoldOff);
        WriteLine(ms, string.Empty);
        Write(ms, CenterOn);
        WriteLine(ms, "Thank you. Enjoy your meal.");
        Write(ms, LeftOn);
        Write(ms, CutLine);

        return await SendAsync(ms.ToArray(), cancellationToken);
    }

    public async Task<PrintResult> KickDrawerAsync(CancellationToken cancellationToken = default)
    {
        // Just the kick-out pulse — no init, no cut. Drawer responds on its own pin.
        return await SendAsync(KickPin2, cancellationToken);
    }

    public async Task<PrintResult> PrintKitchenTicketAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orders.NoTrackingQuery()
            .Where(o => o.OrderID == orderId)
            .Select(o => new {
                o.OrderNumber, o.OrderDate, o.UserId,
                Items = o.OrderItems.Select(oi => new { oi.FoodItem.ItemName, oi.Quantity }).ToList()
            }).FirstOrDefaultAsync(cancellationToken);
        if (order is null) return PrintResult.Fail("Order not found.");

        var width = await _settings.GetIntAsync(ReceiptPrinterSettingsKeys.LineWidth, 48, cancellationToken);
        using var ms = new MemoryStream();
        Write(ms, Init);
        Write(ms, CenterOn); Write(ms, BoldOn); Write(ms, DoubleOn);
        WriteLine(ms, $"#{order.OrderNumber}");
        Write(ms, DoubleOff); Write(ms, BoldOff); Write(ms, LeftOn);
        WriteLine(ms, $"Placed: {order.OrderDate:HH:mm}");
        WriteLine(ms, new string('-', width));
        foreach (var i in order.Items)
        {
            WriteLine(ms, $"  {i.Quantity}  x  {i.ItemName}");
        }
        Write(ms, CutLine);
        return await SendAsync(ms.ToArray(), cancellationToken);
    }

    // ─── helpers ──────────────────────────────────────────────────────────

    private async Task<PrintResult> SendAsync(byte[] payload, CancellationToken ct)
    {
        var host = await _settings.GetAsync(ReceiptPrinterSettingsKeys.Host, defaultValue: null, ct);
        if (string.IsNullOrWhiteSpace(host))
            return PrintResult.Fail($"No printer host configured for tenant '{_tenant.ClientCode}'.");

        var port = await _settings.GetIntAsync(ReceiptPrinterSettingsKeys.Port, 9100, ct);
        var timeoutMs = await _settings.GetIntAsync(ReceiptPrinterSettingsKeys.TimeoutMs, 4000, ct);

        try
        {
            using var tcp = new TcpClient { SendTimeout = timeoutMs, ReceiveTimeout = timeoutMs };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
            await tcp.ConnectAsync(host!, port, cts.Token);
            await using var stream = tcp.GetStream();
            await stream.WriteAsync(payload, cts.Token);
            await stream.FlushAsync(cts.Token);
            return PrintResult.Ok(payload.Length);
        }
        catch (OperationCanceledException)
        {
            return PrintResult.Fail($"Timeout after {timeoutMs} ms talking to {host}:{port}.");
        }
        catch (SocketException ex)
        {
            _logger.LogWarning(ex, "Printer socket error to {Host}:{Port}", host, port);
            return PrintResult.Fail($"Cannot reach printer {host}:{port} ({ex.SocketErrorCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Printer unknown error to {Host}:{Port}", host, port);
            return PrintResult.Fail($"Printer error: {ex.Message}");
        }
    }

    private static void Write(Stream s, byte[] bytes) => s.Write(bytes, 0, bytes.Length);
    private static void WriteLine(Stream s, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text + "\n");
        s.Write(bytes, 0, bytes.Length);
    }
    private static string TwoCols(string left, string right, int width)
    {
        if (left.Length + 1 + right.Length >= width) return left + " " + right;
        return left + new string(' ', width - left.Length - right.Length) + right;
    }
}

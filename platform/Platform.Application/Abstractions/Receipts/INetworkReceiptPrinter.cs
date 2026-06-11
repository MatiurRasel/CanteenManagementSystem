// =============================================================================
// INetworkReceiptPrinter  (Platform.Application.Abstractions.Receipts)
// -----------------------------------------------------------------------------
// Drives a thermal receipt printer over TCP (most ESC/POS printers expose a
// raw socket on port 9100). The implementation lives in Platform.Infrastructure
// so this abstraction stays AspNetCore-free.
//
// CONFIG (tenant settings — read by the impl)
//   Printer.Network.Host            e.g. "192.168.1.45"
//   Printer.Network.Port            default 9100
//   Printer.Network.TimeoutMs       default 4000
//   Printer.Network.CodePage        default "cp437" (Latin); "cp864" for Arabic, etc.
//
// PUBLIC SURFACE
//   PrintReceiptAsync(orderId)      print the customer-facing receipt
//   PrintKitchenTicketAsync(orderId) print the slim kitchen ticket
//   PrintTestAsync()                "hello, world" smoke test from admin UI
//
// All three return PrintResult so the caller can surface the failure mode
// (network unreachable / wrong port / timeout) on the admin page.
// =============================================================================

namespace Platform.Application.Abstractions.Receipts;

public interface INetworkReceiptPrinter
{
    Task<PrintResult> PrintReceiptAsync(int orderId, CancellationToken cancellationToken = default);
    Task<PrintResult> PrintKitchenTicketAsync(int orderId, CancellationToken cancellationToken = default);
    Task<PrintResult> PrintTestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Trigger the connected cash drawer. ESC/POS drawers are wired to the
    /// printer's RJ11 jack and respond to the <c>ESC p m t1 t2</c> kick-out
    /// command. Sends the byte sequence over the same TCP socket. Every kick
    /// is audited via <see cref="Platform.Application.Abstractions.Audit.IAuditTrail"/>.
    /// </summary>
    Task<PrintResult> KickDrawerAsync(CancellationToken cancellationToken = default);
}

public sealed record PrintResult(bool Success, string Message, int BytesSent)
{
    public static PrintResult Ok(int bytes)            => new(true, $"Sent {bytes} bytes.", bytes);
    public static PrintResult Fail(string message)     => new(false, message, 0);
}

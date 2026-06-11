// =============================================================================
// IAdminExportsService  (CanteenManagementSystem.Application.Exports)
// -----------------------------------------------------------------------------
// Read-only export aggregator for the /admin/exports/* CSV endpoints. Kept
// behind a service so the controller doesn't touch IAppDbContext (ADR-0004).
// =============================================================================

using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Application.Exports;

public sealed record OrderExportRow(
    string OrderNumber,
    DateTime OrderDate,
    DateTime? DeliveredDate,
    CanteenOrderStatus Status,
    string UserId,
    CanteenUserType UserType,
    decimal TotalAmount,
    bool IsPreOrder,
    DateTime? PickupAtUtc,
    string? TableNumber,
    string Items);

public sealed record WalletLedgerExportRow(
    long LedgerId,
    string UserId,
    string EntryType,
    decimal Amount,
    decimal BalanceAfter,
    decimal BlockedAfter,
    int? OrderId,
    string? Reason,
    DateTime CreatedAtUtc,
    string? CreatedBy,
    string? IdempotencyKey);

public interface IAdminExportsService
{
    Task<IReadOnlyList<OrderExportRow>> GetOrdersAsync(
        DateTime fromUtc, DateTime toUtcExclusive,
        CanteenOrderStatus? status, int maxRows,
        CancellationToken ct = default);

    Task<IReadOnlyList<WalletLedgerExportRow>> GetWalletLedgerAsync(
        DateTime fromUtc, DateTime toUtcExclusive,
        string? userId, int maxRows,
        CancellationToken ct = default);
}

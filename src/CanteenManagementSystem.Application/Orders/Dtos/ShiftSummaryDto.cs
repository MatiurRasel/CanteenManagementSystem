// =============================================================================
// ShiftSummaryDto  (CanteenManagementSystem.Application.Orders.Dtos)
// -----------------------------------------------------------------------------
// End-of-shift summary the counter operator prints before signing off.
// Aggregates today's Orders + WalletLedger into a single record so the view
// is a flat data-binding exercise (no extra DB hits in Razor).
// =============================================================================

namespace CanteenManagementSystem.Application.Orders.Dtos;

public sealed class ShiftSummaryDto
{
    public DateTime ShiftDate { get; init; }
    public int OrdersPlaced { get; init; }
    public int OrdersDelivered { get; init; }
    public int OrdersPending { get; init; }
    public int OrdersCancelled { get; init; }
    public decimal GrossRevenue { get; init; }       // sum of delivered TotalAmount
    public decimal Refunded { get; init; }            // sum of refund ledger entries today
    public decimal NetRevenue => GrossRevenue - Refunded;
    public int UniqueCustomers { get; init; }
    public IReadOnlyList<ShiftTopItemRow> TopItems { get; init; } = Array.Empty<ShiftTopItemRow>();
    public IReadOnlyList<ShiftCancelledRow> Cancelled { get; init; } = Array.Empty<ShiftCancelledRow>();
    public DateTime? FirstOrderAtUtc { get; init; }
    public DateTime? LastOrderAtUtc { get; init; }
}

public sealed record ShiftTopItemRow(string ItemName, int QuantitySold, decimal Revenue);
public sealed record ShiftCancelledRow(int OrderId, string OrderNumber, decimal Amount, DateTime At, string? Reason);

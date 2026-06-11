using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Domain.Orders;

/// Encapsulates the legal state transitions described in §6.1 of the source of
/// truth: Placed → Confirmed → Preparing → Ready → Delivered → Completed.
public static class OrderLifecycle
{
    private static readonly Dictionary<CanteenOrderStatus, CanteenOrderStatus[]> _allowed = new()
    {
        [CanteenOrderStatus.Pending]    = new[] { CanteenOrderStatus.Placed, CanteenOrderStatus.Cancelled },
        [CanteenOrderStatus.Placed]     = new[] { CanteenOrderStatus.Confirmed, CanteenOrderStatus.Cancelled },
        [CanteenOrderStatus.Confirmed]  = new[] { CanteenOrderStatus.Preparing, CanteenOrderStatus.Cancelled },
        [CanteenOrderStatus.Preparing]  = new[] { CanteenOrderStatus.Ready, CanteenOrderStatus.Cancelled },
        [CanteenOrderStatus.Ready]      = new[] { CanteenOrderStatus.Delivered, CanteenOrderStatus.Cancelled },
        [CanteenOrderStatus.Delivered]  = new[] { CanteenOrderStatus.Completed, CanteenOrderStatus.Refunded },
        [CanteenOrderStatus.Cancelled]  = new[] { CanteenOrderStatus.Refunded },
        [CanteenOrderStatus.Completed]  = Array.Empty<CanteenOrderStatus>(),
        [CanteenOrderStatus.Refunded]   = Array.Empty<CanteenOrderStatus>()
    };

    public static bool CanTransition(CanteenOrderStatus from, CanteenOrderStatus to)
        => _allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    public static void EnsureCanTransition(CanteenOrderStatus from, CanteenOrderStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Illegal order status transition from {from} to {to}.");
        }
    }

    public static bool IsTerminal(CanteenOrderStatus status)
        => status is CanteenOrderStatus.Completed or CanteenOrderStatus.Refunded;
}

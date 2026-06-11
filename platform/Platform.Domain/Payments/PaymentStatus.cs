namespace Platform.Domain.Payments;

/// <summary>
/// FLOW
///   Initiated -> Pending  (user redirected to gateway)
///   Pending   -> Succeeded (callback verified)
///   Pending   -> Failed    (callback rejected / timeout)
///   Succeeded -> Refunded  (operator-initiated refund)
/// </summary>
public enum PaymentStatus
{
    Initiated = 1,
    Pending = 2,
    Succeeded = 3,
    Failed = 4,
    Cancelled = 5,
    Refunded = 6
}

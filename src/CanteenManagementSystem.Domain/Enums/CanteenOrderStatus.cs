namespace CanteenManagementSystem.Domain.Enums;

public enum CanteenOrderStatus
{
    Pending = 0,
    Delivered = 1,
    Cancelled = 2,

    Placed = 10,
    Confirmed = 20,
    Preparing = 30,
    Ready = 40,
    Completed = 50,
    Refunded = 60
}

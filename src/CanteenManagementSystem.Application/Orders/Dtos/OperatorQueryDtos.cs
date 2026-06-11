namespace CanteenManagementSystem.Application.Orders.Dtos;

public class OperatorDashboardStatsDto
{
    public int TodayOrders { get; set; }
    public int PendingOrders { get; set; }
    public int DeliveredOrders { get; set; }
    public decimal TodayRevenue { get; set; }
}

public class PagedOrdersResultDto
{
    public List<OperatorOrderViewModel> Orders { get; set; } = new();
    public bool IsToday { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalOrders { get; set; }
}

public class OrderStatusUpdateResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

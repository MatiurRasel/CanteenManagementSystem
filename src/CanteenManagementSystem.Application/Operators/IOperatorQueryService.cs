using CanteenManagementSystem.Application.Orders.Dtos;
using CanteenManagementSystem.Domain.Orders;

namespace CanteenManagementSystem.Application.Operators;

public interface IOperatorQueryService
{
    /// <summary>End-of-shift summary for the counter operator (totals, top items, cancelled list).</summary>
    Task<ShiftSummaryDto> GetShiftSummaryAsync(DateTime date, CancellationToken cancellationToken = default);

    Task<OperatorDashboardStatsDto> GetDashboardStatsAsync(DateTime? selectedDate);
    Task<PagedOrdersResultDto> GetPendingOrdersAsync(DateTime? selectedDate, int page, int pageSize);
    Task<PagedOrdersResultDto> SearchAllOrdersAsync(string search, DateTime? selectedDate, int page, int pageSize);
    Task<PagedOrdersResultDto> SearchOrdersAsync(string search, DateTime? selectedDate, int page, int pageSize);
    Task<List<OperatorOrderViewModel>> GetOrdersWithUserDetailsAsync(List<Order> orders, bool includePhoto = true);
    Task<List<Order>> GetHistoryOrdersAsync(DateTime? date);
    Task<object> GetStatisticsAsync(DateTime? selectedDate);
    Task<List<Order>> GetOrdersAsync(int status = -1);
    Task<OrderStatusUpdateResultDto> UpdateOrderStatusAsync(int orderId, int status);
    Task<object> GetLiveOrdersAsync();
    Task<OrderStatusUpdateResultDto> MarkAsDeliveredAsync(int orderId);
}

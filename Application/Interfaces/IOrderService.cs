using CanteenManagementSystem.Application.DTOs;

namespace CanteenManagementSystem.Application.Interfaces
{
    public interface IOrderService
    {
        Task<OrderDto> PlaceOrderAsync(PlaceOrderRequest request, Guid? userId = null);
        Task<OrderDto> GetOrderAsync(Guid orderId);
        Task<List<OrderDto>> GetUserOrdersAsync(Guid userId);
        Task<bool> CancelOrderAsync(Guid orderId, Guid userId);
        Task<bool> ConfirmDeliveryAsync(Guid orderId, string nfcCardNumber);
        Task<bool> MarkOrderReadyAsync(Guid orderId, Guid operatorId);
        Task<List<OrderDto>> GetPendingOrdersAsync(Guid clientId);
    }
}


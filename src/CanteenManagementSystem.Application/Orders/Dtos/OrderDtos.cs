using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Application.Orders.Dtos;

public class PlaceOrderRequestDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserIdentifier { get; set; } = string.Empty;
    public CanteenUserType UserType { get; set; }
    public List<OrderItemRequestDto> Items { get; set; } = new();
    public string? IdempotencyKey { get; set; }
}

public class OrderItemRequestDto
{
    public int FoodItemId { get; set; }
    public int DailyMenuId { get; set; }
    public int Quantity { get; set; }
}

public class OrderResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal RemainingBalance { get; set; }
}

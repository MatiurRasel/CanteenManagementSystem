namespace CanteenManagementSystem.Models.ViewModels
{
    public class PlaceOrderRequest
    {
        public string UserId { get; set; }
        public string UserIdentifier { get; set; }
        public CanteenUserType UserType { get; set; } 
        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public class OrderItemRequest
    {
        public int FoodItemId { get; set; }
        public int DailyMenuId { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal RemainingBalance { get; set; }
    }
}

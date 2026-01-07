namespace CanteenManagementSystem.Application.DTOs
{
    public class OrderDto
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string TokenNumber { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? DeliveryTimeSlot { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
    }

    public class OrderItemDto
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public List<CustomizationDto> Customizations { get; set; } = new();
    }

    public class CustomizationDto
    {
        public string VariantName { get; set; } = string.Empty;
        public string Option { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class PlaceOrderRequest
    {
        public Guid? UserId { get; set; }
        public string OrderType { get; set; } = "INSTANT";
        public DateTime? DeliveryTimeSlot { get; set; }
        public string? TableNumber { get; set; }
        public string? SpecialInstructions { get; set; }
        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public class OrderItemRequest
    {
        public Guid ItemId { get; set; }
        public int Quantity { get; set; }
        public List<CustomizationDto> Customizations { get; set; } = new();
        public string? SpecialInstructions { get; set; }
    }
}


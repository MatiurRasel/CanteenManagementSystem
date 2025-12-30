namespace CanteenManagementSystem.Models.ViewModels
{
    public class OperatorOrderViewModel
    {
        public int OrderID { get; set; }
        public string OrderNumber { get; set; }
        public CanteenUserType UserType { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserIdentifier { get; set; } = string.Empty;
        public string UserPhotoUrl { get; set; } = string.Empty;
        public string UserMobileNo { get; set; } = string.Empty;
        public string UserGender { get; set; } = string.Empty;
        public string AcademicInformation { get; set; } = string.Empty;
        public string EmployeeTypeName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public CanteenOrderStatus Status { get; set; }
        public string OrderTime { get; set; } = string.Empty;
        public DateTime OrderDateTime { get; set; }
        public List<OrderItemDetail> Items { get; set; } = new();
    }

    public class OrderItemDetail
    {
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal FoodItemPrice { get; set; }
    }
}

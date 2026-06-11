using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Application.Verifications.Dtos;

public class VerifyRequestDto
{
    public string Identifier { get; set; } = string.Empty;
}

public class VerificationViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public CanteenUserType UserType { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserIdentifier { get; set; } = string.Empty;
    public string AcademicInformation { get; set; } = string.Empty;
    public string EmployeeTypeName { get; set; } = string.Empty;
    public string UserPhotoUrl { get; set; } = string.Empty;
    public string UserMobileNo { get; set; } = string.Empty;
    public string UserGender { get; set; } = string.Empty;
    public decimal TotalBalance { get; set; }
    public decimal UsedBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public List<MenuItemViewModel> TodayMenu { get; set; } = new();
}

public class MenuItemViewModel
{
    public int DailyMenuID { get; set; }
    public int FoodItemID { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public CanteenMealType? Category { get; set; }
    public int AvailableQuantity { get; set; }
    public bool InStock { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}

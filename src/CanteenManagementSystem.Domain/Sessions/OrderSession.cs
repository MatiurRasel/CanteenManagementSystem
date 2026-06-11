using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Domain.Sessions;

/// In-memory ordering session for keypad / NFC flows. Persisted only for the
/// duration of one user interaction; never written to the database directly.
public class OrderSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserPhoto { get; set; } = string.Empty;
    public string UserInfo { get; set; } = string.Empty;
    public CanteenUserType UserType { get; set; }
    public decimal AvailableBalance { get; set; }
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime LastActivity { get; set; } = DateTime.Now;
    public int TimeoutSeconds { get; set; } = 30;

    public string InputSequence { get; set; } = string.Empty;
    public List<OrderSessionItem> Items { get; set; } = new();
    public int CurrentPage { get; set; } = 1;

    public decimal TotalAmount => Items.Sum(i => i.TotalPrice);
    public bool IsExpired => (DateTime.Now - LastActivity).TotalSeconds > TimeoutSeconds;
    public int RemainingSeconds => Math.Max(0, TimeoutSeconds - (int)(DateTime.Now - LastActivity).TotalSeconds);
}

public class OrderSessionItem
{
    public int DailyMenuID { get; set; }
    public int FoodItemID { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int ItemNumber { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => UnitPrice * Quantity;
}

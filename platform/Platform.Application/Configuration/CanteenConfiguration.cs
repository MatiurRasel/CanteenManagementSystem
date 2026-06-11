namespace Platform.Application.Configuration;

public class CanteenConfiguration
{
    public decimal DefaultMonthlyLimit { get; set; } = 2000;
    public decimal MaxMonthlyLimit { get; set; } = 2000;
    public int StudentLimitPercentage { get; set; } = 100;
    public int TeacherLimitPercentage { get; set; } = 100;
    public int StaffLimitPercentage { get; set; } = 100;

    public int OrderSessionTimeout { get; set; } = 30;
    public int IdleScreenTimeout { get; set; } = 10;
    public int SuccessScreenDuration { get; set; } = 2;

    public int ItemsPerPage { get; set; } = 10;
    public bool ShowPricesOnDisplay { get; set; } = true;
    public bool ShowStockQuantity { get; set; } = true;

    /// <summary>
    /// Show calorie counts and allergen chips on the TV menu board + phone preview.
    /// Default true; set false from appsettings/tenant settings when the canteen
    /// doesn't want nutritional info shown (e.g. partial data only entered).
    /// </summary>
    public bool ShowNutritionOnDisplay { get; set; } = true;

    public bool EnableTouchKeypad { get; set; } = true;
    public bool EnableUSBKeypad { get; set; } = true;
    public string KeypadNextPage { get; set; } = "#";
    public string KeypadPrevPage { get; set; } = "*";
    public string KeypadItemZero { get; set; } = "0";

    public bool RequireConfirmation { get; set; } = true;

    public int DisplayRefreshInterval { get; set; } = 5000;
    public bool MenuAutoRefresh { get; set; } = true;

    public int ReadyOrderAutoCancelMinutes { get; set; } = 30;
}

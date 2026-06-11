namespace Platform.Application.Configuration;

public class BrandingConfiguration
{
    public string AppName { get; set; } = "Canteen Management System";
    public string AppSubtitle { get; set; } = "Smart canteen operations";

    public string AccentColor { get; set; } = "#10B981";
    public string SidebarColor { get; set; } = "#111827";
    public string TopbarColor { get; set; } = "#FFFFFF";
    public string MenuActiveColor { get; set; } = "#10B981";
    public string MenuTextColor { get; set; } = "#E5E7EB";

    public string SuccessColor { get; set; } = "#10B981";
    public string WarningColor { get; set; } = "#F59E0B";
    public string ErrorColor { get; set; } = "#EF4444";
    public string InfoColor { get; set; } = "#3B82F6";
}

// =============================================================================
// UiOptions — option models for the shadcn-style UX partials.
// =============================================================================

using Microsoft.AspNetCore.Html;

namespace CanteenManagementSystem.Presentation.Models;

public sealed class UiPageHeaderOptions
{
    public string  Title    { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public IHtmlContent? Actions { get; set; }
}

public enum UiKpiTrend { None = 0, Up = 1, Down = 2 }

public sealed class UiKpiOptions
{
    public string  Label { get; set; } = string.Empty;
    public string  Value { get; set; } = "—";

    /// <summary>
    /// Numeric change vs prior period (e.g. "+12% vs yesterday"). Rendered with
    /// an up/down arrow according to Trend.
    /// </summary>
    public string? Delta { get; set; }

    /// <summary>
    /// Plain contextual note shown below the value (e.g. "across all tenants",
    /// "avg ৳ 280 / order"). Rendered without an arrow, in muted colour.
    /// If both Delta and Hint are set, Delta wins.
    /// </summary>
    public string? Hint { get; set; }

    public UiKpiTrend Trend { get; set; } = UiKpiTrend.None;
    public string? Icon  { get; set; }
}

public enum UiBadgeVariant { Primary = 0, Secondary = 1, Success = 2, Warning = 3, Danger = 4, Info = 5, Dark = 6 }

public sealed class UiBadgeOptions
{
    public string  Text    { get; set; } = string.Empty;
    public UiBadgeVariant Variant { get; set; } = UiBadgeVariant.Secondary;
    public bool    WithDot { get; set; }
    public string? Icon    { get; set; }
}

public enum UiAlertVariant { Info = 0, Success = 1, Warning = 2, Danger = 3, Primary = 4 }

public sealed class UiAlertOptions
{
    public UiAlertVariant Variant { get; set; } = UiAlertVariant.Info;
    public string? Heading { get; set; }
    public string  Body    { get; set; } = string.Empty;
    public string? Icon    { get; set; }
}

public sealed class UiInputOptions
{
    public string  Id          { get; set; } = "input-" + Guid.NewGuid().ToString("N").Substring(0, 6);
    public string  Name        { get; set; } = string.Empty;
    public string? Label       { get; set; }
    public string  InputType   { get; set; } = "text";
    public string? Value       { get; set; }
    public string? Placeholder { get; set; }
    public string? HelpText    { get; set; }
    public string? Autocomplete{ get; set; }
    public bool    Required    { get; set; }
    public bool    ReadOnly    { get; set; }
    public bool    HasError    => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string? ErrorMessage{ get; set; }
}

public sealed class UiTab
{
    public UiTab() { }
    public UiTab(string label, string url, bool isActive = false, string? icon = null, int badgeCount = 0)
    {
        Label = label; Url = url; IsActive = isActive; Icon = icon; BadgeCount = badgeCount;
    }
    public string  Label { get; set; } = string.Empty;
    public string  Url   { get; set; } = "#";
    public bool    IsActive { get; set; }
    public string? Icon  { get; set; }
    public int     BadgeCount { get; set; }
}

public sealed class UiTabsOptions
{
    public IReadOnlyList<UiTab> Tabs { get; set; } = Array.Empty<UiTab>();
    public string? AriaLabel { get; set; }
}

public enum UiButtonVariant
{
    Primary = 0, Secondary = 1, OutlinePrimary = 2, OutlineSecondary = 3,
    Ghost = 4, Danger = 5, Success = 6
}
public enum UiButtonSize { Default = 0, Small = 1, Large = 2 }

public sealed class UiButtonOptions
{
    public string  Text     { get; set; } = "";
    public string  Type     { get; set; } = "button";
    public string? Href     { get; set; }
    public string? Icon     { get; set; }
    public UiButtonVariant Variant { get; set; } = UiButtonVariant.Secondary;
    public UiButtonSize    Size    { get; set; } = UiButtonSize.Default;
    public bool    Disabled { get; set; }
}

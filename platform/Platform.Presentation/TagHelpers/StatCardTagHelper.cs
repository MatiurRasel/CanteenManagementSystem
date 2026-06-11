using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Platform.Presentation.TagHelpers;

/// <stat-card label="Pending" value="12" icon="fa-clock" tone="warning" />
/// Renders a Sneat-styled KPI card with a coloured accent bar.
[HtmlTargetElement("stat-card")]
public sealed class StatCardTagHelper : TagHelper
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Icon { get; set; } = "fa-chart-bar";
    public string Tone { get; set; } = "primary";
    public string? Trend { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "card stat-card h-100 shadow-sm");

        var trendBlock = string.IsNullOrEmpty(Trend) ? string.Empty
            : $"<small class=\"text-muted d-block mt-1\">{Trend}</small>";

        output.Content.SetHtmlContent(
            $@"<div class=""card-body d-flex align-items-center gap-3"">
                  <span class=""stat-card-icon bg-label-{Tone} rounded-3 d-inline-flex align-items-center justify-content-center"" style=""width:3rem;height:3rem;font-size:1.25rem;"">
                      <i class=""fa-solid {Icon}""></i>
                  </span>
                  <div class=""flex-grow-1 lh-sm"">
                      <div class=""text-muted small fw-semibold text-uppercase"">{Label}</div>
                      <div class=""fs-3 fw-bold text-body"">{Value}</div>
                      {trendBlock}
                  </div>
              </div>");
    }
}

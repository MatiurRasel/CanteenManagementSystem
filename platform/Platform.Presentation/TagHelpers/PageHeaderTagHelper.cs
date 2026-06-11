using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Platform.Presentation.TagHelpers;

/// <page-header title="Dashboard" subtitle="Operator overview" icon="fa-chart-line">
///   <a class="btn btn-primary" href="/menu">New</a>
/// </page-header>
[HtmlTargetElement("page-header")]
public sealed class PageHeaderTagHelper : TagHelper
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Icon { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var inner = await output.GetChildContentAsync();
        var iconBlock = string.IsNullOrEmpty(Icon)
            ? string.Empty
            : $@"<span class=""page-header-icon bg-label-primary rounded-3 d-inline-flex align-items-center justify-content-center me-3"" style=""width:2.75rem;height:2.75rem;""><i class=""fa-solid {Icon}""></i></span>";

        var subtitleBlock = string.IsNullOrEmpty(Subtitle)
            ? string.Empty
            : $"<div class=\"text-muted small\">{Subtitle}</div>";

        output.TagName = "div";
        output.Attributes.SetAttribute("class", "page-header d-flex flex-column flex-md-row gap-3 align-items-md-center justify-content-between mb-4");
        output.Content.SetHtmlContent($@"
            <div class=""d-flex align-items-center"">
                {iconBlock}
                <div>
                    <h4 class=""mb-0 fw-bold"">{Title}</h4>
                    {subtitleBlock}
                </div>
            </div>
            <div class=""d-flex gap-2 flex-wrap"">{inner.GetContent()}</div>");
    }
}

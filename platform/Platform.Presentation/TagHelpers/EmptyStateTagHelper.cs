using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Platform.Presentation.TagHelpers;

/// <empty-state title="No orders yet" message="When orders arrive, they appear here." icon="fa-inbox" />
[HtmlTargetElement("empty-state")]
public sealed class EmptyStateTagHelper : TagHelper
{
    public string Title { get; set; } = "Nothing to show";
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = "fa-inbox";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var inner = await output.GetChildContentAsync();
        var actions = string.IsNullOrWhiteSpace(inner.GetContent()) ? string.Empty
            : $"<div class=\"mt-3\">{inner.GetContent()}</div>";

        output.TagName = "div";
        output.Attributes.SetAttribute("class", "empty-state text-center py-5 px-3 text-muted");
        output.Content.SetHtmlContent($@"
            <div class=""empty-state-icon mb-3 d-inline-flex align-items-center justify-content-center rounded-circle bg-label-secondary"" style=""width:3.5rem;height:3.5rem;"">
                <i class=""fa-solid {Icon} fa-lg""></i>
            </div>
            <h6 class=""mb-1 text-body"">{Title}</h6>
            <div class=""small"">{Message}</div>
            {actions}");
    }
}

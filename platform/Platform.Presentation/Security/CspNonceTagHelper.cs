// =============================================================================
// CspNonceTagHelper  (Platform.Presentation.Security)
// -----------------------------------------------------------------------------
// Auto-injects the per-request CSP nonce onto every <script> and <style> tag
// in Razor output. Modern browsers will then accept these inline blocks per
// CSP3 §6.7.1 (script-src 'nonce-xxxxx').
//
// HOW IT IS USED
//   Add to _ViewImports.cshtml:
//       @addTagHelper *, Platform.Presentation
//   …then every <script> and <style> in your views gets nonce="..."
//   automatically. Authors do nothing.
//
// SAFETY
//   We only set the attribute when it is NOT already present, so a developer
//   can intentionally omit a nonce on a tag (e.g. a stub during testing).
//
// PERFORMANCE
//   One HttpContext.Items lookup + one attribute write per script/style tag.
//   Negligible vs Razor rendering cost.
// =============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Platform.Presentation.Middleware;

namespace Platform.Presentation.Security;

[HtmlTargetElement("script")]
[HtmlTargetElement("style")]
public sealed class CspNonceTagHelper : TagHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CspNonceTagHelper(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>Run AFTER all other tag helpers so attributes are stable.</summary>
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (output.Attributes.ContainsName("nonce"))
        {
            return; // honour an explicit nonce attribute set by the author
        }

        var http = _httpContextAccessor.HttpContext;
        if (http is null)
        {
            return;
        }

        if (http.Items.TryGetValue(SecurityHeadersMiddleware.CspNonceItemKey, out var raw)
            && raw is string nonce
            && !string.IsNullOrEmpty(nonce))
        {
            output.Attributes.SetAttribute("nonce", nonce);
        }
    }
}

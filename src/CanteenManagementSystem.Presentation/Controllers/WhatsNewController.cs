// =============================================================================
// WhatsNewController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Surfaces a static changelog at /whats-new. Backed by `docs/Changelog.md` (or
// a fallback hard-coded list when the file is missing). The topbar Bell icon
// links here when there are entries newer than the per-user
// `whatsNewSeenAt` localStorage timestamp.
// =============================================================================

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize]
[Route("whats-new")]
public sealed class WhatsNewController : Controller
{
    private readonly IWebHostEnvironment _env;
    public WhatsNewController(IWebHostEnvironment env) { _env = env; }

    public sealed record ChangelogEntry(DateOnly Date, string Title, IReadOnlyList<string> Bullets);
    public sealed record WhatsNewVm(IReadOnlyList<ChangelogEntry> Entries);

    [HttpGet("")]
    public IActionResult Index()
    {
        var entries = LoadChangelog();
        return View(new WhatsNewVm(entries));
    }

    private IReadOnlyList<ChangelogEntry> LoadChangelog()
    {
        var path = Path.Combine(_env.ContentRootPath, "..", "..", "docs", "Changelog.md");
        if (!System.IO.File.Exists(path))
        {
            // Sensible defaults so the page is never empty.
            return new[]
            {
                new ChangelogEntry(new DateOnly(2026, 6, 5),
                    "Big roadmap-completion sweep",
                    new[]
                    {
                        "10 brand-new features shipped (IP allow-list, favicon-per-tenant, keyboard shortcuts, etc.)",
                        "Counter screens now subscribe to live wallet-balance + stock-out events.",
                        "GDPR data export at /admin/tenancy/export.",
                        "Feature flags admin at /admin/feature-flags."
                    }),
                new ChangelogEntry(new DateOnly(2026, 6, 5),
                    "Loading + skeleton system",
                    new[]
                    {
                        "Drop-in <partial name=\"_Skeleton\" /> with 10 variants.",
                        "CanteenLoading JS helper auto-binds form submits + button spinners.",
                        "Dark-mode + prefers-reduced-motion aware."
                    })
            };
        }

        var text = System.IO.File.ReadAllText(path);
        var list = new List<ChangelogEntry>();
        var rx = new Regex(@"^##\s+(?<date>\d{4}-\d{2}-\d{2})\s+(?<title>.+)$", RegexOptions.Multiline);
        var matches = rx.Matches(text);
        for (int i = 0; i < matches.Count; i++)
        {
            var date = DateOnly.Parse(matches[i].Groups["date"].Value);
            var title = matches[i].Groups["title"].Value.Trim();
            var start = matches[i].Index + matches[i].Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            var section = text.Substring(start, end - start);
            var bullets = section
                .Split('\n')
                .Select(l => l.TrimStart())
                .Where(l => l.StartsWith("* ") || l.StartsWith("- "))
                .Select(l => l[2..].Trim())
                .ToList();
            list.Add(new ChangelogEntry(date, title, bullets));
        }
        return list;
    }
}

// =============================================================================
// Shared option models for the reusable UX partials in Views/Shared/.
//
//   _Skeleton.cshtml       → SkeletonOptions
//   _EmptyState.cshtml     → EmptyStateOptions
//   _Spinner.cshtml        → SpinnerOptions
//   _LoadingOverlay.cshtml → LoadingOverlayOptions
//
// Razor @model binding can't define types that are bound in the SAME file,
// so model types live here as proper C#.
//
// See `/wwwroot/css/canteen-loading.css` + `/wwwroot/js/canteen-loading.js`
// for the runtime that backs the partials.
// =============================================================================

namespace CanteenManagementSystem.Presentation.Models;

/// <summary>
/// Skeleton variants. Pick the one that mirrors the page structure that's
/// about to load — Card for grid pages, Table for rows, Kpi for stat tiles,
/// Form for input groups, List for avatar+two-line items, Chart for graphs,
/// Detail for an article-shaped page, Avatar for a single profile row.
/// Line is the catch-all (N stacked blocks).
/// </summary>
public enum SkeletonVariant
{
    Line,
    Table,
    Card,
    Kpi,
    List,
    Form,
    Detail,
    Chart,
    Avatar,
    Stat
}

public sealed class SkeletonOptions
{
    public SkeletonVariant Variant { get; set; } = SkeletonVariant.Line;

    /// <summary>How many repeating units to render. Default 4.</summary>
    public int Rows { get; set; } = 4;

    /// <summary>Optional aria-label that screen readers announce.</summary>
    public string? Label { get; set; }

    /// <summary>Render inside a card wrapper (panel + border). Defaults true for most variants.</summary>
    public bool Card { get; set; } = true;

    /// <summary>Tighter spacing / smaller blocks — good for inline use inside cards.</summary>
    public bool Compact { get; set; }
}

public sealed class EmptyStateOptions
{
    public string  Icon        { get; set; } = "fa-inbox";
    public string  Title       { get; set; } = "Nothing here yet";
    public string? Description { get; set; }
    public string? CtaLabel    { get; set; }
    public string? CtaUrl      { get; set; }
    public string? CtaIcon     { get; set; }
}

public enum SpinnerSize { Small, Medium, Large }

public sealed class SpinnerOptions
{
    public SpinnerSize Size  { get; set; } = SpinnerSize.Medium;

    /// <summary>Optional text shown next to the spinner.</summary>
    public string? Text { get; set; }

    /// <summary>If true, the spinner sits inline with the surrounding text.</summary>
    public bool Inline { get; set; }

    public string? AriaLabel { get; set; }
}

public sealed class LoadingOverlayOptions
{
    /// <summary>Visible message inside the curtain. Optional.</summary>
    public string? Text { get; set; } = "Loading…";

    /// <summary>
    /// If true, overlay sits at the document level (covers the viewport).
    /// If false, it scopes to its closest positioned ancestor — typically a card.
    /// </summary>
    public bool Fixed { get; set; }

    /// <summary>Start active. Defaults false so the page can toggle it via JS.</summary>
    public bool Active { get; set; }

    /// <summary>Optional id so JS can find it: <c>CanteenLoading.show('#myOverlay')</c>.</summary>
    public string? Id { get; set; }
}

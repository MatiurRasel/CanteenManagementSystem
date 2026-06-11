// =============================================================================
// TemplateRenderer  (Infrastructure.Notifications)
// -----------------------------------------------------------------------------
// Tiny string-token renderer. Pattern: {a.b.c} replaced with the deeply-nested
// value from the tokens object. Anonymous types, dictionaries, and POCOs are
// all supported. Intentionally NOT using a heavyweight templating engine —
// notification bodies should stay short and free of conditional logic.
// =============================================================================

using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Platform.Infrastructure.Notifications;

internal static class TemplateRenderer
{
    private static readonly Regex Token = new(@"\{([\w\.]+)\}", RegexOptions.Compiled);

    public static string Render(string template, object? tokens)
    {
        if (string.IsNullOrEmpty(template) || tokens is null) return template ?? string.Empty;
        return Token.Replace(template, match =>
        {
            var path = match.Groups[1].Value;
            var value = Resolve(tokens, path);
            return value?.ToString() ?? string.Empty;
        });
    }

    private static object? Resolve(object? root, string path)
    {
        if (root is null) return null;
        var current = root;
        foreach (var segment in path.Split('.'))
        {
            current = ResolveSegment(current, segment);
            if (current is null) return null;
        }
        return current;
    }

    private static object? ResolveSegment(object? source, string segment)
    {
        if (source is null) return null;
        if (source is IDictionary dict)
        {
            return dict.Contains(segment) ? dict[segment] : null;
        }
        var prop = source.GetType().GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop?.GetValue(source);
    }
}

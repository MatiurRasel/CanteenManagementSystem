// =============================================================================
// ResourceKeysGuardTests — static localization guards (no web host needed).
//
//   1. Every L["Key"] referenced from a Razor view must exist in
//      Resources/SharedResource.resx — a missing key silently renders the raw
//      key name to end users, so this fails the build instead.
//   2. SharedResource.bn-BD.resx must contain every key the English file has
//      (run tools/BilingualBaseline after adding keys to restore parity).
// =============================================================================

using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace CanteenManagementSystem.IntegrationTests;

public sealed class ResourceKeysGuardTests
{
    private static readonly Regex LocalizerKey = new(
        """L\[\s*"(?<key>[^"]+)"\s*[,\]]""",
        RegexOptions.Compiled);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CanteenManagementSystem.sln")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test must run from somewhere inside the repository");
        return dir!.FullName;
    }

    private static string PresentationRoot() =>
        Path.Combine(RepoRoot(), "src", "CanteenManagementSystem.Presentation");

    private static HashSet<string> ResxKeys(string resxPath) =>
        XDocument.Load(resxPath)
            .Root!
            .Elements("data")
            .Select(d => d.Attribute("name")!.Value)
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void EveryLocalizerKeyInViews_ExistsInSharedResource()
    {
        var presentation = PresentationRoot();
        var resx = Path.Combine(presentation, "Resources", "SharedResource.resx");
        var known = ResxKeys(resx);

        var missing = new List<string>();
        foreach (var view in Directory.EnumerateFiles(
                     Path.Combine(presentation, "Views"), "*.cshtml", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(view);
            foreach (Match match in LocalizerKey.Matches(content))
            {
                var key = match.Groups["key"].Value;
                if (!known.Contains(key))
                {
                    missing.Add($"{Path.GetRelativePath(presentation, view)}: {key}");
                }
            }
        }

        missing.Should().BeEmpty(
            "every L[\"…\"] key used by a view must exist in Resources/SharedResource.resx " +
            "(add the key, then run tools/BilingualBaseline for bn-BD parity)");
    }

    [Fact]
    public void BengaliResource_HasParityWithEnglish()
    {
        var resources = Path.Combine(PresentationRoot(), "Resources");
        var english = ResxKeys(Path.Combine(resources, "SharedResource.resx"));
        var bengali = ResxKeys(Path.Combine(resources, "SharedResource.bn-BD.resx"));

        english.Except(bengali).Should().BeEmpty(
            "bn-BD must contain every English key — run tools/BilingualBaseline to restore parity");
    }
}

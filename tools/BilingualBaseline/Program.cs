// =============================================================================
// BilingualBaseline  (tools/BilingualBaseline)
// -----------------------------------------------------------------------------
// One-shot CLI that walks the English SharedResource.resx, finds every key
// that's missing from SharedResource.bn-BD.resx, and appends the English
// value as a starting baseline. Untranslated UI then silently falls through
// to English instead of returning the raw key.
//
//   dotnet run --project tools/BilingualBaseline -- \
//       --en   <path>/SharedResource.resx \
//       --tgt  <path>/SharedResource.bn-BD.resx
//
// Idempotent: running it twice doesn't duplicate entries.
// =============================================================================

using System.Xml.Linq;

string? enPath  = null, tgtPath = null;
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--en")  enPath  = args[++i];
    if (args[i] == "--tgt") tgtPath = args[++i];
}
if (enPath is null || tgtPath is null)
{
    Console.Error.WriteLine("Usage: dotnet run -- --en <path>/SharedResource.resx --tgt <path>/SharedResource.bn-BD.resx");
    return 1;
}

if (!File.Exists(enPath))
{
    Console.Error.WriteLine($"Source EN resx not found: {enPath}");
    return 1;
}
if (!File.Exists(tgtPath))
{
    Console.Error.WriteLine($"Target resx not found: {tgtPath}");
    return 1;
}

var en  = XDocument.Load(enPath);
var tgt = XDocument.Load(tgtPath);
var enRoot  = en.Root  ?? throw new InvalidOperationException("No root in en resx.");
var tgtRoot = tgt.Root ?? throw new InvalidOperationException("No root in tgt resx.");

var tgtNames = tgtRoot.Elements("data")
    .Select(d => d.Attribute("name")?.Value)
    .Where(n => !string.IsNullOrEmpty(n))
    .ToHashSet(StringComparer.Ordinal);

var added = 0;
foreach (var data in enRoot.Elements("data"))
{
    var name  = data.Attribute("name")?.Value;
    if (string.IsNullOrEmpty(name) || tgtNames.Contains(name)) continue;
    // Clone the EN <data> element with the same value as the EN baseline so
    // untranslated UI falls through to English visually until a translator
    // overrides it.
    var clone = new XElement(data);
    tgtRoot.Add(clone);
    added++;
}

tgt.Save(tgtPath);
Console.WriteLine($"Bilingual baseline: added {added} key(s) to {tgtPath}.");
return 0;

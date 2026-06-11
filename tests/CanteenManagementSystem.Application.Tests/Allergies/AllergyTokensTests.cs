// =============================================================================
// AllergyTokensTests
// -----------------------------------------------------------------------------
// Locks down the comma-CSV allergen vocabulary shared between FoodItem
// and Student / Employee. The token splitter is the bottleneck — break it
// and the counter shows false positives / silently misses real allergies.
// =============================================================================

using CanteenManagementSystem.Application.Allergies;
using FluentAssertions;
using Xunit;

namespace CanteenManagementSystem.Application.Tests.Allergies;

public class AllergyTokensTests
{
    [Fact]
    public void Split_returns_empty_for_null_or_whitespace()
    {
        AllergyTokens.Split(null).Should().BeEmpty();
        AllergyTokens.Split("").Should().BeEmpty();
        AllergyTokens.Split("   ").Should().BeEmpty();
    }

    [Fact]
    public void Split_trims_lowercases_and_dedupes()
    {
        var result = AllergyTokens.Split("Nuts, dairy , GLUTEN, nuts ,egg");
        result.Should().BeEquivalentTo(new[] { "nuts", "dairy", "gluten", "egg" });
    }

    [Fact]
    public void Intersect_finds_case_insensitive_matches()
    {
        var hits = AllergyTokens.Intersect("nuts, dairy", "NUTS, soy, EGG");
        hits.Should().BeEquivalentTo(new[] { "nuts" });
    }

    [Fact]
    public void Intersect_is_token_exact_not_substring()
    {
        // "egg" must NOT match "eggplant" — they're whole tokens, not substrings.
        var hits = AllergyTokens.Intersect("egg", "eggplant, tomato");
        hits.Should().BeEmpty();
    }

    [Fact]
    public void Intersect_returns_empty_when_either_side_is_empty()
    {
        AllergyTokens.Intersect(null, "nuts, dairy").Should().BeEmpty();
        AllergyTokens.Intersect("nuts, dairy", null).Should().BeEmpty();
        AllergyTokens.Intersect("", "nuts, dairy").Should().BeEmpty();
    }

    [Fact]
    public void Intersect_handles_multi_token_overlap()
    {
        var hits = AllergyTokens.Intersect("nuts, dairy, soy", "soy, gluten, nuts");
        hits.Should().BeEquivalentTo(new[] { "nuts", "soy" });
    }
}

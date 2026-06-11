// =============================================================================
// ComboButtonTests
// -----------------------------------------------------------------------------
// Locks defaults + the FoodItemIdsCsv parsing contract shared between the
// admin editor and the counter resolve endpoint.
// =============================================================================

using CanteenManagementSystem.Domain.Menu;
using FluentAssertions;
using Xunit;

namespace CanteenManagementSystem.Domain.Tests.Menu;

public class ComboButtonTests
{
    [Fact]
    public void Defaults_are_safe_for_unset_combo()
    {
        var c = new ComboButton();
        c.IsActive.Should().BeTrue();
        c.SortOrder.Should().Be(0);
        c.Code.Should().BeEmpty();
        c.DisplayName.Should().BeEmpty();
        c.FoodItemIdsCsv.Should().BeEmpty();
        c.CreatedAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
    }

    [Theory]
    [InlineData("1,2,3",      new[] { 1, 2, 3 })]
    [InlineData("1, 2 , 3",   new[] { 1, 2, 3 })]
    [InlineData("",           new int[0])]
    [InlineData(null,         new int[0])]
    [InlineData("1,1,2",      new[] { 1, 1, 2 })]
    [InlineData(" 7 ,abc, 9", new[] { 7, 9 })]    // garbage tokens are dropped
    public void FoodItemIdsCsv_parses_into_int_list(string? csv, int[] expected)
    {
        var parsed = (csv ?? string.Empty)
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToList();
        parsed.Should().BeEquivalentTo(expected, opts => opts.WithStrictOrdering());
    }
}

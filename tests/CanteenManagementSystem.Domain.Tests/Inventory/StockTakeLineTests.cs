// =============================================================================
// StockTakeLineTests
// -----------------------------------------------------------------------------
// Variance is a simple computed property today — these tests pin the
// expected behaviour so the next refactor doesn't silently break the audit
// report (Variance drives loss / overage analytics).
// =============================================================================

using CanteenManagementSystem.Domain.Inventory;
using FluentAssertions;
using Xunit;

namespace CanteenManagementSystem.Domain.Tests.Inventory;

public class StockTakeLineTests
{
    [Fact]
    public void Variance_is_zero_when_count_matches_expected()
    {
        var line = new StockTakeLine { ExpectedQty = 10, CountedQty = 10 };
        line.VarianceQty.Should().Be(0);
    }

    [Fact]
    public void Variance_is_positive_when_counted_more_than_expected()
    {
        var line = new StockTakeLine { ExpectedQty = 10, CountedQty = 12 };
        line.VarianceQty.Should().Be(2);
    }

    [Fact]
    public void Variance_is_negative_when_counted_less_than_expected()
    {
        var line = new StockTakeLine { ExpectedQty = 10, CountedQty = 7 };
        line.VarianceQty.Should().Be(-3);
    }

    [Fact]
    public void Default_counted_is_zero_so_variance_is_negative_expected()
    {
        var line = new StockTakeLine { ExpectedQty = 8 };
        line.CountedQty.Should().Be(0);
        line.VarianceQty.Should().Be(-8);
    }
}

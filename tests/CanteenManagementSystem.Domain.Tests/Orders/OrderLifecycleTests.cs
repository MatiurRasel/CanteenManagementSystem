// =============================================================================
// OrderLifecycleTests
// -----------------------------------------------------------------------------
// Pure-domain tests for the order state machine. No DB, no DI, no mocks —
// the whole point of putting OrderLifecycle in the Domain layer is that it
// is trivially testable.
//
// These are the seed tests the CI matrix should expand on. A fully-grown
// suite would also cover refund flows from each terminal state, race
// conditions on simultaneous transitions, and time-based auto-cancel.
// =============================================================================

using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using FluentAssertions;
using Xunit;

namespace CanteenManagementSystem.Domain.Tests.Orders;

public class OrderLifecycleTests
{
    [Theory]
    [InlineData(CanteenOrderStatus.Pending,    CanteenOrderStatus.Placed,    true)]
    [InlineData(CanteenOrderStatus.Pending,    CanteenOrderStatus.Cancelled, true)]
    [InlineData(CanteenOrderStatus.Placed,     CanteenOrderStatus.Confirmed, true)]
    [InlineData(CanteenOrderStatus.Confirmed,  CanteenOrderStatus.Preparing, true)]
    [InlineData(CanteenOrderStatus.Preparing,  CanteenOrderStatus.Ready,     true)]
    [InlineData(CanteenOrderStatus.Ready,      CanteenOrderStatus.Delivered, true)]
    [InlineData(CanteenOrderStatus.Delivered,  CanteenOrderStatus.Completed, true)]
    [InlineData(CanteenOrderStatus.Delivered,  CanteenOrderStatus.Refunded,  true)]
    public void Allowed_transitions_return_true(CanteenOrderStatus from, CanteenOrderStatus to, bool expected)
        => OrderLifecycle.CanTransition(from, to).Should().Be(expected);

    [Theory]
    [InlineData(CanteenOrderStatus.Pending,   CanteenOrderStatus.Delivered)]   // can't skip Placed/Confirmed/Preparing/Ready
    [InlineData(CanteenOrderStatus.Completed, CanteenOrderStatus.Placed)]      // terminal -> nothing
    [InlineData(CanteenOrderStatus.Refunded,  CanteenOrderStatus.Delivered)]   // terminal -> nothing
    [InlineData(CanteenOrderStatus.Cancelled, CanteenOrderStatus.Confirmed)]   // cancelled can't be resurrected
    public void Disallowed_transitions_return_false(CanteenOrderStatus from, CanteenOrderStatus to)
        => OrderLifecycle.CanTransition(from, to).Should().BeFalse();

    [Fact]
    public void Ensure_throws_with_explanatory_message_on_invalid_transition()
    {
        var act = () => OrderLifecycle.EnsureCanTransition(CanteenOrderStatus.Pending, CanteenOrderStatus.Delivered);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pending*Delivered*");
    }

    [Theory]
    [InlineData(CanteenOrderStatus.Completed)]
    [InlineData(CanteenOrderStatus.Refunded)]
    public void IsTerminal_recognises_end_states(CanteenOrderStatus status)
        => OrderLifecycle.IsTerminal(status).Should().BeTrue();

    [Theory]
    [InlineData(CanteenOrderStatus.Pending)]
    [InlineData(CanteenOrderStatus.Ready)]
    [InlineData(CanteenOrderStatus.Cancelled)]
    public void IsTerminal_rejects_intermediate_states(CanteenOrderStatus status)
        => OrderLifecycle.IsTerminal(status).Should().BeFalse();
}

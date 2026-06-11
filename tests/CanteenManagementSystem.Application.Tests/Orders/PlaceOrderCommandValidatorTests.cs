// =============================================================================
// PlaceOrderCommandValidatorTests
// -----------------------------------------------------------------------------
// Tests the FluentValidation rules used by the dispatcher's ValidationBehavior.
// Pure validation — no DB. Demonstrates the seam every other validator can
// follow.
// =============================================================================

using CanteenManagementSystem.Application.Orders.Commands;
using CanteenManagementSystem.Application.Orders.Dtos;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace CanteenManagementSystem.Application.Tests.Orders;

public class PlaceOrderCommandValidatorTests
{
    private readonly PlaceOrderCommandValidator _validator = new();

    [Fact]
    public void Empty_items_fails()
    {
        var cmd = new PlaceOrderCommand(new PlaceOrderRequestDto { UserId = "u1" });
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(c => c.Request.Items);
    }

    [Fact]
    public void Missing_user_fails()
    {
        var cmd = new PlaceOrderCommand(new PlaceOrderRequestDto
        {
            UserId = "",
            Items = new List<OrderItemRequestDto> { new() { DailyMenuId = 1, FoodItemId = 1, Quantity = 1 } }
        });
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.Request.UserId);
    }

    [Fact]
    public void Zero_quantity_fails()
    {
        var cmd = new PlaceOrderCommand(new PlaceOrderRequestDto
        {
            UserId = "u1",
            Items = new List<OrderItemRequestDto> { new() { DailyMenuId = 1, FoodItemId = 1, Quantity = 0 } }
        });
        var result = _validator.TestValidate(cmd);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Valid_request_passes()
    {
        var cmd = new PlaceOrderCommand(new PlaceOrderRequestDto
        {
            UserId = "u1",
            UserIdentifier = "u1",
            Items = new List<OrderItemRequestDto> { new() { DailyMenuId = 1, FoodItemId = 1, Quantity = 2 } }
        });
        _validator.TestValidate(cmd).IsValid.Should().BeTrue();
    }
}

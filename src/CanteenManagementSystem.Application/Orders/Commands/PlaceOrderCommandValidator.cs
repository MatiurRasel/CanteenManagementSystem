using FluentValidation;

namespace CanteenManagementSystem.Application.Orders.Commands;

public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(c => c.Request.UserId).NotEmpty();
        RuleFor(c => c.Request.Items).NotEmpty().WithMessage("Order must contain at least one item.");
        RuleForEach(c => c.Request.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.DailyMenuId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}

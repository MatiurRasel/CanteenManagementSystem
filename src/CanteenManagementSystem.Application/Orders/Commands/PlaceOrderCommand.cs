using Platform.Application.Dispatch;
using CanteenManagementSystem.Application.Orders.Dtos;

namespace CanteenManagementSystem.Application.Orders.Commands;

/// Source-of-truth-compliant order placement: block wallet first, reserve stock
/// first; deduction happens later in MarkOrderDeliveredCommand.
public sealed record PlaceOrderCommand(PlaceOrderRequestDto Request) : ICommand<OrderResponseDto>;

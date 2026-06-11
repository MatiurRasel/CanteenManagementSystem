using CanteenManagementSystem.Domain.Cards;
using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Application.Cards.Dtos;

public sealed record CardDto(
    int CardId,
    string CardUid,
    string UserId,
    CanteenUserType UserType,
    CardStatus Status,
    DateTime IssuedAtUtc,
    DateTime? ActivatedAtUtc,
    DateTime? BlockedAtUtc,
    string? Notes);

public sealed record CardOperationResult(bool Success, string Message, int CardId = 0);

public sealed class IssueCardRequest
{
    public string CardUid { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public CanteenUserType UserType { get; set; }
    public string? Notes { get; set; }
}

public sealed class BlockCardRequest
{
    public int CardId { get; set; }
    public string? Reason { get; set; }
}

public sealed class ReassignCardRequest
{
    public int CardId { get; set; }
    public string NewCardUid { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

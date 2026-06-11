// =============================================================================
// WalletController (API v1)  (CanteenManagementSystem.Presentation.Controllers.Api.V1)
// -----------------------------------------------------------------------------
// Per ADR 0004 the controller depends on IWalletService + a read-only repo
// (UserBalance lookup). No IAppDbContext. WalletService.RechargeAsync now
// commits internally via IUnitOfWork, so no SaveChanges from the controller.
// =============================================================================

using Asp.Versioning;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Wallets;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Wallets;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Presentation.Controllers.Api.V1;

/// Wallet operations: balance lookup + recharge + manual adjustment.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/wallets")]
[Produces("application/json")]
public sealed class WalletController : ControllerBase
{
    private readonly IReadOnlyRepository<UserBalance> _balances;
    private readonly IWalletService _wallet;

    public WalletController(IReadOnlyRepository<UserBalance> balances, IWalletService wallet)
    {
        _balances = balances;
        _wallet = wallet;
    }

    /// <summary>Balance snapshot for a user.</summary>
    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(WalletSnapshotDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<WalletSnapshotDto>> GetBalance(string userId, [FromQuery] CanteenUserType userType, CancellationToken cancellationToken)
    {
        var balance = await _balances.FirstOrDefaultAsync(b => b.UserId == userId && b.UserType == userType, cancellationToken);
        if (balance is null) return NotFound();
        return Ok(WalletSnapshotDto.From(balance));
    }

    /// <summary>Recharge a wallet from an internal operator action. Online gateway recharge goes through /payments instead.</summary>
    [HttpPost("{userId}/recharge")]
    public async Task<IActionResult> Recharge(string userId, [FromBody] RechargeRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _wallet.RechargeAsync(userId, request.UserType.ToString(), request.Amount, request.Source ?? "Manual recharge", cancellationToken);
        return result.IsSuccess ? Ok() : Conflict(new ProblemDetails { Title = "recharge_failed", Detail = result.Error.Message });
    }
}

public sealed record WalletSnapshotDto(
    string UserId,
    CanteenUserType UserType,
    decimal TotalBalance,
    decimal UsedBalance,
    decimal BlockedAmount,
    decimal AvailableBalance,
    decimal EmergencyEntitlement,
    decimal EmergencyAvailable,
    DateTime LastUpdated)
{
    public static WalletSnapshotDto From(UserBalance b) => new(
        b.UserId, b.UserType, b.TotalBalance, b.UsedBalance, b.BlockedAmount,
        b.AvailableBalance, b.EmergencyEntitlement, b.EmergencyAvailable, b.LastUpdated);
}

public sealed class RechargeRequestDto
{
    public CanteenUserType UserType { get; set; }
    public decimal Amount { get; set; }
    public string? Source { get; set; }
}

// =============================================================================
// LoyaltyRedeemController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Counter + kiosk surface for applying loyalty points to the current order
// session.
//
// GET  /loyalty/balance/{userId}         → JSON { balance, ratePerPoint, canRedeem }
// POST /loyalty/redeem                   → JSON { success, balance, message }
//
// The redeem flow:
//   1. Validate the operator's input (points > 0, ≤ user's balance).
//   2. Convert points to a wallet credit via `Loyalty.PointToCurrencyRate`
//      (default 1.0 — i.e. 1 point = ৳1). Tenant setting.
//   3. Call ILoyaltyService.RedeemAsync to draw points + audit.
//   4. Call IWalletService.RechargeAsync to credit the wallet (idempotent
//      via `loyalty-redeem:<userId>:<at>` source string).
//
// The counter UI surfaces a "Use {n} points" button next to the keypad once
// the balance is loaded; the kiosk surfaces a similar button on the order
// review screen.
// =============================================================================

using CanteenManagementSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Loyalty;
using Platform.Application.Abstractions.Wallets;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "Operator")]
[Route("loyalty")]
public sealed class LoyaltyRedeemController : Controller
{
    private readonly ILoyaltyService _loyalty;
    private readonly IWalletService _wallet;
    private readonly ITenantSettings _settings;
    private readonly IAuditTrail _audit;

    public LoyaltyRedeemController(
        ILoyaltyService loyalty, IWalletService wallet,
        ITenantSettings settings, IAuditTrail audit)
    {
        _loyalty = loyalty; _wallet = wallet; _settings = settings; _audit = audit;
    }

    [HttpGet("balance/{userId}")]
    public async Task<IActionResult> Balance(string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest();
        var balance = await _loyalty.GetBalanceAsync(userId, ct);
        var enabled = await _settings.GetBoolAsync("Loyalty.Enabled", false, ct);
        var rate    = await _settings.GetDecimalAsync("Loyalty.PointToCurrencyRate", 1m, ct);
        return Json(new { userId, balance, rate, enabled, canRedeem = enabled && balance > 0 });
    }

    public sealed class RedeemRequest
    {
        public string UserId { get; set; } = string.Empty;
        public CanteenUserType UserType { get; set; }
        public decimal Points { get; set; }
        public int OrderId { get; set; }     // Optional — 0 if pre-order session
        public string? Reason { get; set; }
    }

    [HttpPost("redeem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Redeem([FromBody] RedeemRequest request, CancellationToken ct)
    {
        var enabled = await _settings.GetBoolAsync("Loyalty.Enabled", false, ct);
        if (!enabled) return Json(new { success = false, message = "Loyalty disabled for this tenant." });
        if (request.Points <= 0) return Json(new { success = false, message = "Points must be positive." });

        var rate = await _settings.GetDecimalAsync("Loyalty.PointToCurrencyRate", 1m, ct);
        var amount = decimal.Round(request.Points * rate, 2);
        if (amount <= 0) return Json(new { success = false, message = "Redeem rate is zero — ask admin to set Loyalty.PointToCurrencyRate." });

        var balance = await _loyalty.GetBalanceAsync(request.UserId, ct);
        if (balance < request.Points)
            return Json(new { success = false, message = $"Insufficient points (have {balance}, need {request.Points})." });

        var orderId = request.OrderId > 0 ? request.OrderId : 0;
        var reason  = string.IsNullOrWhiteSpace(request.Reason)
            ? $"Counter redeem: {request.Points} points × {rate:N2} = ৳{amount:N0}"
            : request.Reason!;

        var redeem = await _loyalty.RedeemAsync(request.UserId, request.Points, orderId, reason, ct);
        if (redeem.IsFailure) return Json(new { success = false, message = redeem.Error.Message });

        var credit = await _wallet.RechargeAsync(
            request.UserId, request.UserType.ToString(), amount,
            source: $"Loyalty.Redeem #{request.UserId} {DateTime.UtcNow:O}", ct);
        if (credit.IsFailure)
        {
            // We've debited points but failed to credit cash — log loudly + try a reversal.
            await _loyalty.AdjustAsync(request.UserId, request.Points, "Reversal — wallet credit failed", ct);
            return Json(new { success = false, message = "Wallet credit failed — points refunded." });
        }

        await _audit.RecordAsync("Loyalty.Redeemed", "User", request.UserId,
            new { request.Points, amountCredited = amount, orderId }, ct);

        var newBalance = await _loyalty.GetBalanceAsync(request.UserId, ct);
        return Json(new { success = true, balance = newBalance, amountCredited = amount,
            message = $"Redeemed {request.Points} points for ৳{amount:N0}." });
    }
}

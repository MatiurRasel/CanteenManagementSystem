// =============================================================================
// MfaService  (Platform.Infrastructure.Auth)
// -----------------------------------------------------------------------------
// Default IMfaService implementation. Follows the strict layering rule
// (ADR 0004): no IAppDbContext; uses IUnitOfWork.Repository<User>() for
// persistence and delegates recovery-code work to IMfaRecoveryCodeService.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Auth;
using Platform.Application.Persistence;
using Platform.Domain.Identity;

namespace Platform.Infrastructure.Auth;

public sealed class MfaService : IMfaService
{
    private readonly IUnitOfWork _uow;
    private readonly TotpService _totp;
    private readonly IMfaRecoveryCodeService _recovery;

    public MfaService(IUnitOfWork uow, TotpService totp, IMfaRecoveryCodeService recovery)
    {
        _uow = uow; _totp = totp; _recovery = recovery;
    }

    public async Task<MfaStatus> GetStatusAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(userId, cancellationToken);
        if (user is null) return new MfaStatus(false, 0);

        var remaining = user.MfaEnabled
            ? await _recovery.RemainingAsync(userId, cancellationToken)
            : 0;
        return new MfaStatus(user.MfaEnabled, remaining);
    }

    public Task<MfaSetupHandshake> BeginSetupAsync(int userId, string issuer = "Canteen", CancellationToken cancellationToken = default)
    {
        var secret = _totp.GenerateSecret();
        return FindUserAsync(userId, cancellationToken).ContinueWith(t =>
        {
            var user = t.Result;
            var account = user?.UserName ?? "user";
            return new MfaSetupHandshake(secret, _totp.BuildOtpAuthUri(secret, issuer, account));
        }, cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    public async Task<bool> ConfirmSetupAsync(int userId, string pendingSecret, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pendingSecret) || string.IsNullOrWhiteSpace(code)) return false;
        if (!_totp.Verify(pendingSecret, code.Trim())) return false;

        var user = await FindUserAsync(userId, cancellationToken);
        if (user is null) return false;

        user.TotpSecret   = pendingSecret;
        user.MfaEnabled   = true;
        user.UpdatedAtUtc = DateTime.UtcNow;
        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task DisableAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(userId, cancellationToken);
        if (user is null) return;
        user.MfaEnabled   = false;
        user.TotpSecret   = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<MfaVerifyOutcome> VerifyAsync(int userId, string codeOrRecovery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codeOrRecovery)) return MfaVerifyOutcome.Invalid;
        var entered = codeOrRecovery.Trim();
        var isRecovery = entered.Contains('-') || entered.Length > 8;

        if (isRecovery)
        {
            var ok = await _recovery.VerifyAndConsumeAsync(userId, entered, cancellationToken);
            return ok ? MfaVerifyOutcome.OkRecoveryCode : MfaVerifyOutcome.Invalid;
        }

        var user = await FindUserAsync(userId, cancellationToken);
        if (user is null || !user.MfaEnabled || string.IsNullOrEmpty(user.TotpSecret))
            return MfaVerifyOutcome.Invalid;

        return _totp.Verify(user.TotpSecret, entered) ? MfaVerifyOutcome.OkTotp : MfaVerifyOutcome.Invalid;
    }

    private Task<User?> FindUserAsync(int userId, CancellationToken cancellationToken)
        => _uow.Repository<User>().Query().IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
}

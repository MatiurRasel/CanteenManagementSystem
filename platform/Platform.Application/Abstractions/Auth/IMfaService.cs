// =============================================================================
// IMfaService  (Platform.Application.Abstractions.Auth)
// -----------------------------------------------------------------------------
// Application-layer facade for MFA enrolment / verification. Lives in the
// Application abstractions layer so Controllers depend on the contract, not
// on the EF DbContext or any infrastructure detail.
//
// Per ADR 0004 — Controllers go through services; services use IRepository.
// =============================================================================

namespace Platform.Application.Abstractions.Auth;

public sealed record MfaStatus(bool IsEnabled, int RemainingRecoveryCodes);

public sealed record MfaSetupHandshake(string PendingSecret, string OtpAuthUri);

public interface IMfaService
{
    /// <summary>Check current MFA state for the signed-in user.</summary>
    Task<MfaStatus> GetStatusAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Issue a fresh pending secret + otpauth:// URI for QR rendering.</summary>
    Task<MfaSetupHandshake> BeginSetupAsync(int userId, string issuer = "Canteen", CancellationToken cancellationToken = default);

    /// <summary>Confirm the user can read codes from the supplied secret. Persists secret + flips MfaEnabled on success.</summary>
    Task<bool> ConfirmSetupAsync(int userId, string pendingSecret, string code, CancellationToken cancellationToken = default);

    /// <summary>Turn MFA off and clear the stored secret. Recovery codes are NOT auto-cleared (audit retention).</summary>
    Task DisableAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Verify a 6-digit TOTP code OR a recovery code (router auto-detects by length / hyphen).</summary>
    Task<MfaVerifyOutcome> VerifyAsync(int userId, string codeOrRecovery, CancellationToken cancellationToken = default);
}

public enum MfaVerifyOutcome
{
    Invalid              = 0,
    OkTotp               = 1,
    OkRecoveryCode       = 2,
}

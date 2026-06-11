// =============================================================================
// IMfaRecoveryCodeService  (Platform.Application.Abstractions.Auth)
// -----------------------------------------------------------------------------
// Recovery codes are the fallback path when a user has lost access to their
// TOTP authenticator app. Each code is one-shot and stored only as a PBKDF2
// hash — the raw codes are shown to the user ONCE at issuance.
//
// CONTRACT
//   RegenerateAsync(userId, count)
//       Invalidates every existing code for the user; mints `count` fresh
//       codes; returns the raw codes (caller is responsible for showing
//       them to the user once and never persisting them in cleartext).
//
//   VerifyAndConsumeAsync(userId, rawCode)
//       Hashes the raw code, looks up an unused match for the user, marks
//       it consumed atomically. Returns true on success.
//
//   RemainingAsync(userId)
//       Count of unconsumed codes — drives the "you should regenerate" UI
//       prompt once it drops to a configurable threshold (default 2).
// =============================================================================

namespace Platform.Application.Abstractions.Auth;

public interface IMfaRecoveryCodeService
{
    /// <summary>Issue a fresh batch of recovery codes; existing codes are revoked.</summary>
    /// <returns>The raw codes to be shown to the user exactly once.</returns>
    Task<IReadOnlyList<string>> RegenerateAsync(int userId, int count = 8, CancellationToken cancellationToken = default);

    /// <summary>Verify a code and (on success) mark it consumed atomically.</summary>
    Task<bool> VerifyAndConsumeAsync(int userId, string rawCode, CancellationToken cancellationToken = default);

    /// <summary>Number of unused recovery codes left for the user.</summary>
    Task<int> RemainingAsync(int userId, CancellationToken cancellationToken = default);
}

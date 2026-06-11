// =============================================================================
// IApiKeyAuthorizer  (Platform.Application.Abstractions.Auth)
// -----------------------------------------------------------------------------
// Validates the X-App-Key / X-App-Signature / X-App-Timestamp triple on
// inbound API requests. Used by partner integrations (school portal,
// mobile-app backend) to call the canteen API server-to-server.
//
// VALIDATION STEPS (in order)
//   1. Timestamp within ±5 min of server clock        → reject if skewed.
//   2. Find AppApiKey row by X-App-Key                → reject if missing/revoked.
//   3. Optional IP allowlist                          → reject if mismatch.
//   4. Recompute HMAC-SHA256(Secret, canonicalString) → reject if mismatch.
//   5. Update LastUsedAtUtc / UsageCount on success.
//
// canonicalString =
//   "{X-App-Key}\n{X-App-Timestamp}\n{HTTP-VERB}\n{Path}\n{BodySha256-Hex}"
// =============================================================================

using Platform.Application.Results;

namespace Platform.Application.Abstractions.Auth;

public interface IApiKeyAuthorizer
{
    Task<Result<ApiKeyContext>> AuthorizeAsync(
        string? appKey,
        string? signature,
        string? timestamp,
        string httpMethod,
        string path,
        string? bodySha256,
        string? clientIp,
        CancellationToken cancellationToken = default);
}

public sealed record ApiKeyContext(
    int ApiKeyId,
    int ClientId,
    string ClientCode,
    string DisplayName,
    IReadOnlyList<string> Scopes);

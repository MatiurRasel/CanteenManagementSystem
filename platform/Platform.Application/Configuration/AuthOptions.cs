// =============================================================================
// AuthOptions  (Platform.Application.Configuration)
// -----------------------------------------------------------------------------
// Bound from "Auth" in appsettings.json. The signing key SHOULD live in
// dotnet user-secrets / Azure Key Vault, NOT in appsettings.json itself.
// =============================================================================

namespace Platform.Application.Configuration;

public class AuthOptions
{
    /// <summary>The "iss" claim and JWT validator's expected issuer.</summary>
    public string Issuer { get; set; } = "canteen-platform";

    /// <summary>The "aud" claim and JWT validator's expected audience.</summary>
    public string Audience { get; set; } = "canteen-clients";

    /// <summary>Base64 of >= 32 random bytes. Generate via `dotnet user-secrets set "Auth:SigningKey" "$(openssl rand -base64 48)"`.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Access-token TTL in minutes.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh-token TTL in days.</summary>
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>Number of consecutive failed logins before a temporary lockout.</summary>
    public int MaxLoginAttempts { get; set; } = 5;

    /// <summary>Lockout duration in minutes after MaxLoginAttempts.</summary>
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Tolerance for API-key timestamp drift (seconds).</summary>
    public int ApiKeyTimestampToleranceSeconds { get; set; } = 300;
}

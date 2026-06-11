// =============================================================================
// TotpService  (Platform.Infrastructure.Auth)
// -----------------------------------------------------------------------------
// Thin wrapper around Otp.NET that:
//   * generates a fresh Base32 secret on enrolment
//   * formats the otpauth:// URI consumed by Authy / Google Authenticator /
//     Microsoft Authenticator / 1Password etc.
//   * verifies a 6-digit code against the secret with the standard
//     ±1 step (30 s) clock-skew window
//
// We deliberately don't add a QR-image library; the enrolment view shows the
// secret text AND the otpauth URI, and the user can either type the secret or
// paste the URI into their authenticator. (Most authenticators accept either.)
// =============================================================================

using System.Security.Cryptography;
using OtpNet;

namespace Platform.Infrastructure.Auth;

public sealed class TotpService
{
    /// <summary>Generate a fresh 20-byte Base32 secret suitable for storage.</summary>
    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32Encoding.ToString(bytes).TrimEnd('=');
    }

    /// <summary>
    /// Build the canonical otpauth URI an authenticator app reads:
    ///   otpauth://totp/{issuer}:{accountName}?secret={secret}&issuer={issuer}&digits=6&period=30
    /// </summary>
    public string BuildOtpAuthUri(string secret, string issuer, string accountName)
    {
        var safeIssuer  = Uri.EscapeDataString(issuer);
        var safeAccount = Uri.EscapeDataString(accountName);
        var safeSecret  = Uri.EscapeDataString(secret);
        return $"otpauth://totp/{safeIssuer}:{safeAccount}?secret={safeSecret}&issuer={safeIssuer}&digits=6&period=30";
    }

    /// <summary>Validate a 6-digit code against the stored secret (±1 step / ±30 s).</summary>
    public bool Verify(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;
        try
        {
            var bytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(bytes);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch { return false; }
    }
}

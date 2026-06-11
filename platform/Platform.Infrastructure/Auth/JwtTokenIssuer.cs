// =============================================================================
// JwtTokenIssuer  (Platform.Infrastructure.Auth)
// -----------------------------------------------------------------------------
// HS256 JWTs. We deliberately use a *symmetric* key because every service
// that signs or validates tokens runs inside the same trust boundary (we
// control the platform). If you ever federate verification to a third party
// — partner mobile app, public OAuth client — switch to RS256 + JWKS.
//
// REFRESH TOKEN
//   * 32 random bytes, base64url-encoded -> ~43 chars sent over the wire.
//   * Stored as SHA-256 hex digest in AppRefreshTokens — leak-resistant.
//   * Rotated on every /auth/refresh; previous token marked revoked.
// =============================================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Platform.Application.Abstractions.Auth;
using Platform.Application.Configuration;

namespace Platform.Infrastructure.Auth;

public sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly AuthOptions _options;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenIssuer(IOptions<AuthOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Auth:SigningKey must be a base64 string of at least 32 bytes. " +
                "Generate with: `dotnet user-secrets set \"Auth:SigningKey\" \"$(openssl rand -base64 48)\"`.");
        }
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
    }

    public string IssueAccessToken(TokenSubject subject)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, subject.UserName),
            new("name",      subject.DisplayName),
            new("client_id", subject.ClientId.ToString()),
            new("tenant",    subject.ClientCode),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        foreach (var role in subject.Roles)        claims.Add(new Claim(ClaimTypes.Role, role));
        foreach (var perm in subject.Permissions)  claims.Add(new Claim("perm", perm));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string PlainToken, string HashedToken, DateTime ExpiresAtUtc) IssueRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var plain = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash  = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain)));
        return (plain, hash, DateTime.UtcNow.AddDays(_options.RefreshTokenDays));
    }

    public TokenSubject? Validate(string accessToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out var validated);

            var jwt = (JwtSecurityToken)validated;
            var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
            var perms = jwt.Claims.Where(c => c.Type == "perm").Select(c => c.Value).ToList();
            return new TokenSubject(
                UserId:      int.Parse(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value),
                UserName:    jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value,
                DisplayName: jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? string.Empty,
                ClientId:    int.Parse(jwt.Claims.First(c => c.Type == "client_id").Value),
                ClientCode:  jwt.Claims.FirstOrDefault(c => c.Type == "tenant")?.Value ?? string.Empty,
                Roles: roles, Permissions: perms);
        }
        catch { return null; }
    }
}

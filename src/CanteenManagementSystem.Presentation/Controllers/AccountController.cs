// =============================================================================
// AccountController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Cookie-based browser login + change-password. Calls IAuthService — same
// engine that backs /api/v1/auth/* — so password rules, lockout, audit, and
// tenant binding are identical across surfaces.
//
// COOKIE CLAIMS WRITTEN ON SIGN-IN
//   sub        = UserId
//   name       = UserName
//   email      = Email (if set)
//   role*      = one claim per role
//   perm*      = one claim per permission
//   client_id  = user's ClientId  (the user's bound tenant — drives EF filter downstream)
//   tenant     = user's ClientCode
//
// MUSTCHANGEPASSWORD
//   On successful sign-in, if UserProfileDto.MustChangePassword is true we
//   redirect to /Account/ChangePassword. A pipeline middleware blocks every
//   other route until the change completes.
//
// CSRF
//   Every POST is [ValidateAntiForgeryToken]; Razor forms emit the token via
//   @Html.AntiForgeryToken().
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Auth;
using Platform.Application.Abstractions.Notifications;
using Platform.Domain.Identity;
using Platform.Domain.Notifications;
using Platform.Presentation.Auth;

namespace CanteenManagementSystem.Presentation.Controllers;

[AllowAnonymous]
[Route("Account")]
public sealed class AccountController : Controller
{
    // ─── Layering (ADR 0004) ──────────────────────────────────────────────
    // Controller depends ONLY on application-layer service interfaces. No
    // DbContext, no DbSet, no IRepository — those belong inside services.
    private readonly IAuthService _auth;
    private readonly IMfaService _mfa;
    private readonly IMfaRecoveryCodeService _recovery;
    private readonly IAuthTokenService _tokens;
    private readonly INotificationService _notifications;
    private readonly IConfiguration _config;

    public AccountController(IAuthService auth, IMfaService mfa, IMfaRecoveryCodeService recovery,
        IAuthTokenService tokens, INotificationService notifications, IConfiguration config)
    {
        _auth = auth; _mfa = mfa; _recovery = recovery;
        _tokens = tokens; _notifications = notifications; _config = config;
    }

    /// <summary>True when Auth:Google:ClientId is configured — used by the login view to show/hide the Google button.</summary>
    public bool GoogleSsoEnabled => !string.IsNullOrWhiteSpace(_config["Auth:Google:ClientId"]);

    /// <summary>Exposed to views so they can render the SSO button conditionally.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Razor needs an instance member.")]
    public bool ShouldShowGoogleButton() => GoogleSsoEnabled;

    // ─── Login ────────────────────────────────────────────────────────────

    public sealed class LoginForm
    {
        [Required, StringLength(64)]
        public string UserName { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(128)]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
        public bool RememberMe { get; set; }
    }

    [HttpGet("Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["GoogleSsoEnabled"] = GoogleSsoEnabled;
        return View(new LoginForm { ReturnUrl = returnUrl });
    }

    [HttpPost("Login")]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth-login")]
    public async Task<IActionResult> Login(LoginForm form, CancellationToken ct)
    {
        ViewData["GoogleSsoEnabled"] = GoogleSsoEnabled;
        if (!ModelState.IsValid) return View(form);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.LoginAsync(new LoginRequest(form.UserName.Trim(), form.Password, ip), ct);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message ?? "Sign-in failed.");
            return View(form);
        }

        var profile = result.Value!.User;

        // ─── MFA intercept ────────────────────────────────────────────────
        // If the user has MFA enrolled, we DON'T sign in yet — we drop a short-
        // lived "pending mfa" cookie and redirect to the verify form. The
        // MFA status lookup goes through IMfaService (ADR 0004 — no DbContext
        // in controllers).
        var mfaStatus = await _mfa.GetStatusAsync(profile.UserId, ct);
        if (mfaStatus.IsEnabled)
        {
            Response.Cookies.Append("ccs.mfa.pending", profile.UserId.ToString(),
                new Microsoft.AspNetCore.Http.CookieOptions
                {
                    HttpOnly = true,
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                    Secure   = Request.IsHttps,
                    Expires  = DateTimeOffset.UtcNow.AddMinutes(5),
                    Path     = "/"
                });
            TempData["Mfa.ReturnUrl"]   = form.ReturnUrl;
            TempData["Mfa.RememberMe"] = form.RememberMe;
            return RedirectToAction(nameof(VerifyMfa));
        }

        var claims = BuildClaims(profile);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, PlatformAuthSchemes.Cookie));

        await HttpContext.SignInAsync(
            PlatformAuthSchemes.Cookie,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = form.RememberMe,
                ExpiresUtc   = DateTimeOffset.UtcNow.AddHours(form.RememberMe ? 168 : 8)
            });

        // Force password change BEFORE any other navigation.
        if (profile.MustChangePassword)
            return RedirectToAction(nameof(ChangePassword));

        return RedirectToLocal(form.ReturnUrl);
    }

    [HttpPost("Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(PlatformAuthSchemes.Cookie);
        return Redirect("/");
    }

    // ─── MFA verify (step 2 of login) ─────────────────────────────────────

    public sealed class VerifyMfaForm
    {
        /// <summary>6-digit TOTP code OR a recovery code like "5F2H7-K9XYM".</summary>
        [Required, StringLength(20, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;
    }

    [HttpGet("verify-mfa")]
    public IActionResult VerifyMfa() => View(new VerifyMfaForm());

    [HttpPost("verify-mfa")]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth-login")]
    public async Task<IActionResult> VerifyMfa(VerifyMfaForm form, CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue("ccs.mfa.pending", out var pendingUserId) ||
            !int.TryParse(pendingUserId, out var userId))
        {
            return RedirectToAction(nameof(Login));
        }

        var outcome = await _mfa.VerifyAsync(userId, form.Code, ct);
        if (outcome == MfaVerifyOutcome.Invalid)
        {
            var entered = form.Code.Trim();
            var lookedLikeRecovery = entered.Contains('-') || entered.Length > 8;
            ModelState.AddModelError(nameof(form.Code),
                lookedLikeRecovery
                    ? "Recovery code is invalid or already used."
                    : "Invalid code. Try the next one your authenticator shows (or use a recovery code).");
            return View(form);
        }

        if (outcome == MfaVerifyOutcome.OkRecoveryCode)
        {
            TempData["Flash.Warning"] =
                "Signed in with a recovery code. Regenerate your recovery codes on the MFA page so this one isn't reused.";
        }

        // Hydrate the post-MFA profile via the auth service so the controller
        // never touches the DbContext (ADR 0004).
        var profileResult = await _auth.GetProfileAsync(userId, ct);
        if (!profileResult.IsSuccess) return RedirectToAction(nameof(Login));
        var profile = profileResult.Value!;

        Response.Cookies.Delete("ccs.mfa.pending");

        var claims = BuildClaims(profile);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, PlatformAuthSchemes.Cookie));
        var remember = TempData["Mfa.RememberMe"] is true;
        await HttpContext.SignInAsync(PlatformAuthSchemes.Cookie, principal,
            new AuthenticationProperties
            {
                IsPersistent = remember,
                ExpiresUtc   = DateTimeOffset.UtcNow.AddHours(remember ? 168 : 8)
            });

        if (profile.MustChangePassword) return RedirectToAction(nameof(ChangePassword));
        var returnUrl = TempData["Mfa.ReturnUrl"] as string;
        return RedirectToLocal(returnUrl);
    }

    /// <summary>DRY claim builder — used by both Login and VerifyMfa sign-in paths.</summary>
    private static List<Claim> BuildClaims(UserProfileDto profile)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, profile.UserId.ToString()),
            new(ClaimTypes.Name, profile.UserName),
            new("client_id", (profile.ClientId ?? 0).ToString()),
            new("tenant",    profile.ClientCode)
        };
        if (!string.IsNullOrEmpty(profile.Email)) claims.Add(new Claim(ClaimTypes.Email, profile.Email));
        foreach (var r in profile.Roles)       claims.Add(new Claim(ClaimTypes.Role, r));
        foreach (var p in profile.Permissions) claims.Add(new Claim("perm", p));
        if (profile.MustChangePassword)        claims.Add(new Claim("must_change_password", "true"));
        return claims;
    }

    // ─── MFA setup (signed-in user enrols / disables) ─────────────────────

    public sealed class SetupMfaVm
    {
        public bool IsEnabled { get; set; }
        public string? PendingSecret { get; set; }
        public string? OtpAuthUri { get; set; }
        public int RemainingRecoveryCodes { get; set; }
    }

    /// <summary>Shown ONCE after a regenerate — the only time the raw codes are visible.</summary>
    public sealed class RecoveryCodesVm
    {
        public IReadOnlyList<string> Codes { get; init; } = Array.Empty<string>();
    }

    public sealed class ConfirmMfaForm
    {
        [Required] public string PendingSecret { get; set; } = string.Empty;
        [Required, StringLength(8, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;
    }

    [HttpGet("mfa")]
    [Authorize(Policy = "AuthenticatedAny")]
    public async Task<IActionResult> Mfa(CancellationToken ct)
    {
        var uid = ResolveUserId();
        if (uid is null) return RedirectToAction(nameof(Login));

        var status = await _mfa.GetStatusAsync(uid.Value, ct);
        var vm = new SetupMfaVm
        {
            IsEnabled              = status.IsEnabled,
            RemainingRecoveryCodes = status.RemainingRecoveryCodes
        };
        if (!status.IsEnabled)
        {
            var handshake = await _mfa.BeginSetupAsync(uid.Value, "Canteen", ct);
            vm.PendingSecret = handshake.PendingSecret;
            vm.OtpAuthUri    = handshake.OtpAuthUri;
        }
        return View(vm);
    }

    [HttpPost("mfa/regenerate-recovery-codes")]
    [Authorize(Policy = "AuthenticatedAny")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateRecoveryCodes(CancellationToken ct)
    {
        var uid = ResolveUserId();
        if (uid is null) return RedirectToAction(nameof(Login));

        var status = await _mfa.GetStatusAsync(uid.Value, ct);
        if (!status.IsEnabled)
        {
            TempData["Flash.Error"] = "Enrol MFA before generating recovery codes.";
            return RedirectToAction(nameof(Mfa));
        }
        var codes = await _recovery.RegenerateAsync(uid.Value, count: 8, ct);
        TempData["Flash.Success"] = $"Issued {codes.Count} fresh recovery codes. Store them somewhere safe — they will not be shown again.";
        return View("RecoveryCodes", new RecoveryCodesVm { Codes = codes });
    }

    [HttpPost("mfa")]
    [Authorize(Policy = "AuthenticatedAny")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmMfa(ConfirmMfaForm form, CancellationToken ct)
    {
        var uid = ResolveUserId();
        if (uid is null) return RedirectToAction(nameof(Login));

        var ok = await _mfa.ConfirmSetupAsync(uid.Value, form.PendingSecret, form.Code, ct);
        if (!ok)
        {
            TempData["Flash.Error"] = "Code didn't match. Try the next one and submit faster.";
            return RedirectToAction(nameof(Mfa));
        }
        TempData["Flash.Success"] = "Multi-factor authentication enrolled.";
        return RedirectToAction(nameof(Mfa));
    }

    [HttpPost("mfa/disable")]
    [Authorize(Policy = "AuthenticatedAny")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableMfa(CancellationToken ct)
    {
        var uid = ResolveUserId();
        if (uid is null) return RedirectToAction(nameof(Login));
        await _mfa.DisableAsync(uid.Value, ct);
        TempData["Flash.Warning"] = "MFA disabled. Re-enrol soon for stronger account security.";
        return RedirectToAction(nameof(Mfa));
    }

    private int? ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var n) ? n : null;
    }

    [HttpGet("AccessDenied")]
    public IActionResult AccessDenied() => View();

    // ─── Change password ──────────────────────────────────────────────────

    public sealed class ChangePasswordForm
    {
        [Required, DataType(DataType.Password), StringLength(128)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 8,
            ErrorMessage = "New password must be at least 8 characters.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(NewPassword),
            ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    [HttpGet("ChangePassword")]
    [Authorize(Policy = "AuthenticatedAny")]
    public IActionResult ChangePassword() => View(new ChangePasswordForm());

    [HttpPost("ChangePassword")]
    [Authorize(Policy = "AuthenticatedAny")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            ModelState.AddModelError(string.Empty, "Sign in again.");
            return View(form);
        }

        var result = await _auth.ChangePasswordAsync(userId, form.CurrentPassword, form.NewPassword, ct);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message ?? "Could not change password.");
            return View(form);
        }

        // Re-sign-in to drop the must_change_password claim and refresh expiry.
        await HttpContext.SignOutAsync(PlatformAuthSchemes.Cookie);
        TempData["Flash.Success"] = "Password updated. Please sign in with your new password.";
        return RedirectToAction(nameof(Login));
    }

    // ─── Forgot password ──────────────────────────────────────────────────
    public sealed class ForgotPasswordForm
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;
    }

    [HttpGet("ForgotPassword")]
    public IActionResult ForgotPassword() => View(new ForgotPasswordForm());

    [HttpPost("ForgotPassword")]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth-login")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        // Always show the same confirmation — never disclose whether an email is registered.
        var lookup = await _auth.FindUserIdByEmailAsync(form.Email, ct);
        if (lookup.IsSuccess && lookup.Value is int uid)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var token = await _tokens.IssueAsync(uid, AuthTokenPurpose.PasswordReset, TimeSpan.FromMinutes(30), ip, ct);
            var resetUrl = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword?token={Uri.EscapeDataString(token.RawToken)}";
            try
            {
                var body = $"You requested a password reset.\n\nClick to choose a new password (expires in 30 minutes):\n{resetUrl}\n\nIf you didn't request this, ignore this email.";
                await _notifications.SendRawAsync(NotificationChannel.Email, form.Email, "Reset your password", body, cancellationToken: ct);
            }
            catch { /* email failure is silent here — caller already got generic confirmation */ }
        }

        TempData["Flash.Success"] =
            "If an account exists for that email, a reset link has been sent. Check your inbox (and spam folder).";
        return RedirectToAction(nameof(Login));
    }

    public sealed class ResetPasswordForm
    {
        [Required, StringLength(64)]
        public string Token { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 8,
            ErrorMessage = "New password must be at least 8 characters.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(NewPassword),
            ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    [HttpGet("ResetPassword")]
    public IActionResult ResetPassword([FromQuery] string? token)
        => View(new ResetPasswordForm { Token = token ?? string.Empty });

    [HttpPost("ResetPassword")]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth-login")]
    public async Task<IActionResult> ResetPassword(ResetPasswordForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        var redeem = await _tokens.RedeemAsync(form.Token, AuthTokenPurpose.PasswordReset, ct);
        if (redeem is null)
        {
            ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired. Request a new one.");
            return View(form);
        }

        var result = await _auth.ResetPasswordAsync(redeem.UserId, form.NewPassword, ct);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message ?? "Could not reset password.");
            return View(form);
        }

        TempData["Flash.Success"] = "Password updated. Please sign in with your new password.";
        return RedirectToAction(nameof(Login));
    }

    // ─── Magic-link sign-in (passwordless) ────────────────────────────────
    public sealed class MagicLinkForm
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;
    }

    [HttpGet("MagicLink")]
    public IActionResult MagicLink() => View(new MagicLinkForm());

    [HttpPost("MagicLink")]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth-login")]
    public async Task<IActionResult> MagicLink(MagicLinkForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        var lookup = await _auth.FindUserIdByEmailAsync(form.Email, ct);
        if (lookup.IsSuccess && lookup.Value is int uid)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var token = await _tokens.IssueAsync(uid, AuthTokenPurpose.MagicLink, TimeSpan.FromMinutes(15), ip, ct);
            var url = $"{Request.Scheme}://{Request.Host}/Account/MagicLink/Consume?token={Uri.EscapeDataString(token.RawToken)}";
            try
            {
                var body = $"Click this one-time link to sign in (expires in 15 minutes):\n\n{url}\n\nIf you didn't request this, ignore the email.";
                await _notifications.SendRawAsync(NotificationChannel.Email, form.Email, "Your sign-in link", body, cancellationToken: ct);
            }
            catch { /* silent — caller gets generic confirmation */ }
        }

        TempData["Flash.Success"] =
            "If an account exists for that email, a one-time sign-in link has been sent.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("MagicLink/Consume")]
    public async Task<IActionResult> ConsumeMagicLink([FromQuery] string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Flash.Error"] = "Missing sign-in token.";
            return RedirectToAction(nameof(Login));
        }
        var redeem = await _tokens.RedeemAsync(token, AuthTokenPurpose.MagicLink, ct);
        if (redeem is null)
        {
            TempData["Flash.Error"] = "This sign-in link is invalid or has expired.";
            return RedirectToAction(nameof(Login));
        }

        var profileResult = await _auth.GetProfileAsync(redeem.UserId, ct);
        if (!profileResult.IsSuccess)
        {
            TempData["Flash.Error"] = "Account no longer available.";
            return RedirectToAction(nameof(Login));
        }
        var profile = profileResult.Value!;

        var claims = BuildClaims(profile);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, PlatformAuthSchemes.Cookie));
        await HttpContext.SignInAsync(PlatformAuthSchemes.Cookie, principal,
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });

        TempData["Flash.Success"] = "Signed in via email link.";
        return Redirect("/");
    }

    // ─── External (Google) sign-in ────────────────────────────────────────
    [HttpPost("ExternalLogin")]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        if (!GoogleSsoEnabled || !string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Flash.Error"] = "Google sign-in is not configured on this server.";
            return RedirectToAction(nameof(Login));
        }
        var redirectUrl = Url.Action(nameof(ExternalCallback), "Account", new { returnUrl });
        var props = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(props, "Google");
    }

    [HttpGet("ExternalCallback")]
    public async Task<IActionResult> ExternalCallback(string? returnUrl = null, CancellationToken ct = default)
    {
        var ext = await HttpContext.AuthenticateAsync(PlatformAuthSchemes.External);
        if (!ext.Succeeded || ext.Principal is null)
        {
            TempData["Flash.Error"] = "External sign-in failed. Try again or use your password.";
            return RedirectToAction(nameof(Login));
        }

        var email = ext.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Flash.Error"] = "Google did not share an email address.";
            return RedirectToAction(nameof(Login));
        }

        var lookup = await _auth.FindUserIdByEmailAsync(email, ct);
        if (!lookup.IsSuccess || lookup.Value is not int uid)
        {
            TempData["Flash.Error"] = $"No SmartCanteen account is linked to {email}. Ask an admin to invite you.";
            return RedirectToAction(nameof(Login));
        }

        var profileResult = await _auth.GetProfileAsync(uid, ct);
        if (!profileResult.IsSuccess) return RedirectToAction(nameof(Login));
        var profile = profileResult.Value!;

        await HttpContext.SignOutAsync(PlatformAuthSchemes.External);
        var claims = BuildClaims(profile);
        claims.Add(new Claim("auth-scheme", "Google"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, PlatformAuthSchemes.Cookie));
        await HttpContext.SignInAsync(PlatformAuthSchemes.Cookie, principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });

        return string.IsNullOrEmpty(returnUrl) ? Redirect("/") : RedirectToLocal(returnUrl);
    }

    // ─── helpers ──────────────────────────────────────────────────────────

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return Redirect("/");
    }
}

using Platform.Application.Abstractions.Identity;

namespace Platform.Presentation.Auth;

/// HttpContext-backed implementation of <see cref="ICurrentUser"/>. Reads the
/// session-stored operator identity (set by OperatorAuthService once it lands).
/// Falls back to "anonymous" if no session is present.
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public string? UserId => _accessor.HttpContext?.Session.GetString("OperatorUserId");
    public string? UserName => _accessor.HttpContext?.Session.GetString("OperatorName");
    public string? Role => _accessor.HttpContext?.Session.GetString("OperatorRole");
    public bool IsAuthenticated => !string.IsNullOrEmpty(UserId);
}

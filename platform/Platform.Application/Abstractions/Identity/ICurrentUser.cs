namespace Platform.Application.Abstractions.Identity;

/// Per-request abstraction over the authenticated principal. Resolved from
/// HttpContext in Presentation and consumed by audit / authorization logic.
public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}

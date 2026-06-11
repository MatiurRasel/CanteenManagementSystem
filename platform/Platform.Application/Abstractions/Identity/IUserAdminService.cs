// =============================================================================
// IUserAdminService  (Platform.Application.Abstractions.Identity)
// -----------------------------------------------------------------------------
// Tenant-scoped user administration. Used by /admin/users (ADR 0004 — the
// controller never touches IAppDbContext).
// =============================================================================

using Platform.Application.Results;
using Platform.Domain.Identity;

namespace Platform.Application.Abstractions.Identity;

public sealed record UserListItem(
    int UserId, int? ClientId, string UserName, string DisplayName,
    string? Email, string UserKind, bool IsActive, DateTime? LastLoginAtUtc,
    IReadOnlyList<string> Roles);

public sealed record InviteUserInput(
    string UserName, string DisplayName, string Email, string UserKind,
    string RoleCode, int? BoundClientId);

public sealed record InviteUserOutcome(User User, string TempPassword);

public sealed record BulkImportRow(
    string UserName, string DisplayName, string? Email,
    string? UserKind, string? RoleCode, string? ExternalId);

public sealed record BulkImportOutcome(
    int Created, int Skipped, IReadOnlyList<(string UserName, string TempPassword)> SampleCredentials);

public sealed record ResetPasswordOutcome(User User, string TempPassword);

public interface IUserAdminService
{
    Task<IReadOnlyList<UserListItem>> ListAsync(bool crossTenant, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> ListAssignableRolesAsync(CancellationToken ct = default);

    Task<Result<InviteUserOutcome>> InviteAsync(InviteUserInput input, CancellationToken ct = default);

    Task<Result<User>> ToggleActiveAsync(int userId, bool requesterIsSystemAdmin, int? requesterClientId, CancellationToken ct = default);
    Task<Result<ResetPasswordOutcome>> ResetPasswordAsync(int userId, bool requesterIsSystemAdmin, int? requesterClientId, CancellationToken ct = default);

    /// <summary>Idempotent: existing UserNames are skipped, not overwritten.</summary>
    Task<Result<BulkImportOutcome>> BulkImportAsync(IEnumerable<BulkImportRow> rows, int? boundClientId, CancellationToken ct = default);
}

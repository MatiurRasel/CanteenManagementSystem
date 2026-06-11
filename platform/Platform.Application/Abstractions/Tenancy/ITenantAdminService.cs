// =============================================================================
// ITenantAdminService  (Platform.Application.Abstractions.Tenancy)
// -----------------------------------------------------------------------------
// Platform-admin surface for managing the Clients (tenant) table + bootstrap
// admin user. Used by /admin/tenants (SystemAdmin only) — controller per ADR
// 0004 never touches IAppDbContext.
// =============================================================================

using Platform.Application.Results;
using Platform.Domain.Tenancy;

namespace Platform.Application.Abstractions.Tenancy;

public sealed record NewTenantInput(
    string ClientCode, string ClientName, string? ShortName,
    string? Email, string? PhoneNumber, string? Address, string? WebsiteUrl,
    string AdminEmail, string AdminDisplayName);

public sealed record NewTenantOutcome(
    Client Client, string AdminLogin, string BootstrapPassword);

public interface ITenantAdminService
{
    Task<IReadOnlyList<Client>> ListAsync(CancellationToken ct = default);
    Task<Client?> GetByIdAsync(int clientId, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string clientCode, CancellationToken ct = default);

    Task<Result<NewTenantOutcome>> CreateAsync(NewTenantInput input, CancellationToken ct = default);
    Task<Result<Client>> ToggleActiveAsync(int clientId, CancellationToken ct = default);
}

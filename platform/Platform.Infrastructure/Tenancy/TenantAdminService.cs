// =============================================================================
// TenantAdminService  (Platform.Infrastructure.Tenancy)
// -----------------------------------------------------------------------------
// ITenantAdminService default impl. IUnitOfWork only — no IAppDbContext.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Auth;
using Platform.Application.Abstractions.Tenancy;
using Platform.Application.Persistence;
using Platform.Application.Results;
using Platform.Domain.Identity;
using Platform.Domain.Tenancy;

namespace Platform.Infrastructure.Tenancy;

public sealed class TenantAdminService : ITenantAdminService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;

    public TenantAdminService(IUnitOfWork uow, IPasswordHasher hasher)
    {
        _uow = uow; _hasher = hasher;
    }

    private IRepository<Client>   Clients   => _uow.Repository<Client>();
    private IRepository<User>     Users     => _uow.Repository<User>();
    private IRepository<Role>     Roles     => _uow.Repository<Role>();
    private IRepository<UserRole> UserRoles => _uow.Repository<UserRole>();

    public async Task<IReadOnlyList<Client>> ListAsync(CancellationToken ct = default)
        => await Clients.NoTrackingQuery()
            .IgnoreQueryFilters()
            .OrderBy(c => c.ClientId)
            .ToListAsync(ct);

    public Task<Client?> GetByIdAsync(int clientId, CancellationToken ct = default)
        => Clients.Query().IgnoreQueryFilters().FirstOrDefaultAsync(c => c.ClientId == clientId, ct)!;

    public Task<bool> CodeExistsAsync(string clientCode, CancellationToken ct = default)
        => Clients.Query().IgnoreQueryFilters().AnyAsync(c => c.ClientCode == clientCode, ct);

    public async Task<Result<NewTenantOutcome>> CreateAsync(NewTenantInput input, CancellationToken ct = default)
    {
        if (await CodeExistsAsync(input.ClientCode, ct))
            return Result.Failure<NewTenantOutcome>(Error.Conflict("This tenant code already exists."));

        var now = DateTime.UtcNow;
        var client = new Client
        {
            ClientCode  = input.ClientCode.Trim(),
            ClientName  = input.ClientName.Trim(),
            ShortName   = input.ShortName?.Trim(),
            Email       = input.Email?.Trim(),
            PhoneNumber = input.PhoneNumber?.Trim(),
            Address     = input.Address?.Trim(),
            WebsiteUrl  = input.WebsiteUrl?.Trim(),
            IsActive    = true,
            CreatedAtUtc = now
        };
        await Clients.AddAsync(client, ct);
        await _uow.SaveChangesAsync(ct);

        var bootstrapPassword = $"Change@{Guid.NewGuid().ToString("N")[..8]}";
        var user = new User
        {
            ClientId       = client.ClientId,
            UserName       = input.AdminEmail.Trim().ToLowerInvariant(),
            DisplayName    = input.AdminDisplayName.Trim(),
            Email          = input.AdminEmail.Trim(),
            UserKind       = "Admin",
            PasswordHash   = _hasher.Hash(bootstrapPassword),
            MustChangePassword = true,
            IsActive       = true,
            CreatedAtUtc   = now
        };
        await Users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        var tenantAdminRole = await Roles.FirstOrDefaultAsync(r => r.RoleCode == "TenantAdmin", ct);
        if (tenantAdminRole is not null)
        {
            await UserRoles.AddAsync(new UserRole { UserId = user.UserId, RoleId = tenantAdminRole.RoleId }, ct);
            await _uow.SaveChangesAsync(ct);
        }

        return Result.Success(new NewTenantOutcome(client, user.UserName, bootstrapPassword));
    }

    public async Task<Result<Client>> ToggleActiveAsync(int clientId, CancellationToken ct = default)
    {
        var client = await Clients.Query().IgnoreQueryFilters().FirstOrDefaultAsync(c => c.ClientId == clientId, ct);
        if (client is null) return Result.Failure<Client>(Error.NotFound("Tenant not found."));
        client.IsActive = !client.IsActive;
        Clients.Update(client);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(client);
    }
}

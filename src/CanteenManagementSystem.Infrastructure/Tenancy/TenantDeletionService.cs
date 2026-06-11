// =============================================================================
// TenantDeletionService  (CanteenManagementSystem.Infrastructure.Tenancy)
// -----------------------------------------------------------------------------
// Implements ITenantDeletionService. The hard-delete path runs SQL DELETE
// statements gated on the tenant's ClientId — no per-entity LINQ — because:
//
//   1. The set of tenant-owned tables is large + grows often. A SQL-driven
//      approach keeps adding new entities free (the EF model itself lists
//      every ITenantOwned table).
//   2. Per-tenant deletes are infrequent enough that a single transaction
//      bracketing a list of DELETEs is the right durability tradeoff.
//   3. The EF query filter is bypassed deliberately because we're operating
//      on a Client that's already soft-deleted (filter would hide everything).
// =============================================================================

using System.Text;
using CanteenManagementSystem.Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Persistence;
using Platform.Domain.Common;
using Platform.Domain.Tenancy;

namespace CanteenManagementSystem.Infrastructure.Tenancy;

internal sealed class TenantDeletionService : ITenantDeletionService
{
    private const int DefaultHoldDays = 30;
    private const int MinHoldDays = 7;

    private readonly IAppDbContext _db;        // Soft-delete + hard-delete must bypass the query filter.
    private readonly IAuditTrail   _audit;
    private readonly ILogger<TenantDeletionService> _logger;

    public TenantDeletionService(IAppDbContext db, IAuditTrail audit, ILogger<TenantDeletionService> logger)
    {
        _db = db; _audit = audit; _logger = logger;
    }

    public async Task<bool> SoftDeleteAsync(SoftDeleteRequest request, CancellationToken ct = default)
    {
        var client = await _db.Set<Client>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ClientId == request.ClientId, ct);
        if (client is null) return false;
        if (client.DeletedAtUtc is not null) return false;     // already soft-deleted

        var holdDays = Math.Max(MinHoldDays, request.HoldDaysOverride ?? DefaultHoldDays);
        client.DeletedAtUtc   = DateTime.UtcNow;
        client.HoldUntilUtc   = DateTime.UtcNow.AddDays(holdDays);
        client.DeletionReason = request.Reason;
        client.DeletedBy      = request.PerformedBy;
        client.IsActive       = false;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(
            action: "Tenant.SoftDeleted",
            entityType: nameof(Client),
            entityId: client.ClientId.ToString(),
            payload: new { client.ClientCode, request.Reason, holdDays, request.PerformedBy },
            cancellationToken: ct);
        _logger.LogWarning("Tenant {Tenant} ({ClientId}) soft-deleted; hard-delete at {When}",
            client.ClientCode, client.ClientId, client.HoldUntilUtc);
        return true;
    }

    public async Task<bool> RestoreAsync(int clientId, string? performedBy, CancellationToken ct = default)
    {
        var client = await _db.Set<Client>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ClientId == clientId, ct);
        if (client is null || client.DeletedAtUtc is null) return false;

        client.DeletedAtUtc   = null;
        client.HoldUntilUtc   = null;
        client.DeletionReason = null;
        client.DeletedBy      = null;
        client.IsActive       = true;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync("Tenant.Restored", nameof(Client), client.ClientId.ToString(),
            new { client.ClientCode, performedBy }, ct);
        return true;
    }

    public async Task<int> HardDeleteAsync(int clientId, string? performedBy, CancellationToken ct = default)
    {
        // Enumerate every entity type that implements ITenantOwned and DELETE
        // its rows scoped to this ClientId. Walking the metadata means new
        // tenant-owned entities are picked up automatically.
        var dbContext = (DbContext)_db;     // The interface is implemented by ApplicationDbContext.
        var tenantOwnedTypes = dbContext.Model.GetEntityTypes()
            .Where(et => typeof(ITenantOwned).IsAssignableFrom(et.ClrType))
            .Where(et => !et.IsOwned())
            .ToList();
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        var deleted = 0;

        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
            foreach (var et in tenantOwnedTypes)
            {
                if (et.ClrType == typeof(Client)) continue;
                var tableName = et.GetSchemaQualifiedTableName();
                if (string.IsNullOrEmpty(tableName)) continue;
                try
                {
                    var sql = $"DELETE FROM {tableName} WHERE [ClientId] = {{0}}";
                    deleted += await dbContext.Database.ExecuteSqlRawAsync(sql, new object[] { clientId }, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Hard-delete: skipping {Table} ({Reason})", tableName, ex.Message);
                }
            }

            var clientRow = await _db.Set<Client>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.ClientId == clientId, ct);
            if (clientRow is not null)
            {
                _db.Set<Client>().Remove(clientRow);
                deleted += await _db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        });

        await _audit.RecordAsync("Tenant.HardDeleted", nameof(Client), clientId.ToString(),
            new { rowsDeleted = deleted, performedBy }, ct);
        _logger.LogWarning("Tenant {ClientId} hard-deleted — {Rows} rows removed.", clientId, deleted);
        return deleted;
    }
}

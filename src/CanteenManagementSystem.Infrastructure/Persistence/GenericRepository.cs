// =============================================================================
// GenericRepository<TEntity>  (CanteenManagementSystem.Infrastructure.Persistence)
// -----------------------------------------------------------------------------
// The ONLY consumer of IAppDbContext outside of CQRS handlers (per ADR 0004).
// Implements both IReadOnlyRepository<TEntity> (read surface) and
// IRepository<TEntity> (full surface) — one class, two views.
//
// REGISTRATION
//   services.AddScoped(typeof(IRepository<>),         typeof(GenericRepository<>));
//   services.AddScoped(typeof(IReadOnlyRepository<>), typeof(GenericRepository<>));
//   services.AddScoped<IUnitOfWork, UnitOfWork>();
// =============================================================================

using System.Linq.Expressions;
using Platform.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanteenManagementSystem.Infrastructure.Persistence;

public class GenericRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
    private readonly IAppDbContext _dbContext;

    public GenericRepository(IAppDbContext dbContext) => _dbContext = dbContext;

    private DbSet<TEntity> Set() => _dbContext.Set<TEntity>();

    // ─── Read ─────────────────────────────────────────────────────────────

    public IQueryable<TEntity> Query() => Set().AsQueryable();

    public IQueryable<TEntity> NoTrackingQuery() => Set().AsNoTracking();

    public async Task<TEntity?> GetByIdAsync(object[] keyValues, CancellationToken cancellationToken = default)
        => await Set().FindAsync(keyValues, cancellationToken);

    public Task<TEntity?> GetByIdAsync(object keyValue, CancellationToken cancellationToken = default)
        => GetByIdAsync(new[] { keyValue }, cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => Set().AsNoTracking().AnyAsync(predicate, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => Set().AsNoTracking().CountAsync(cancellationToken);

    public Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => Set().AsNoTracking().CountAsync(predicate, cancellationToken);

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => Set().FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => Set().SingleOrDefaultAsync(predicate, cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default)
        => await Set().AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => await Set().AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

    // ─── Legacy methods (kept for backward compat) ────────────────────────

    public async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Set().AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => await Set().AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

    // ─── Write ────────────────────────────────────────────────────────────

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await Set().AddAsync(entity, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        => await Set().AddRangeAsync(entities, cancellationToken);

    public void Update(TEntity entity) => Set().Update(entity);
    public void UpdateRange(IEnumerable<TEntity> entities) => Set().UpdateRange(entities);

    public void Remove(TEntity entity) => Set().Remove(entity);
    public void RemoveRange(IEnumerable<TEntity> entities) => Set().RemoveRange(entities);
}

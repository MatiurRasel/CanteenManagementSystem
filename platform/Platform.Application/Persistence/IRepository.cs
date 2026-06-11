// =============================================================================
// IReadOnlyRepository<T> + IRepository<T>  (Platform.Application.Persistence)
// -----------------------------------------------------------------------------
// The single seam between application services and EF Core. Services depend
// on these interfaces — never on IAppDbContext or DbSet<T>. The concrete
// implementation lives in <see cref="CanteenManagementSystem.Infrastructure.Persistence.GenericRepository{T}"/>.
//
// SPLIT
//   IReadOnlyRepository<T>   — query-only surface. Use when the service only
//                              reads (reporting, analytics, validation checks).
//                              Makes intent obvious AND prevents accidental
//                              writes through a "read" service.
//   IRepository<T>           — full surface. Inherits the read methods + adds
//                              Add / Update / Remove. Use for repositories that
//                              participate in a UnitOfWork transaction.
//
// COMPOSITION
//   * Use IUnitOfWork.Repository&lt;T&gt;() to get an IRepository&lt;T&gt; from a
//     long-lived UoW (recommended — the UoW caches one repo per entity type
//     and shares the same DbContext, so changes commit atomically on SaveChanges).
//   * Inject IRepository&lt;T&gt; directly when you only need that one type
//     (also wired in DI). Saves typing.
//
// QUERY COMPOSITION
//   Most reads use the typed helpers (AnyAsync, FirstOrDefaultAsync, ListAsync).
//   For shapes the helpers can't express (joins, group-by, projections),
//   call Query() / NoTrackingQuery() and continue with the LINQ vocabulary.
//   Either is fine — they're just shortcuts for the same underlying queryable.
// =============================================================================

using System.Linq.Expressions;

namespace Platform.Application.Persistence;

public interface IReadOnlyRepository<TEntity> where TEntity : class
{
    /// <summary>Tracked queryable. Prefer NoTrackingQuery() for pure reads — faster, lower allocations.</summary>
    IQueryable<TEntity> Query();

    /// <summary>AsNoTracking() queryable — the right choice for projections, reports, validation reads.</summary>
    IQueryable<TEntity> NoTrackingQuery();

    /// <summary>Lookup by primary-key value(s). Composite keys pass multiple values in the array.</summary>
    Task<TEntity?> GetByIdAsync(object[] keyValues, CancellationToken cancellationToken = default);

    /// <summary>Short-hand for single-PK lookups: <c>repo.GetByIdAsync(42)</c>.</summary>
    Task<TEntity?> GetByIdAsync(object keyValue, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    // ─── Legacy method names (pre-Batch-1). Keep callers working; new code  ──
    //     should prefer ListAsync.                                            ──
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}

public interface IRepository<TEntity> : IReadOnlyRepository<TEntity> where TEntity : class
{
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    void Update(TEntity entity);
    void UpdateRange(IEnumerable<TEntity> entities);

    void Remove(TEntity entity);
    void RemoveRange(IEnumerable<TEntity> entities);
}

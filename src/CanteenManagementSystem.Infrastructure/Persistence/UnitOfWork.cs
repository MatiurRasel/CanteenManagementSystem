using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly IAppDbContext _dbContext;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        var entityType = typeof(TEntity);

        if (_repositories.TryGetValue(entityType, out var repository))
        {
            return (IRepository<TEntity>)repository;
        }

        var createdRepository = new GenericRepository<TEntity>(_dbContext);
        _repositories[entityType] = createdRepository;
        return createdRepository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public void Dispose()
    {
        if (_dbContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

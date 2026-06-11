using Platform.Application.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Platform.Application.Dispatch.Behaviors;

/// Opens a transaction for every command. Queries pass through untouched. Any
/// exception aborts the transaction; success commits.
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(IAppDbContext dbContext, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICommand<TResponse>)
        {
            return await next();
        }

        // If an outer transaction is already in progress (e.g. nested handler),
        // do not double-wrap.
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await next();
        }

        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var response = await next();
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rolling back transaction for {RequestType}", typeof(TRequest).Name);
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}

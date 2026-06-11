namespace Platform.Application.Dispatch;

/// Marker base for any dispatched request. Concrete intents implement
/// <see cref="ICommand{TResponse}"/> (writes) or <see cref="IQuery{TResponse}"/>
/// (reads) so the dispatcher and pipeline behaviors can treat them differently.
public interface IRequest<out TResponse>;

/// Write-side intent. Behaviors that participate in transactions, idempotency,
/// and audit run only for commands.
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// Read-side intent. Validation + logging behaviors still run, but no
/// transaction is started.
public interface IQuery<out TResponse> : IRequest<TResponse>;

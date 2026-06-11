namespace Platform.Application.Dispatch;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// Behavior wraps the handler invocation. Registered open-generic so behaviors
/// run in DI-resolution order around every dispatched request.
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

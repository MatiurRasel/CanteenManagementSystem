namespace Platform.Application.Dispatch;

/// Single seam for invoking a command or query. Controllers depend on
/// <see cref="IDispatcher"/>, not on individual handlers, so behaviors apply
/// uniformly across the application.
public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

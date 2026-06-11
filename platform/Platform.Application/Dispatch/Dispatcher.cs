using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Application.Dispatch;

/// Reflection-cached dispatcher. Resolves the matching handler from the DI
/// container, wraps it in the registered pipeline behaviors, and runs the
/// pipeline from outermost behavior inwards. Reflection is done once per
/// (TRequest, TResponse) pair and cached.
public sealed class Dispatcher : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, InvokerCache> _invokers = new();

    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var cache = _invokers.GetOrAdd(requestType, t => new InvokerCache(t, typeof(TResponse)));

        return cache.Invoke<TResponse>(_serviceProvider, request, cancellationToken);
    }

    private sealed class InvokerCache
    {
        private readonly MethodInfo _invokeMethod;
        private readonly Type _handlerType;
        private readonly Type _behaviorType;

        public InvokerCache(Type requestType, Type responseType)
        {
            _handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
            _behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
            _invokeMethod = typeof(InvokerCache)
                .GetMethod(nameof(InvokeCore), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(requestType, responseType);
        }

        public Task<TResponse> Invoke<TResponse>(IServiceProvider sp, object request, CancellationToken cancellationToken)
            => (Task<TResponse>)_invokeMethod.Invoke(this, new[] { sp, request, cancellationToken })!;

        private Task<TResponse> InvokeCore<TRequest, TResponse>(IServiceProvider sp, TRequest request, CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
        {
            var handler = (IRequestHandler<TRequest, TResponse>)sp.GetRequiredService(_handlerType);
            var behaviors = sp.GetServices(_behaviorType)
                .Cast<IPipelineBehavior<TRequest, TResponse>>()
                .Reverse()
                .ToArray();

            RequestHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(request, cancellationToken);
            foreach (var behavior in behaviors)
            {
                var next = pipeline;
                pipeline = () => behavior.HandleAsync(request, next, cancellationToken);
            }

            return pipeline();
        }
    }
}

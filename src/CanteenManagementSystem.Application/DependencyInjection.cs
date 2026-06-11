using System.Reflection;
using Platform.Application.Configuration;
using Platform.Application.Dispatch;
using Platform.Application.Dispatch.Behaviors;
using CanteenManagementSystem.Application.Menus;
using CanteenManagementSystem.Application.Operators;
using CanteenManagementSystem.Application.Tenancy;
using CanteenManagementSystem.Application.Verifications;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanteenManagementSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCanteenApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CanteenConfiguration>().BindConfiguration("CanteenSettings");
        services.AddOptions<BrandingConfiguration>().BindConfiguration("Branding");
        services.AddOptions<TenancyOptions>().BindConfiguration("Tenancy");

        services.AddMemoryCache();

        // CQRS dispatcher + pipeline behaviors.
        // Order matters: outermost behavior is registered first.
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        // Auto-register every FluentValidation validator in this assembly.
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<IClientCacheService, ClientCacheService>();
        services.AddScoped<IMenuManagementService, MenuManagementService>();
        services.AddScoped<IVerificationQueryService, VerificationQueryService>();
        services.AddScoped<IOperatorQueryService, OperatorQueryService>();

        return services;
    }
}

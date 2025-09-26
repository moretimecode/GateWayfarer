using Infrastructure.Endpoints;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EndPoints;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEndpointServices(this IServiceCollection services)
    { 
        services.AddSingleton<RegexMatcher>();

        services.AddSingleton<RouteToEndpointMapper>();
        services.AddSingleton<DynamicEndpointManager>();
        services.AddSingleton<IDynamicEndpointManager>(provider => provider.GetRequiredService<DynamicEndpointManager>());

        services.AddSingleton<RouteConfigurationEventPublisher>();

        services.AddSingleton<IHotUpdateCoordinator, HotUpdateCoordinator>();

        services.AddScoped<IEndpointHandler, UnifiedEndpointHandler>();
        services.AddScoped<IEndpointHandler, RegexEndpointHandler>();
        services.AddScoped<UnifiedEndpointHandler>();
        services.AddScoped<RegexEndpointHandler>();

        services.AddScoped<IEndpointHandlerFactory, EndpointHandlerFactory>();

        services.AddSingleton<IStartupEndpointRegistrar, StartupEndpointRegistrar>();

        return services;
    }
}

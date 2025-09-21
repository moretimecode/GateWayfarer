using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services.LoadBalancing;
using Infrastructure.Routes.LoadBalance;
using Infrastructure.Routes.ParameterExtractors;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Routes
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRouteServices(this IServiceCollection services)
        {
            services.AddKeyedScoped<ILoadBalanceStrategy, RoundRobinStrategy>(LoadBalanceType.RoundRobin);
            services.AddKeyedScoped<ILoadBalanceStrategy, WeightedRoundRobinStrategy>(LoadBalanceType.WeightedRoundRobin);
            services.AddKeyedScoped<ILoadBalanceStrategy, RandomStrategy>(LoadBalanceType.Random);
            services.AddKeyedScoped<ILoadBalanceStrategy, WeightedRandomStrategy>(LoadBalanceType.WeightedRandom);
            services.AddKeyedScoped<ILoadBalanceStrategy, LeastConnectionsStrategy>(LoadBalanceType.LeastConnections);
            services.AddKeyedScoped<ILoadBalanceStrategy, IpHashStrategy>(LoadBalanceType.IpHash);
            services.AddKeyedScoped<ILoadBalanceStrategy, ConsistentHashLoadBalancer>(LoadBalanceType.ConsistentHash);

            services.AddKeyedScoped<IRouteParameterExtractor, PrefixParameterExtractor>(RouteMatchType.Prefix);
            services.AddKeyedScoped<IRouteParameterExtractor, ExactParameterExtractor>(RouteMatchType.Exact);
            services.AddKeyedScoped<IRouteParameterExtractor, RegexParameterExtractor>(RouteMatchType.Regex);

            services.AddScoped<IRouteMatchingService, RouteMatchingService>();
            services.AddScoped<IRouteValidationService, RouteValidationService>();

            return services;
        }
    }
}
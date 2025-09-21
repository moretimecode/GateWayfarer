using Domain.Routes.Contexts;
using Domain.Routes.Services;
using Domain.Services;
using Domain.Services.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Domain.Routes;

namespace Infrastructure.Routes
{
    public class RouteMatchingService : IRouteMatchingService
    {
        private readonly IRouteRepository _routeRepository;
        private readonly IServiceRepository _serviceRepository;
        private readonly IHealthCalculationService _healthCalculationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RouteMatchingService> _logger;

        public RouteMatchingService(
            IRouteRepository routeRepository,
            IServiceRepository serviceRepository,
            IHealthCalculationService healthCalculationService,
            IServiceProvider serviceProvider,
            ILogger<RouteMatchingService> logger)
        {
            _routeRepository = routeRepository;
            _serviceRepository = serviceRepository;
            _healthCalculationService = healthCalculationService;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<RouteMatchResult?> MatchAsync(GatewayRequest request)
        {
            _logger.LogDebug("Starting route matching for request: {Request}", request);

            try
            {
                var routes = await _routeRepository.GetEnabledRoutesOrderedByPriorityAsync();
                _logger.LogDebug("Found {RouteCount} enabled routes for request: {RequestId}", routes.Count, request.RequestId);

                var matchedRoute = FindMatchingRoute(routes, request);
                if (matchedRoute == null)
                {
                    _logger.LogWarning("No route matched for request: {Request}", request);
                    return null;
                }

                _logger.LogDebug("Route matched: {RouteName} for request: {RequestId}", matchedRoute.Name, request.RequestId);

                var service = await _serviceRepository.GetByCodeAsync(matchedRoute.ServiceCode);
                if (service == null)
                {
                    _logger.LogError("Service not found for code: {ServiceCode}, request: {RequestId}", 
                        matchedRoute.ServiceCode, request.RequestId);
                    return null;
                }

                var healthyDestinations =  service.GetHealthyDestinations(_healthCalculationService);
                if (!healthyDestinations.Any())
                {
                    _logger.LogWarning("No healthy destinations available for service: {ServiceCode}, request: {RequestId}", 
                        service.Code, request.RequestId);
                    return null;
                }

                _logger.LogDebug("Found {DestinationCount} healthy destinations for service: {ServiceCode}, request: {RequestId}",
                    healthyDestinations.Count, service.Code, request.RequestId);

                var selectedDestination = SelectDestination(healthyDestinations, matchedRoute.LoadBalanceType, request);

                var parameters = ExtractRouteParameters(matchedRoute, request);

                _logger.LogInformation("Route matching completed. Route: {RouteName}, Destination: {DestinationCode}, LoadBalance: {LoadBalanceType}, Request: {RequestId}",
                    matchedRoute.Name, selectedDestination.Code, matchedRoute.LoadBalanceType, request.RequestId);

                return new RouteMatchResult
                {
                    Route = matchedRoute,
                    SelectedDestination = selectedDestination,
                    Parameters = parameters
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during route matching for request: {Request}", request);
                return null;
            }
        }

        /// <summary>
        /// 查找匹配的路由
        /// </summary>
        private Domain.Routes.Route? FindMatchingRoute(List<Domain.Routes.Route> routes, GatewayRequest request)
        {
            foreach (var route in routes)
            {
                try
                {
                    if (route.IsMatch(request.Path, request.HttpMethod))
                    {
                        _logger.LogDebug("Route {RouteName} matched path {Path} with match type {MatchType}, request: {RequestId}",
                            route.Name, request.Path, route.MatchType, request.RequestId);
                        return route;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking route match for route: {RouteName}, request: {Request}",
                        route.Name, request);
                }
            }

            return null;
        }

        /// <summary>
        /// 使用负载均衡策略选择目标实例
        /// </summary>
        private Destination SelectDestination(List<Destination> destinations, Domain.Routes.LoadBalanceType loadBalanceType, GatewayRequest request)
        {
            try
            {
                var strategy = _serviceProvider.GetKeyedService<ILoadBalanceStrategy>(loadBalanceType);

                if (strategy == null)
                {
                    _logger.LogWarning("Load balance strategy not found for type: {LoadBalanceType}, using first destination, request: {RequestId}", 
                        loadBalanceType, request.RequestId);
                    return destinations.First();
                }

                var context = new LoadBalanceContext
                {
                    RequestPath = request.Path,
                    RequestId = request.RequestId,
                    ClientIp = request.ClientIp ?? string.Empty,
                    Metadata = request.Metadata.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => kvp.Value)
                };

                var selectedDestination = strategy.SelectDestination(destinations, context);

                _logger.LogDebug("Load balance strategy {StrategyType} selected destination: {DestinationCode}, request: {RequestId}",
                    loadBalanceType, selectedDestination.Code, request.RequestId);

                return selectedDestination;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in load balance selection for type: {LoadBalanceType}, using first destination, request: {RequestId}", 
                    loadBalanceType, request.RequestId);
                return destinations.First();
            }
        }

        /// <summary>
        /// 使用参数提取策略提取路由参数
        /// </summary>
        private RouteParameters ExtractRouteParameters(Domain.Routes.Route route, GatewayRequest request)
        {
            try
            {
                var extractor = _serviceProvider.GetKeyedService<IRouteParameterExtractor>(route.MatchType);

                if (extractor == null)
                {
                    _logger.LogWarning("Parameter extractor not found for match type: {MatchType}, request: {RequestId}", 
                        route.MatchType, request.RequestId);
                    return new RouteParameters(new Dictionary<string, string>());
                }

                var parameters = extractor.ExtractParameters(route.Path, request.Path);

                _logger.LogDebug("Extracted {ParameterCount} route parameters for route: {RouteName}, request: {RequestId}",
                    parameters.PathParameters.Count, route.Name, request.RequestId);

                return parameters;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting route parameters for route: {RouteName}, request: {Request}",
                    route.Name, request);
                return new RouteParameters(new Dictionary<string, string>());
            }
        }
    }
}
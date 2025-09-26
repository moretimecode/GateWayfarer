using Domain.Routes;
using Domain.Routes.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// Endpoint处理器工厂
    /// 根据路由配置选择合适的处理器
    /// </summary>
    public class EndpointHandlerFactory : IEndpointHandlerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public EndpointHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 根据路由配置创建合适的处理器
        /// </summary>
        public IEndpointHandler CreateHandler(RouteConfigItem routeConfig)
        {
            return routeConfig.MatchType switch
            {
                RouteMatchType.Regex => _serviceProvider.GetRequiredService<RegexEndpointHandler>(),
                RouteMatchType.Exact => _serviceProvider.GetRequiredService<UnifiedEndpointHandler>(),
                RouteMatchType.Prefix => _serviceProvider.GetRequiredService<UnifiedEndpointHandler>(),
                _ => _serviceProvider.GetRequiredService<UnifiedEndpointHandler>()
            };
        }

        /// <summary>
        /// 获取所有支持指定路由配置的处理器
        /// </summary>
        public IEnumerable<IEndpointHandler> GetSupportedHandlers(RouteConfigItem routeConfig)
        {
            var handlers = _serviceProvider.GetServices<IEndpointHandler>();
            return handlers.Where(h => h.CanHandle(routeConfig));
        }
    }

    /// <summary>
    /// Endpoint处理器工厂接口
    /// </summary>
    public interface IEndpointHandlerFactory
    {
        IEndpointHandler CreateHandler(RouteConfigItem routeConfig);
        IEnumerable<IEndpointHandler> GetSupportedHandlers(RouteConfigItem routeConfig);
    }
}
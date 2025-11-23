using System.Runtime.CompilerServices;
using Domain.Routes;
using Domain.Routes.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 路由配置到Endpoint的映射器
    /// 负责将RouteConfigItem转换为ASP.NET Core的RouteEndpoint
    /// 只处理Exact和Prefix路由，正则路由由RegexMatcher单独处理
    /// </summary>
    public class RouteToEndpointMapper(ILogger<RouteToEndpointMapper> logger)
    {
        private readonly ILogger<RouteToEndpointMapper> _logger = logger;

        /// <summary>
        /// 将RouteConfigItem转换为RouteEndpoint（不支持正则路由）
        /// </summary>
        public RouteEndpoint MapToEndpoint(RouteConfigItem routeConfig)
        {
            // 正则路由不应该到这里
            if (routeConfig.MatchType == RouteMatchType.Regex)
            {
                throw new InvalidOperationException(
                    $"Regex routes should not be processed by RouteToEndpointMapper. Route: {routeConfig.Name}");
            }

            // 1. 生成路由模板
            var routePattern = CreateRoutePattern(routeConfig);

            // 2. 创建请求处理委托
            var requestDelegate = CreateRequestDelegate(routeConfig);

            // 3. 创建元数据
            var metadata = CreateEndpointMetadata(routeConfig);

            // 4. 构建RouteEndpoint
            return new RouteEndpoint(
                requestDelegate: requestDelegate,
                routePattern: routePattern,
                order: routeConfig.Priority,
                metadata: metadata,
                displayName: $"Gateway_{routeConfig.Name}"
            );
        }

        /// <summary>
        /// 根据Route配置创建路由模式（简化版，不处理正则）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RoutePattern CreateRoutePattern(RouteConfigItem routeConfig)
        {
            var finalPattern = routeConfig.MatchType switch
            {
                RouteMatchType.Exact => routeConfig.Path,
                RouteMatchType.Prefix => routeConfig.Path.EndsWith('/')
                    ? $"{routeConfig.Path}{{**catchAll}}"
                    : $"{routeConfig.Path}/{{**catchAll}}",
                RouteMatchType.Regex => throw new InvalidOperationException("Regex routes not supported here"),
                _ => routeConfig.Path
            };

            try
            {
                var routePattern = RoutePatternFactory.Parse(finalPattern);

                _logger.LogDebug("Created route pattern: {Pattern} for route: {RouteName} (MatchType: {MatchType})",
                    finalPattern, routeConfig.Name, routeConfig.MatchType);

                return routePattern;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Invalid route pattern '{finalPattern}' for route '{routeConfig.Name}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 为RouteConfigItem创建请求处理委托
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RequestDelegate CreateRequestDelegate(RouteConfigItem routeConfig)
        {
            return async (HttpContext context) =>
            {
                var handlerFactory = context.RequestServices.GetRequiredService<IEndpointHandlerFactory>();
                var handler = handlerFactory.CreateHandler(routeConfig);
                await handler.HandleAsync(context, routeConfig);
            };
        }

        /// <summary>
        /// 创建Endpoint元数据（HTTP方法约束等）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private EndpointMetadataCollection CreateEndpointMetadata(RouteConfigItem routeConfig)
        {
            var metadata = new List<object>();

            // 1. 添加HTTP方法约束
            var httpMethods = ConvertToHttpMethodStrings(routeConfig.HttpMethods);
            if (httpMethods.Length > 0)
            {
                metadata.Add(new HttpMethodMetadata(httpMethods));
            }

            // 2. 添加路由名称标识
            metadata.Add(new EndpointNameMetadata($"Gateway_{routeConfig.Name}"));

            // 3. 添加自定义元数据，便于后续识别这是网关路由
            metadata.Add(new GatewayRouteMetadata(routeConfig));

            return new EndpointMetadataCollection(metadata);
        }

        /// <summary>
        /// 将Domain的HttpMethod枚举转换为字符串数组
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string[] ConvertToHttpMethodStrings(Domain.Routes.HttpMethod httpMethods)
        {
            var methods = new List<string>();

            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.GET))
                methods.Add("GET");
            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.POST))
                methods.Add("POST");
            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.PUT))
                methods.Add("PUT");
            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.DELETE))
                methods.Add("DELETE");
            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.PATCH))
                methods.Add("PATCH");
            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.HEAD))
                methods.Add("HEAD");
            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.OPTIONS))
                methods.Add("OPTIONS");

            _logger.LogDebug("Converted HTTP methods for route: [{Methods}]", string.Join(", ", methods));

            return methods.ToArray();
        }
    }

    /// <summary>
    /// 网关路由元数据，用于标识这是由网关创建的路由
    /// </summary>
    public class GatewayRouteMetadata
    {
        public RouteConfigItem RouteConfig { get; }

        public GatewayRouteMetadata(RouteConfigItem routeConfig)
        {
            RouteConfig = routeConfig ?? throw new ArgumentNullException(nameof(routeConfig));
        }
    }
}
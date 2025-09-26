using Domain.Routes;
using Domain.Routes.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 统一Endpoint处理器
    /// 处理精确匹配和前缀匹配的路由
    /// </summary>
    public class UnifiedEndpointHandler : IEndpointHandler
    {
        private readonly ILogger<UnifiedEndpointHandler> _logger;
        private readonly IServiceProvider _serviceProvider;

        public UnifiedEndpointHandler(
            ILogger<UnifiedEndpointHandler> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 处理HTTP请求
        /// </summary>
        public async Task HandleAsync(HttpContext context, RouteConfigItem routeConfig)
        {
            try
            {
                _logger.LogDebug("统一处理器处理请求: {RouteName}, Path: {Path}, MatchType: {MatchType}", 
                    routeConfig.Name, context.Request.Path, routeConfig.MatchType);

                // 验证HTTP方法
                if (!IsHttpMethodAllowed(context.Request.Method, routeConfig.HttpMethods))
                {
                    context.Response.StatusCode = 405; // Method Not Allowed
                    await context.Response.WriteAsync("Method Not Allowed");
                    return;
                }

                // 设置路由信息
                context.Items["RouteConfig"] = routeConfig;
                context.Items["RouteName"] = routeConfig.Name;
                context.Items["ServiceCode"] = routeConfig.ServiceCode;

                // 这里可以添加具体的业务逻辑
                // 比如：转发到后端服务、执行本地处理等

                // 临时响应（后续会替换为实际的业务逻辑）
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                
                var response = new
                {
                    success = true,
                    message = "Route handled successfully",
                    route = routeConfig.Name,
                    path = context.Request.Path.Value,
                    method = context.Request.Method,
                    serviceCode = routeConfig.ServiceCode,
                    matchType = routeConfig.MatchType.ToString(),
                    timestamp = DateTime.UtcNow
                };

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));

                _logger.LogInformation("✅ 成功处理请求: {RouteName}", routeConfig.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 统一处理器处理请求失败: {RouteName}", routeConfig.Name);
                
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("Internal Server Error");
                }
            }
        }

        /// <summary>
        /// 检查是否支持指定的路由配置
        /// </summary>
        public bool CanHandle(RouteConfigItem routeConfig)
        {
            return routeConfig.MatchType is RouteMatchType.Exact or RouteMatchType.Prefix;
        }

        /// <summary>
        /// 验证HTTP方法是否被允许
        /// </summary>
        private bool IsHttpMethodAllowed(string requestMethod, Domain.Routes.HttpMethod allowedMethods)
        {
            return requestMethod.ToUpperInvariant() switch
            {
                "GET" => allowedMethods.HasFlag(Domain.Routes.HttpMethod.GET),
                "POST" => allowedMethods.HasFlag(Domain.Routes.HttpMethod.POST),
                "PUT" => allowedMethods.HasFlag(Domain.Routes.HttpMethod.PUT),
                "DELETE" => allowedMethods.HasFlag(Domain.Routes.HttpMethod.DELETE),
                "PATCH" => allowedMethods.HasFlag(Domain.Routes.HttpMethod.PATCH),
                "HEAD" => allowedMethods.HasFlag(Domain.Routes.HttpMethod.HEAD),
                "OPTIONS" => allowedMethods.HasFlag(Domain.Routes.HttpMethod.OPTIONS),
                _ => false
            };
        }
    }
}
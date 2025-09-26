using Domain.Routes;
using Domain.Routes.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 正则Endpoint处理器
    /// 处理正则匹配的路由
    /// </summary>
    public class RegexEndpointHandler : IEndpointHandler
    {
        private readonly ILogger<RegexEndpointHandler> _logger;
        private readonly RegexMatcher _regexMatcher;
        private readonly IServiceProvider _serviceProvider;

        public RegexEndpointHandler(
            ILogger<RegexEndpointHandler> logger,
            RegexMatcher regexMatcher,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _regexMatcher = regexMatcher;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 处理HTTP请求
        /// </summary>
        public async Task HandleAsync(HttpContext context, RouteConfigItem routeConfig)
        {
            try
            {
                _logger.LogDebug("正则处理器处理请求: {RouteName}, Path: {Path}, Pattern: {Pattern}", 
                    routeConfig.Name, context.Request.Path, routeConfig.Path);

                // 验证HTTP方法
                if (!IsHttpMethodAllowed(context.Request.Method, routeConfig.HttpMethods))
                {
                    context.Response.StatusCode = 405; // Method Not Allowed
                    await context.Response.WriteAsync("Method Not Allowed");
                    return;
                }

                // 执行正则匹配并提取参数
                var matchResult = _regexMatcher.TryMatch(context.Request.Path, routeConfig.Path);
                if (!(matchResult?.IsSuccess??false))
                {
                    _logger.LogWarning("正则匹配失败: {Path} 不匹配 {Pattern}", context.Request.Path, routeConfig.Path);
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("Not Found");
                    return;
                }

                // 设置路由信息和匹配参数
                context.Items["RouteConfig"] = routeConfig;
                context.Items["RouteName"] = routeConfig.Name;
                context.Items["ServiceCode"] = routeConfig.ServiceCode;
                context.Items["Parameters"] = matchResult.Parameters;
                context.Items["Match"] = matchResult.Match;


                // 这里可以添加具体的业务逻辑
                // 比如：转发到后端服务、执行本地处理等

                // 临时响应（后续会替换为实际的业务逻辑）
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                
                var response = new
                {
                    success = true,
                    message = "Regex route handled successfully",
                    route = routeConfig.Name,
                    path = context.Request.Path.Value,
                    method = context.Request.Method,
                    serviceCode = routeConfig.ServiceCode,
                    matchType = routeConfig.MatchType.ToString(),
                    regexPattern = routeConfig.Path,
                    timestamp = DateTime.UtcNow
                };

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));

                _logger.LogInformation("✅ 成功处理正则路由请求: {RouteName}, 捕获组: {GroupCount}", 
                    routeConfig.Name, matchResult.Parameters?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 正则处理器处理请求失败: {RouteName}", routeConfig.Name);
                
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
            return routeConfig.MatchType == RouteMatchType.Regex;
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
using Domain.Routes;
using Domain.Routes.Configuration;
using Domain.Routes.Contexts;
using Domain.Routes.Services;
using Infrastructure.Protocol;
using Infrastructure.Protocol.Streaming;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 正则Endpoint处理器
    /// 处理正则匹配的路由，并转发到后端服务
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

                // 1. 验证HTTP方法
                if (!IsHttpMethodAllowed(context.Request.Method, routeConfig.HttpMethods))
                {
                    context.Response.StatusCode = 405; // Method Not Allowed
                    await context.Response.WriteAsync("Method Not Allowed");
                    return;
                }

                // 2. 执行正则匹配并提取参数
                var matchResult = _regexMatcher.TryMatch(context.Request.Path, routeConfig.Path);
                if (!(matchResult?.IsSuccess ?? false))
                {
                    _logger.LogWarning("正则匹配失败: {Path} 不匹配 {Pattern}",
                        context.Request.Path, routeConfig.Path);
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("Not Found");
                    return;
                }

                // 3. 设置路由信息和匹配参数
                context.Items["RouteConfig"] = routeConfig;
                context.Items["RouteName"] = routeConfig.Name;
                context.Items["ServiceCode"] = routeConfig.ServiceCode;
                context.Items["RegexParameters"] = matchResult.Parameters; // 正则捕获的参数
                context.Items["RegexMatch"] = matchResult.Match;

                // 4. 转发请求到后端服务
                await ForwardRequestAsync(context, routeConfig, matchResult);

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
        /// 转发请求到后端服务
        /// </summary>
        private async Task ForwardRequestAsync(
            HttpContext context,
            RouteConfigItem routeConfig,
            RegexMatchResult matchResult)
        {
            // 1. 获取路由匹配服务（负责负载均衡和健康检查）
            var routeMatchingService = _serviceProvider.GetRequiredService<IRouteMatchingService>();

            // 2. 创建 GatewayRequest（使用扩展方法）
            var gatewayRequest = context.FromHttpContext();

            // 3. 将正则捕获的参数添加到 GatewayRequest（重要！）
            if (matchResult.Parameters != null)
            {
                foreach (var param in matchResult.Parameters)
                {
                    gatewayRequest.WithRouteParameter(param.Key, param.Value);
                }
            }

            // 4. 路由匹配
            var routeMatchResult = await routeMatchingService.MatchAsync(gatewayRequest);

            if (routeMatchResult == null)
            {
                _logger.LogWarning("未找到可用的目标服务: Route={RouteName}", routeConfig.Name);
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("No available destination");
                return;
            }

            // 5. 判断是否需要流式转发
            var streamingForwarder = _serviceProvider.GetService<IStreamingForwarder>();

            if (streamingForwarder != null && streamingForwarder.ShouldUseStreaming(context))
            {
                // 使用流式转发
                await ForwardWithStreamingAsync(context, routeMatchResult, streamingForwarder);
            }
            else
            {
                // 使用标准协议转发
                await ForwardWithProtocolAsync(context, routeMatchResult);
            }
        }

        /// <summary>
        /// 使用流式转发
        /// </summary>
        private async Task ForwardWithStreamingAsync(
            HttpContext context,
            RouteMatchResult routeMatchResult,
            IStreamingForwarder streamingForwarder)
        {
            _logger.LogInformation("🔄 使用流式转发 (Regex): Route={RouteName}, ContentLength={ContentLength}",
                routeMatchResult.Route.Name, context.Request.ContentLength);

            StreamingForwardingResponse? response = null;
            try
            {
                response = await streamingForwarder.ForwardAsync(
                    context,
                    routeMatchResult,
                    context.RequestAborted);

                if (response.IsSuccess)
                {
                    await response.WriteToHttpContextAsync(context, cancellationToken: context.RequestAborted);
                }
                else
                {
                    _logger.LogWarning("流式转发失败: StatusCode={StatusCode}, Error={Error}",
                        response.StatusCode, response.ErrorMessage);

                    context.Response.StatusCode = response.StatusCode;
                    await context.Response.WriteAsync(response.ErrorMessage ?? "Streaming forwarding failed");
                }
            }
            finally
            {
                if (response != null)
                {
                    await response.DisposeAsync();
                }
            }
        }

        /// <summary>
        /// 使用标准协议转发
        /// </summary>
        private async Task ForwardWithProtocolAsync(
            HttpContext context,
            RouteMatchResult routeMatchResult)
        {
            _logger.LogDebug("📡 使用标准协议转发 (Regex): Route={RouteName}",
                routeMatchResult.Route.Name);

            var protocolForwarder = _serviceProvider.GetService<IAdaptiveProtocolForwarder>();

            if (protocolForwarder == null)
            {
                // 如果没有注册协议转发器，返回临时响应（保持向后兼容）
                _logger.LogWarning("⚠️ 协议转发器未注册，返回临时响应");

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    success = true,
                    message = "Regex route handled successfully (no forwarder)",
                    route = routeMatchResult.Route.Name,
                    path = context.Request.Path.Value,
                    method = context.Request.Method,
                    serviceCode = routeMatchResult.Route.ServiceCode,
                    destination = routeMatchResult.SelectedDestination?.GetAddress() ?? "",
                    regexParameters = context.Items["RegexParameters"],
                    timestamp = DateTime.UtcNow
                };

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
                return;
            }

            ForwardingResponse? forwardingResponse = null;
            try
            {
                forwardingResponse = await protocolForwarder.ForwardToDestinationAsync(
                    context,
                    routeMatchResult,
                    context.RequestAborted);

                if (forwardingResponse.IsSuccess)
                {
                    await forwardingResponse.WriteToHttpContextAsync(context);
                }
                else
                {
                    _logger.LogWarning("标准转发失败: StatusCode={StatusCode}, Error={Error}",
                        forwardingResponse.StatusCode, forwardingResponse.ErrorMessage);

                    context.Response.StatusCode = forwardingResponse.StatusCode;
                    await context.Response.WriteAsync(forwardingResponse.ErrorMessage ?? "Forwarding failed");
                }
            }
            finally
            {
                if (forwardingResponse != null)
                {
                    await forwardingResponse.DisposeAsync();
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
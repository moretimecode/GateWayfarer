using Domain.Routes;
using Domain.Routes.Configuration;
using Domain.Routes.Contexts;
using Domain.Routes.Services;
using Infrastructure.Protocol;
using Infrastructure.Protocol.Streaming;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Domain.Authentication;
using Domain.Services;
using Domain.Services.Services;
using Infrastructure.Routes;
using System.Security.Claims;

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
        public async Task HandleAsync(Microsoft.AspNetCore.Http.HttpContext context, RouteConfigItem routeConfig)
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

                // 🔐 认证和授权检查
                var authResult = await PerformAuthenticationAsync(context);
                if (!authResult.IsAuthenticated && !routeConfig.AllowAnonymous)
                {
                    var authOptions = _serviceProvider.GetRequiredService<AuthenticationOptions>();
                    var policy = authOptions.Policies.GetValueOrDefault(routeConfig.AuthorizationPolicy ?? authOptions.DefaultAuthorizationPolicy);

                    if (policy?.RequireAuthentication == true)
                    {
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync("{\"error\":\"Unauthorized\",\"message\":\"Authentication is required\"}");
                        return;
                    }
                }

                if (authResult.IsAuthenticated && !routeConfig.AllowAnonymous && authResult.Principal != null)
                {
                    var authzResult = await PerformAuthorizationAsync(context, authResult.Principal, routeConfig);
                    if (!authzResult.IsAuthorized)
                    {
                        context.Response.StatusCode = 403;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync($"{{\"error\":\"Forbidden\",\"message\":\"{authzResult.FailureReason}\"}}");
                        return;
                    }
                }

                // ========== 转发逻辑 ==========
                await ForwardRequestAsync(context, routeConfig);

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
        /// 转发请求到后端服务
        /// </summary>
        private async Task ForwardRequestAsync(Microsoft.AspNetCore.Http.HttpContext context, RouteConfigItem routeConfig)
        {
            // 路由已找到，ServiceCode 已知 → 直接选择目标并转发
            var serviceRepository = _serviceProvider.GetRequiredService<IServiceRepository>();
            var healthCalcService = _serviceProvider.GetRequiredService<IHealthCalculationService>();
            var service = await serviceRepository.GetByCodeAsync(routeConfig.ServiceCode);

            if (service == null)
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("Service not found");
                return;
            }

            var healthyDests = service.GetHealthyDestinations(healthCalcService);
            if (!healthyDests.Any())
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("No healthy destinations");
                return;
            }

            // 负载均衡选择目标
            var strategy = _serviceProvider.GetKeyedService<ILoadBalanceStrategy>(routeConfig.LoadBalanceType);
            var selectedDest = strategy?.SelectDestination(healthyDests, new LoadBalanceContext
            {
                RequestPath = context.Request.Path,
                RequestId = context.TraceIdentifier,
                ClientIp = context.Connection.RemoteIpAddress?.ToString() ?? "",
                Metadata = new Dictionary<string, object>()
            }) ?? healthyDests.First();

            // 构建最小化的 RouteMatchResult 用于转发
            // 注：转发器主要用 SelectedDestination，Route.Name 用于日志
            var result = new RouteMatchResult
            {
                SelectedDestination = selectedDest,
                Parameters = new RouteParameters(),
                Route = Domain.Routes.Route.CreateMinimalRoute(routeConfig.Name)
            };

            // 转发
            var streamingForwarder = _serviceProvider.GetService<IStreamingForwarder>();

            if (streamingForwarder != null && streamingForwarder.ShouldUseStreaming(context))
            {
                await ForwardWithStreamingAsync(context, result, streamingForwarder);
            }
            else
            {
                await ForwardWithProtocolAsync(context, result);
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
            _logger.LogDebug("使用流式转发: ContentLength={ContentLength}", context.Request.ContentLength);

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
            _logger.LogDebug("使用标准协议转发");

            var protocolForwarder = _serviceProvider.GetService<IAdaptiveProtocolForwarder>();

            if (protocolForwarder == null)
            {
                // 如果没有注册协议转发器，返回临时响应（保持你原有的逻辑）
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    success = true,
                    message = "Route handled successfully",
                    route = routeMatchResult.Route.Name,
                    path = context.Request.Path.Value,
                    method = context.Request.Method,
                    serviceCode = routeMatchResult.Route.ServiceCode,
                    destination = routeMatchResult?.SelectedDestination?.GetAddress() ?? "",
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

        /// <summary>
        /// 执行认证
        /// </summary>
        private async Task<Domain.Authentication.AuthenticationResult> PerformAuthenticationAsync(HttpContext context)
        {
            try
            {
                var authService = _serviceProvider.GetRequiredService<IAuthenticationService>();
                var authOptions = _serviceProvider.GetRequiredService<AuthenticationOptions>();

                // 从Authorization Header提取Token
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(authHeader))
                {
                    _logger.LogDebug("⚠️ 未提供Authorization Header");
                    return Domain.Authentication.AuthenticationResult.Failure("No token provided");
                }

                // 提取Token
                var token = authService.ExtractToken(authHeader);
                if (string.IsNullOrWhiteSpace(token))
                {
                    _logger.LogWarning("❌ 无法从Authorization Header提取Token");
                    return Domain.Authentication.AuthenticationResult.Failure("Invalid token format");
                }

                // 验证Token
                var result = await authService.VerifyTokenAsync(token);
                if (result.IsAuthenticated && result.Principal != null)
                {
                    // 将Principal设置到HttpContext
                    context.User = result.Principal;
                    _logger.LogInformation("✅ 用户认证成功: {UserId}",
                        result.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
                }
                else
                {
                    _logger.LogWarning("❌ Token验证失败: {Reason}", result.FailureReason);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 认证过程发生异常");
                return Domain.Authentication.AuthenticationResult.Failure($"Authentication error: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行授权
        /// </summary>
        private async Task<Domain.Authentication.AuthorizationResult> PerformAuthorizationAsync(
            HttpContext context, ClaimsPrincipal user, RouteConfigItem routeConfig)
        {
            try
            {
                var authzService = _serviceProvider.GetRequiredService<IAuthorizationService>();
                var authOptions = _serviceProvider.GetRequiredService<AuthenticationOptions>();

                // 获取路由的授权策略
                var policyName = routeConfig.AuthorizationPolicy ?? authOptions.DefaultAuthorizationPolicy;
                var policy = authOptions.Policies.GetValueOrDefault(policyName);

                if (policy == null)
                {
                    _logger.LogWarning("⚠️ 未找到策略: {PolicyName}", policyName);
                    policy = authOptions.Policies.GetValueOrDefault(authOptions.DefaultAuthorizationPolicy);
                }

                // 执行授权检查
                var result = await authzService.CheckAuthorizationAsync(user, policy);

                if (result.IsAuthorized)
                {
                    _logger.LogInformation("✅ 授权检查通过: 路由 {RouteName} | 策略 {PolicyName}",
                        routeConfig.Name, policyName);
                }
                else
                {
                    _logger.LogWarning("❌ 授权检查失败: {Reason} | 路由 {RouteName} | 策略 {PolicyName}",
                        result.FailureReason, routeConfig.Name, policyName);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 授权过程发生异常");
                return Domain.Authentication.AuthorizationResult.Failure($"Authorization error: {ex.Message}");
            }
        }
    }
}
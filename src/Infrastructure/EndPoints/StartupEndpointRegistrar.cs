using Microsoft.Extensions.Options;
using Domain.Routes.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Domain.Routes;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 启动时Endpoint注册器的实现
    /// 分别处理普通路由和正则路由
    /// </summary>
    public class StartupEndpointRegistrar : IStartupEndpointRegistrar
    {
        private readonly IOptions<Domain.Routes.Configuration.RouteOptions> _routeOptions;
        private readonly RouteToEndpointMapper _endpointMapper;
        private readonly RegexMatcher _regexMatcher;
        private readonly ILogger<StartupEndpointRegistrar> _logger;
        private int _registeredEndpointCount;

        public int RegisteredEndpointCount => _registeredEndpointCount;

        public StartupEndpointRegistrar(
            IOptions<Domain.Routes.Configuration.RouteOptions> routeOptions,
            RouteToEndpointMapper endpointMapper,
            RegexMatcher regexMatcher,
            ILogger<StartupEndpointRegistrar> logger)
        {
            _routeOptions = routeOptions;
            _endpointMapper = endpointMapper;
            _regexMatcher = regexMatcher;
            _logger = logger;
        }

        /// <summary>
        /// 注册所有启用的Route为Endpoint
        /// </summary>
        public void RegisterAllEndpoints(IEndpointRouteBuilder endpointRouteBuilder)
        {
            var routes = _routeOptions.Value.Routes ?? new List<RouteConfigItem>();
            var enabledRoutes = routes.Where(r => r.IsEnabled).ToList();

            _logger.LogInformation("开始注册路由，总计 {TotalCount} 个启用的路由", enabledRoutes.Count);

            // 分离正则路由和普通路由
            var normalRoutes = enabledRoutes.Where(r => r.MatchType != RouteMatchType.Regex).ToList();
            var regexRoutes = enabledRoutes.Where(r => r.MatchType == RouteMatchType.Regex).ToList();

            _logger.LogInformation("路由分类: {NormalCount} 个普通路由, {RegexCount} 个正则路由",
                normalRoutes.Count, regexRoutes.Count);

            var totalRegistered = 0;

            // 1. 注册普通路由到ASP.NET Core
            totalRegistered += RegisterNormalRoutes(endpointRouteBuilder, normalRoutes);

            // 2. 预编译正则路由
            if (regexRoutes.Count > 0)
            {
                totalRegistered += RegisterRegexRoutes(endpointRouteBuilder, regexRoutes);
            }

            _registeredEndpointCount = totalRegistered;

            _logger.LogInformation("路由注册完成: 共注册 {TotalRegistered} 个endpoint", totalRegistered);
        }

        /// <summary>
        /// 注册普通路由（Exact和Prefix）
        /// </summary>
        private int RegisterNormalRoutes(IEndpointRouteBuilder endpointRouteBuilder, List<RouteConfigItem> normalRoutes)
        {
            var successCount = 0;
            var errorCount = 0;

            foreach (var routeConfig in normalRoutes)
            {
                try
                {
                    // 转换为Endpoint
                    var endpoint = _endpointMapper.MapToEndpoint(routeConfig);

                    // 注册到ASP.NET Core
                    endpointRouteBuilder.DataSources.Add(new DefaultEndpointDataSource(endpoint));

                    successCount++;
                    _logger.LogDebug("注册普通路由: {RouteName} -> {Pattern} (Priority: {Priority})",
                        routeConfig.Name, routeConfig.Path, routeConfig.Priority);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex, "注册普通路由失败: {RouteName}", routeConfig.Name);
                }
            }

            _logger.LogInformation("普通路由注册完成: 成功 {SuccessCount}, 失败 {ErrorCount}", successCount, errorCount);

            if (errorCount > 0)
            {
                throw new InvalidOperationException($"Failed to register {errorCount} normal routes");
            }

            return successCount;
        }

        /// <summary>
        /// 注册正则路由
        /// </summary>
        private int RegisterRegexRoutes(IEndpointRouteBuilder endpointRouteBuilder, List<RouteConfigItem> regexRoutes)
        {
            try
            {
                // 预编译所有正则路由
                _regexMatcher.PrecompileRegexRoutes(regexRoutes);

                // 注册一个全局的catch-all endpoint来处理所有正则路由
                endpointRouteBuilder.Map("{**path}", async context =>
                {
                    try
                    {
                        var logger = context.RequestServices.GetRequiredService<ILogger<StartupEndpointRegistrar>>();
                        var requestPath = context.Request.Path.Value ?? "";

                        logger.LogDebug("正则路由catch-all处理请求: {Path}", requestPath);

                        // 遍历所有正则路由，找到第一个匹配的
                        foreach (var regexRoute in regexRoutes)
                        {
                            var matchResult = _regexMatcher.TryMatch(requestPath, regexRoute.Path);
                            if (matchResult?.IsSuccess == true)
                            {
                                logger.LogDebug("找到匹配的正则路由: {RouteName} -> {Pattern}",
                                    regexRoute.Name, regexRoute.Path);

                                // 找到匹配的路由，使用处理器处理
                                var regexHandler = context.RequestServices.GetRequiredService<RegexEndpointHandler>();
                                await regexHandler.HandleAsync(context, regexRoute);
                                return;
                            }
                        }

                        // 没有匹配的正则路由
                        logger.LogDebug("没有找到匹配的正则路由: {Path}", requestPath);
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsync("Not Found");
                    }
                    catch (Exception ex)
                    {
                        var logger = context.RequestServices.GetRequiredService<ILogger<StartupEndpointRegistrar>>();
                        logger.LogError(ex, "正则路由catch-all处理器发生异常");

                        if (!context.Response.HasStarted)
                        {
                            context.Response.StatusCode = 500;
                            await context.Response.WriteAsync("Internal Server Error");
                        }
                    }
                })
                .WithOrder(int.MaxValue) // 最低优先级，确保在所有普通路由之后匹配
                .WithDisplayName("RegexRouteCatchAll")
                .WithMetadata(new EndpointNameMetadata("RegexRouteCatchAll"));

                _logger.LogInformation("正则路由注册完成: 预编译 {RegexCount} 个正则路由，注册了全局catch-all处理器",
                    regexRoutes.Count);

                return 1; // 返回1个endpoint（catch-all处理器）
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "注册正则路由失败");
                throw;
            }
        }
    }
}
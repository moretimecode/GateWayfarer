using Domain.Routes.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 路由代理处理器
    /// 类似 YARP 的 ReverseProxyMiddleware，处理动态 Endpoint 的请求转发
    /// 重点：自动处理路径转换，类似 YARP 的 Transform
    /// </summary>
    public class RouteProxyRequestHandler
    {
        private readonly ILogger<RouteProxyRequestHandler> _logger;

        public RouteProxyRequestHandler(ILogger<RouteProxyRequestHandler> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 创建请求委托（Endpoint 的核心处理逻辑）
        /// </summary>
        public RequestDelegate CreateRequestDelegate(RouteConfigItem routeConfig)
        {
            return async (HttpContext context) =>
            {
                try
                {
                    _logger.LogDebug(
                        "🔀 代理处理: Route={RouteName}, Path={Path}, MatchType={MatchType}",
                        routeConfig.Name, context.Request.Path, routeConfig.MatchType);

                    // 1️⃣ 路径转换（关键！类似 YARP 的 Transform）
                    var transformedPath = context.GetTransformedPath(routeConfig.Path);
                    _logger.LogDebug(
                        "📍 路径转换: 原始={Original}, 配置前缀={Prefix}, 转换后={Transformed}",
                        context.Request.Path, routeConfig.Path, transformedPath);

                    // 2️⃣ 获取通配符捕获的参数
                    var capturedPath = context.GetCapturedPath();
                    _logger.LogDebug("🎯 捕获路径: {CapturedPath}", capturedPath ?? "(empty)");

                    // 3️⃣ 使用处理器工厂选择合适的处理器
                    var handlerFactory = context.RequestServices.GetRequiredService<IEndpointHandlerFactory>();
                    var handler = handlerFactory.CreateHandler(routeConfig);

                    // 4️⃣ 在 HttpContext 中设置转换后的路径信息
                    context.Items["RouteConfig"] = routeConfig;
                    context.Items["RouteName"] = routeConfig.Name;
                    context.Items["OriginalPath"] = context.Request.Path.Value;
                    context.Items["TransformedPath"] = transformedPath;  // 新增：转换后的路径
                    context.Items["CapturedPath"] = capturedPath;        // 新增：捕获的部分
                    context.Items["MatchType"] = routeConfig.MatchType;

                    _logger.LogInformation(
                        "▶️ 执行处理器: Type={HandlerType}, Route={RouteName}",
                        handler.GetType().Name, routeConfig.Name);

                    // 5️⃣ 执行处理
                    await handler.HandleAsync(context, routeConfig);

                    _logger.LogInformation(
                        "✅ 处理完成: Route={RouteName}, StatusCode={StatusCode}",
                        routeConfig.Name, context.Response.StatusCode);
                }
                catch (Exception ex)
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<RouteProxyRequestHandler>>();
                    logger.LogError(ex, "❌ 代理处理失败: RouteName={RouteName}, Path={Path}",
                        routeConfig.Name, context.Request.Path);

                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("Internal Server Error");
                    }
                }
            };
        }

        /// <summary>
        /// 为前缀匹配创建带路径转换的委托
        /// </summary>
        public RequestDelegate CreatePrefixMatchDelegate(RouteConfigItem routeConfig)
        {
            return async (HttpContext context) =>
            {
                try
                {
                    // 对于 Prefix 匹配，自动移除前缀
                    // 例如：
                    //   配置：Path = /api
                    //   请求：GET /api/users
                    //   捕获：users
                    //   转发到后端：/users （前缀已移除）

                    var capturedPath = context.GetCapturedPath();
                    var forwardingPath = string.IsNullOrEmpty(capturedPath)
                        ? "/"
                        : $"/{capturedPath}";

                    _logger.LogDebug(
                        "📤 Prefix 转换: 配置={Config}, 捕获={Captured}, 转发路径={Forward}",
                        routeConfig.Path, capturedPath, forwardingPath);

                    context.Items["RouteConfig"] = routeConfig;
                    context.Items["RouteName"] = routeConfig.Name;
                    context.Items["OriginalPath"] = context.Request.Path.Value;
                    context.Items["TransformedPath"] = forwardingPath;
                    context.Items["CapturedPath"] = capturedPath;

                    var handlerFactory = context.RequestServices.GetRequiredService<IEndpointHandlerFactory>();
                    var handler = handlerFactory.CreateHandler(routeConfig);

                    await handler.HandleAsync(context, routeConfig);
                }
                catch (Exception ex)
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<RouteProxyRequestHandler>>();
                    logger.LogError(ex, "❌ Prefix 转换失败: Route={Route}", routeConfig.Name);

                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("Internal Server Error");
                    }
                }
            };
        }

        /// <summary>
        /// 为精确匹配创建委托（无路径转换）
        /// </summary>
        public RequestDelegate CreateExactMatchDelegate(RouteConfigItem routeConfig)
        {
            return async (HttpContext context) =>
            {
                try
                {
                    // 精确匹配：不需要路径转换，直接使用原始路径
                    _logger.LogDebug(
                        "📌 Exact 匹配: Route={Route}, Path={Path}",
                        routeConfig.Name, context.Request.Path);

                    context.Items["RouteConfig"] = routeConfig;
                    context.Items["RouteName"] = routeConfig.Name;
                    context.Items["OriginalPath"] = context.Request.Path.Value;
                    context.Items["TransformedPath"] = context.Request.Path.Value;

                    var handlerFactory = context.RequestServices.GetRequiredService<IEndpointHandlerFactory>();
                    var handler = handlerFactory.CreateHandler(routeConfig);

                    await handler.HandleAsync(context, routeConfig);
                }
                catch (Exception ex)
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<RouteProxyRequestHandler>>();
                    logger.LogError(ex, "❌ Exact 匹配处理失败: Route={Route}", routeConfig.Name);

                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("Internal Server Error");
                    }
                }
            };
        }
    }
}

using Domain.Routes;
using Domain.Routes.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 动态Endpoint管理器实现
    /// 基于ASP.NET Core的EndpointDataSource实现动态路由管理
    /// </summary>
    public class DynamicEndpointManager : EndpointDataSource, IDynamicEndpointManager
    {
        private readonly ILogger<DynamicEndpointManager> _logger;

        // 存储动态创建的Endpoint
        private readonly ConcurrentDictionary<string, RouteEndpoint> _dynamicEndpoints;
        private readonly ConcurrentDictionary<string, DynamicEndpointInfo> _endpointInfos;

        // 用于通知路由表变更
        private readonly System.Threading.Lock _lock = new();
        private List<Endpoint> _endpoints;
        private IChangeToken? _changeToken;
        private CancellationTokenSource? _cancellationTokenSource;

        public override IReadOnlyList<Endpoint> Endpoints
        {
            get
            {
                lock (_lock)
                {
                    return _endpoints;
                }
            }
        }

        public DynamicEndpointManager(
            ILogger<DynamicEndpointManager> logger)
        {
            _logger = logger;

            _dynamicEndpoints = new ConcurrentDictionary<string, RouteEndpoint>();
            _endpointInfos = new ConcurrentDictionary<string, DynamicEndpointInfo>();
            _endpoints = [];

            _logger.LogInformation("动态Endpoint管理器已初始化");
        }

        /// <summary>
        /// 添加新的Endpoint
        /// </summary>
        public EndpointOperationResult AddEndpoint(RouteConfigItem routeConfig)
        {
            try
            {
                _logger.LogDebug("添加动态Endpoint: {RouteName} -> {Path}", routeConfig.Name, routeConfig.Path);

                // 检查是否已存在
                if (_dynamicEndpoints.ContainsKey(routeConfig.Name))
                {
                    return new EndpointOperationResult
                    {
                        Success = false,
                        Message = $"路由 {routeConfig.Name} 已存在",
                        Errors = ["路由名称重复"]
                    };
                }

                // 创建RouteEndpoint
                var endpoint = CreateRouteEndpoint(routeConfig);
                if (endpoint == null)
                {
                    return new EndpointOperationResult
                    {
                        Success = false,
                        Message = "创建Endpoint失败",
                        Errors = ["无法创建有效的RouteEndpoint"]
                    };
                }

                // 添加到集合
                _dynamicEndpoints[routeConfig.Name] = endpoint;

                // 记录Endpoint信息
                _endpointInfos[routeConfig.Name] = new DynamicEndpointInfo
                {
                    RouteName = routeConfig.Name,
                    Pattern = routeConfig.Path,
                    HttpMethods = routeConfig.HttpMethods.ToString(),
                    MatchType = routeConfig.MatchType,
                    ServiceCode = routeConfig.ServiceCode,
                    Priority = routeConfig.Priority,
                    IsEnabled = routeConfig.IsEnabled,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        ["RouteConfig"] = routeConfig,
                        ["EndpointType"] = "Dynamic"
                    }
                };

                // 刷新路由表
                RefreshEndpoints();

                _logger.LogInformation("✅ 成功添加动态Endpoint: {RouteName}", routeConfig.Name);

                return new EndpointOperationResult
                {
                    Success = true,
                    Message = "Endpoint添加成功",
                    EndpointId = routeConfig.Name
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 添加动态Endpoint失败: {RouteName}", routeConfig.Name);

                return new EndpointOperationResult
                {
                    Success = false,
                    Message = "添加Endpoint时发生异常",
                    Errors = [ex.Message]
                };
            }
        }

        /// <summary>
        /// 更新现有Endpoint
        /// </summary>
        public EndpointOperationResult UpdateEndpoint(string routeName, RouteConfigItem newConfig)
        {
            try
            {
                _logger.LogDebug("更新动态Endpoint: {RouteName}", routeName);

                // 检查是否存在
                if (!_dynamicEndpoints.ContainsKey(routeName))
                {
                    return new EndpointOperationResult
                    {
                        Success = false,
                        Message = $"路由 {routeName} 不存在",
                        Errors = ["路由不存在"]
                    };
                }

                // 先删除旧的
                var removeResult = RemoveEndpoint(routeName);
                if (!removeResult.Success)
                {
                    return removeResult;
                }

                // 再添加新的
                var addResult = AddEndpoint(newConfig);
                if (addResult.Success)
                {
                    _logger.LogInformation("✅ 成功更新动态Endpoint: {RouteName}", routeName);
                }

                return addResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 更新动态Endpoint失败: {RouteName}", routeName);

                return new EndpointOperationResult
                {
                    Success = false,
                    Message = "更新Endpoint时发生异常",
                    Errors = [ex.Message]
                };
            }
        }

        /// <summary>
        /// 删除Endpoint
        /// </summary>
        public EndpointOperationResult RemoveEndpoint(string routeName)
        {
            try
            {
                _logger.LogDebug("删除动态Endpoint: {RouteName}", routeName);

                // 从集合中移除
                var endpointRemoved = _dynamicEndpoints.TryRemove(routeName, out var endpoint);
                var infoRemoved = _endpointInfos.TryRemove(routeName, out var info);

                if (!endpointRemoved && !infoRemoved)
                {
                    return new EndpointOperationResult
                    {
                        Success = false,
                        Message = $"路由 {routeName} 不存在",
                        Errors = ["路由不存在"]
                    };
                }

                // 刷新路由表
                RefreshEndpoints();

                _logger.LogInformation("✅ 成功删除动态Endpoint: {RouteName}", routeName);

                return new EndpointOperationResult
                {
                    Success = true,
                    Message = "Endpoint删除成功",
                    EndpointId = routeName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 删除动态Endpoint失败: {RouteName}", routeName);

                return new EndpointOperationResult
                {
                    Success = false,
                    Message = "删除Endpoint时发生异常",
                    Errors = [ex.Message]
                };
            }
        }

        /// <summary>
        /// 清空所有动态Endpoint
        /// </summary>
        public EndpointOperationResult ClearAllEndpoints()
        {
            try
            {
                _logger.LogDebug("清空所有动态Endpoint，当前数量: {Count}", _dynamicEndpoints.Count);

                var count = _dynamicEndpoints.Count;

                _dynamicEndpoints.Clear();
                _endpointInfos.Clear();

                // 刷新路由表
                RefreshEndpoints();

                _logger.LogInformation("✅ 成功清空所有动态Endpoint，共清空 {Count} 个", count);

                return new EndpointOperationResult
                {
                    Success = true,
                    Message = $"成功清空 {count} 个Endpoint"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 清空动态Endpoint失败");

                return new EndpointOperationResult
                {
                    Success = false,
                    Message = "清空Endpoint时发生异常",
                    Errors = [ex.Message]
                };
            }
        }

        /// <summary>
        /// 获取所有Endpoint信息
        /// </summary>
        public List<DynamicEndpointInfo> GetAllEndpoints()
        {
            return [.. _endpointInfos.Values];
        }

        /// <summary>
        /// 检查Endpoint是否存在
        /// </summary>
        public bool Exists(string routeName)
        {
            return _dynamicEndpoints.ContainsKey(routeName);
        }

        /// <summary>
        /// 创建RouteEndpoint
        /// </summary>
        private RouteEndpoint? CreateRouteEndpoint(RouteConfigItem routeConfig)
        {
            try
            {
                // 根据匹配类型创建不同的路由模式
                var routePattern = CreateRoutePattern(routeConfig);
                if (routePattern == null)
                {
                    _logger.LogWarning("无法为路由 {RouteName} 创建有效的路由模式", routeConfig.Name);
                    return null;
                }

                // 创建请求委托
                var requestDelegate = CreateRequestDelegate(routeConfig);

                // 创建Endpoint元数据
                var metadata = CreateEndpointMetadata(routeConfig);

                // 创建RouteEndpoint
                var endpoint = new RouteEndpoint(
                    requestDelegate,
                    routePattern,
                    routeConfig.Priority,
                    new EndpointMetadataCollection(metadata),
                    routeConfig.Name);

                return endpoint;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建RouteEndpoint失败: {RouteName}", routeConfig.Name);
                return null;
            }
        }

        /// <summary>
        /// 创建路由模式
        /// </summary>
        private RoutePattern? CreateRoutePattern(RouteConfigItem routeConfig)
        {
            try
            {
                return routeConfig.MatchType switch
                {
                    RouteMatchType.Exact => RoutePatternFactory.Parse(routeConfig.Path),
                    RouteMatchType.Prefix => RoutePatternFactory.Parse($"{routeConfig.Path.TrimEnd('/')}{{**catch-all}}"),
                    RouteMatchType.Regex => RoutePatternFactory.Parse($"{{**path:regex({routeConfig.Path})}}"),
                    _ => RoutePatternFactory.Parse(routeConfig.Path)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建路由模式失败: {Path}, MatchType: {MatchType}",
                    routeConfig.Path, routeConfig.MatchType);
                return null;
            }
        }

        /// <summary>
        /// 创建请求委托
        /// </summary>
        private RequestDelegate CreateRequestDelegate(RouteConfigItem routeConfig)
        {
            return async (HttpContext context) =>
    {
        try
        {
            // 🔥 使用工厂模式选择处理器（推荐）
            var handlerFactory = context.RequestServices.GetRequiredService<IEndpointHandlerFactory>();
            var handler = handlerFactory.CreateHandler(routeConfig);

            context.Items["RouteConfig"] = routeConfig;
            context.Items["RouteName"] = routeConfig.Name;

            // 执行处理
            await handler.HandleAsync(context, routeConfig);
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<DynamicEndpointManager>>();
            logger.LogError(ex, "处理动态路由请求失败: {RouteName}, Path: {Path}",
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
        /// 创建Endpoint元数据
        /// </summary>
        private List<object> CreateEndpointMetadata(RouteConfigItem routeConfig)
        {
            var metadata = new List<object>();

            // HTTP方法约束
            var httpMethods = ParseHttpMethods(routeConfig.HttpMethods);
            if (httpMethods.Any())
            {
                metadata.Add(new HttpMethodMetadata(httpMethods));
            }

            // 自定义元数据
            metadata.Add(new RouteConfigMetadata(routeConfig));

            // 显示名称
            metadata.Add(new EndpointNameMetadata(routeConfig.Name));

            return metadata;
        }

        /// <summary>
        /// 解析HTTP方法
        /// </summary>
        private List<string> ParseHttpMethods(Domain.Routes.HttpMethod httpMethods)
        {
            var methods = new List<string>();

            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.GET)) methods.Add("GET");
            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.POST)) methods.Add("POST");
            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.PUT)) methods.Add("PUT");
            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.DELETE)) methods.Add("DELETE");
            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.PATCH)) methods.Add("PATCH");
            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.HEAD)) methods.Add("HEAD");
            if (httpMethods.HasFlag(Domain.Routes.HttpMethod.OPTIONS)) methods.Add("OPTIONS");

            return methods;
        }

        /// <summary>
        /// 刷新Endpoint列表并通知路由系统
        /// </summary>
        private void RefreshEndpoints()
        {
            lock (_lock)
            {
                // 更新Endpoint列表
                _endpoints = _dynamicEndpoints.Values.Cast<Endpoint>().ToList();

                // 触发变更通知
                var oldCancellationTokenSource = _cancellationTokenSource;
                _cancellationTokenSource = new CancellationTokenSource();
                _changeToken = new CancellationChangeToken(_cancellationTokenSource.Token);

                oldCancellationTokenSource?.Cancel();
                oldCancellationTokenSource?.Dispose();

                _logger.LogDebug("刷新了动态Endpoint列表，当前数量: {Count}", _endpoints.Count);
            }
        }

        public override IChangeToken GetChangeToken()
        {
            lock (_lock)
            {
                return _changeToken ??= new CancellationChangeToken(_cancellationTokenSource?.Token ?? default);
            }
        }
    }

    /// <summary>
    /// 路由配置元数据
    /// </summary>
    public class RouteConfigMetadata
    {
        public RouteConfigItem RouteConfig { get; }

        public RouteConfigMetadata(RouteConfigItem routeConfig)
        {
            RouteConfig = routeConfig;
        }
    }
}
using Domain.Routes.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 路由配置变更事件发布器
    /// </summary>
    public class RouteConfigurationEventPublisher : IDisposable
    {
        private readonly ILogger<RouteConfigurationEventPublisher> _logger;
        private readonly IDisposable? _subscription;
        private Domain.Routes.Configuration.RouteOptions? _lastOptions;

        /// <summary>
        /// 路由配置变更事件
        /// </summary>
        public event EventHandler<RouteConfigChangedEventArgs>? ConfigurationChanged;

        public RouteConfigurationEventPublisher(
            IOptionsMonitor<Domain.Routes.Configuration.RouteOptions> optionsMonitor,
            ILogger<RouteConfigurationEventPublisher> logger)
        {
            _logger = logger;
            _lastOptions = optionsMonitor.CurrentValue;
            _subscription = optionsMonitor.OnChange(OnConfigurationChanged);

            _logger.LogInformation("📡 路由配置事件发布器已初始化，当前 {Count} 个路由", 
                _lastOptions?.Routes?.Count ?? 0);
        }

        /// <summary>
        /// 配置变更回调处理
        /// </summary>
        private void OnConfigurationChanged(Domain.Routes.Configuration.RouteOptions newOptions, string? name)
        {
            try
            {
                _logger.LogDebug("检测到RouteOptions配置变更，配置节: {Section}", name ?? "ApiGateway:Routes");

                if (_lastOptions == null)
                {
                    _logger.LogInformation("初始化路由配置基线，共 {Count} 个路由", newOptions.Routes?.Count ?? 0);
                    _lastOptions = CloneOptions(newOptions);
                    return;
                }

                var changes = AnalyzeChanges(_lastOptions, newOptions);
                if (changes.Any())
                {
                    _logger.LogInformation("🔥 检测到 {Count} 个路由配置变更", changes.Count);

                    foreach (var change in changes)
                    {
                        _logger.LogInformation("  📋 路由变更: {RouteName} - {ChangeType} - {Description}",
                            change.RouteName, change.ChangeType, change.ChangeDescription);
                    }

                    // 发布配置变更事件
                    ConfigurationChanged?.Invoke(this, new RouteConfigChangedEventArgs
                    {
                        Changes = changes,
                        Timestamp = DateTime.UtcNow,
                        Source = "EFConfiguration",
                        OldConfiguration = _lastOptions,
                        NewConfiguration = newOptions
                    });
                }
                else
                {
                    _logger.LogTrace("路由配置无实质性变更");
                }

                _lastOptions = CloneOptions(newOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 处理路由配置变更时发生错误");
            }
        }

        /// <summary>
        /// 分析配置变更
        /// </summary>
        private List<RouteConfigChangeInfo> AnalyzeChanges(Domain.Routes.Configuration.RouteOptions oldOptions, Domain.Routes.Configuration.RouteOptions newOptions)
        {
            var changes = new List<RouteConfigChangeInfo>();

            var oldRoutes = oldOptions.Routes?.ToDictionary(r => r.Name) ?? new Dictionary<string, RouteConfigItem>();
            var newRoutes = newOptions.Routes?.ToDictionary(r => r.Name) ?? new Dictionary<string, RouteConfigItem>();

            _logger.LogDebug("分析路由变更: 原有 {OldCount} 个，当前 {NewCount} 个", oldRoutes.Count, newRoutes.Count);

            // 检测新增的路由
            foreach (var newRoute in newRoutes.Values)
            {
                if (!oldRoutes.ContainsKey(newRoute.Name))
                {
                    changes.Add(new RouteConfigChangeInfo
                    {
                        RouteName = newRoute.Name,
                        ChangeType = RouteChangeType.Added,
                        NewConfig = newRoute,
                        ChangeDescription = $"新增路由: {newRoute.Path} ({newRoute.MatchType})"
                    });
                }
            }

            // 检测删除的路由
            foreach (var oldRoute in oldRoutes.Values)
            {
                if (!newRoutes.ContainsKey(oldRoute.Name))
                {
                    changes.Add(new RouteConfigChangeInfo
                    {
                        RouteName = oldRoute.Name,
                        ChangeType = RouteChangeType.Removed,
                        OldConfig = oldRoute,
                        ChangeDescription = $"删除路由: {oldRoute.Path}"
                    });
                }
            }

            // 检测修改的路由
            foreach (var routeName in oldRoutes.Keys.Intersect(newRoutes.Keys))
            {
                var oldRoute = oldRoutes[routeName];
                var newRoute = newRoutes[routeName];

                if (!AreRoutesEqual(oldRoute, newRoute))
                {
                    var changeType = DetermineChangeType(oldRoute, newRoute);
                    var changeDescription = BuildChangeDescription(oldRoute, newRoute);

                    changes.Add(new RouteConfigChangeInfo
                    {
                        RouteName = routeName,
                        ChangeType = changeType,
                        OldConfig = oldRoute,
                        NewConfig = newRoute,
                        ChangeDescription = changeDescription
                    });
                }
            }

            return changes;
        }

        /// <summary>
        /// 比较两个路由配置是否相等
        /// </summary>
        private bool AreRoutesEqual(RouteConfigItem route1, RouteConfigItem route2)
        {
            return route1.Name == route2.Name &&
                   route1.Path == route2.Path &&
                   route1.MatchType == route2.MatchType &&
                   route1.HttpMethods == route2.HttpMethods &&
                   route1.IsEnabled == route2.IsEnabled &&
                   route1.Priority == route2.Priority &&
                   route1.ServiceCode == route2.ServiceCode;
        }

        /// <summary>
        /// 确定变更类型
        /// </summary>
        private RouteChangeType DetermineChangeType(RouteConfigItem oldRoute, RouteConfigItem newRoute)
        {
            if (oldRoute.IsEnabled != newRoute.IsEnabled)
            {
                return newRoute.IsEnabled ? RouteChangeType.Enabled : RouteChangeType.Disabled;
            }
            return RouteChangeType.Modified;
        }

        /// <summary>
        /// 构建变更描述
        /// </summary>
        private string BuildChangeDescription(RouteConfigItem oldRoute, RouteConfigItem newRoute)
        {
            var changes = new List<string>();

            if (oldRoute.Path != newRoute.Path)
                changes.Add($"路径: {oldRoute.Path} -> {newRoute.Path}");
            if (oldRoute.MatchType != newRoute.MatchType)
                changes.Add($"匹配类型: {oldRoute.MatchType} -> {newRoute.MatchType}");
            if (oldRoute.HttpMethods != newRoute.HttpMethods)
                changes.Add($"HTTP方法: {oldRoute.HttpMethods} -> {newRoute.HttpMethods}");
            if (oldRoute.IsEnabled != newRoute.IsEnabled)
                changes.Add($"启用状态: {oldRoute.IsEnabled} -> {newRoute.IsEnabled}");
            if (oldRoute.Priority != newRoute.Priority)
                changes.Add($"优先级: {oldRoute.Priority} -> {newRoute.Priority}");
            if (oldRoute.ServiceCode != newRoute.ServiceCode)
                changes.Add($"服务代码: {oldRoute.ServiceCode} -> {newRoute.ServiceCode}");

            return string.Join("; ", changes);
        }

        /// <summary>
        /// 深度克隆RouteOptions（避免引用问题）
        /// </summary>
        private Domain.Routes.Configuration.RouteOptions CloneOptions(Domain.Routes.Configuration.RouteOptions options)
        {
            return new Domain.Routes.Configuration.RouteOptions
            {
                Routes = options.Routes?.Select(r => new RouteConfigItem
                {
                    Name = r.Name,
                    Path = r.Path,
                    MatchType = r.MatchType,
                    HttpMethods = r.HttpMethods,
                    IsEnabled = r.IsEnabled,
                    Priority = r.Priority,
                    ServiceCode = r.ServiceCode,
                    LoadBalanceType = r.LoadBalanceType,
                }).ToList() ?? []
            };
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }

    /// <summary>
    /// 路由配置变更事件参数
    /// </summary>
    public class RouteConfigChangedEventArgs : EventArgs
    {
        public required List<RouteConfigChangeInfo> Changes { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Source { get; set; } = "EFConfiguration";
        public Domain.Routes.Configuration.RouteOptions? OldConfiguration { get; set; }
        public Domain.Routes.Configuration.RouteOptions? NewConfiguration { get; set; }
    }

    /// <summary>
    /// 路由配置变更信息
    /// </summary>
    public class RouteConfigChangeInfo
    {
        public required string RouteName { get; set; }
        public required RouteChangeType ChangeType { get; set; }
        public RouteConfigItem? OldConfig { get; set; }
        public RouteConfigItem? NewConfig { get; set; }
        public string ChangeDescription { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 路由变更类型
    /// </summary>
    public enum RouteChangeType
    {
        Added,
        Modified,
        Removed,
        Enabled,
        Disabled
    }
}
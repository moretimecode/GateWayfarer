using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Trace;

namespace Infrastructure.OpenTelemetry
{
    /// <summary>
    /// 网关自定义 Activity 创建和跟踪
    /// </summary>
    public static class GatewayActivitySource
    {
        /// <summary>
        /// 链路追踪的活动源
        /// </summary>
        public static readonly ActivitySource Routes = new("Gateway.Routes", "1.0.0");
        public static readonly ActivitySource Services = new("Gateway.Services", "1.0.0");
        public static readonly ActivitySource Protocol = new("Gateway.Protocol", "1.0.0");
        public static readonly ActivitySource HealthCheck = new("Gateway.HealthCheck", "1.0.0");
    }

    /// <summary>
    /// 网关性能指标记录
    /// </summary>
    public class GatewayMetrics
    {
        private readonly Meter _meter;
        public Counter<long> RequestCount { get; private set; }
        public Histogram<double> RequestDuration { get; private set; }
        public Counter<long> RequestErrors { get; private set; }
        public UpDownCounter<long> ActiveConnections { get; private set; }
        public Counter<long> RoutesMatched { get; private set; }
        public Counter<long> LoadBalanceOperations { get; private set; }
        public Histogram<double> HealthCheckDuration { get; private set; }
        public Counter<long> FailoverEvents { get; private set; }

        public GatewayMetrics()
        {
            _meter = new Meter("Gateway.Metrics", "1.0.0");

            // 请求相关指标
            RequestCount = _meter.CreateCounter<long>(
                "http.request.count",
                unit: "1",
                description: "HTTP 请求总数");

            RequestDuration = _meter.CreateHistogram<double>(
                "http.request.duration",
                unit: "ms",
                description: "HTTP 请求处理耗时");

            RequestErrors = _meter.CreateCounter<long>(
                "http.request.errors",
                unit: "1",
                description: "HTTP 请求错误总数");

            ActiveConnections = _meter.CreateUpDownCounter<long>(
                "http.active.connections",
                unit: "1",
                description: "当前活跃连接数");

            // 路由相关指标
            RoutesMatched = _meter.CreateCounter<long>(
                "gateway.routes.matched",
                unit: "1",
                description: "成功匹配的路由数");

            // 负载均衡相关指标
            LoadBalanceOperations = _meter.CreateCounter<long>(
                "gateway.load_balance.operations",
                unit: "1",
                description: "负载均衡操作总数");

            // 健康检查相关指标
            HealthCheckDuration = _meter.CreateHistogram<double>(
                "gateway.health_check.duration",
                unit: "ms",
                description: "健康检查耗时");

            FailoverEvents = _meter.CreateCounter<long>(
                "gateway.failover.events",
                unit: "1",
                description: "故障转移事件总数");
        }
    }

    /// <summary>
    /// Activity 扩展方法，用于添加网关特定的跟踪信息
    /// </summary>
    public static class ActivityExtensions
    {
        /// <summary>
        /// 添加路由信息
        /// </summary>
        public static Activity AddRouteInfo(this Activity? activity, string routeName, string routePath)
        {
            if (activity == null) return null;
            activity.SetTag("gateway.route.name", routeName);
            activity.SetTag("gateway.route.path", routePath);
            return activity;
        }

        /// <summary>
        /// 添加服务信息
        /// </summary>
        public static Activity AddServiceInfo(this Activity? activity, string serviceName, string targetUrl)
        {
            if (activity == null) return null;
            activity.SetTag("gateway.service.name", serviceName);
            activity.SetTag("gateway.service.target", targetUrl);
            return activity;
        }

        /// <summary>
        /// 添加负载均衡信息
        /// </summary>
        public static Activity AddLoadBalanceInfo(this Activity? activity, string strategy, string selectedInstance)
        {
            if (activity == null) return null;
            activity.SetTag("gateway.lb.strategy", strategy);
            activity.SetTag("gateway.lb.selected_instance", selectedInstance);
            return activity;
        }

        /// <summary>
        /// 添加健康检查结果
        /// </summary>
        public static Activity AddHealthCheckInfo(this Activity? activity, string serviceId, bool isHealthy, string reason = "")
        {
            if (activity == null) return null;
            activity.SetTag("gateway.health.service_id", serviceId);
            activity.SetTag("gateway.health.status", isHealthy ? "healthy" : "unhealthy");
            if (!string.IsNullOrEmpty(reason))
            {
                activity.SetTag("gateway.health.reason", reason);
            }
            return activity;
        }

        /// <summary>
        /// 添加请求转发信息
        /// </summary>
        public static Activity AddForwardingInfo(this Activity? activity, string method, string targetHost, int? responseStatus)
        {
            if (activity == null) return null;
            activity.SetTag("gateway.forward.method", method);
            activity.SetTag("gateway.forward.target_host", targetHost);
            if (responseStatus.HasValue)
            {
                activity.SetTag("gateway.forward.response_status", responseStatus);
            }
            return activity;
        }
    }
}

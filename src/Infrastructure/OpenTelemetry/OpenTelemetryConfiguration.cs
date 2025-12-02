using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Reflection;

namespace Infrastructure.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry 配置助手类
    /// 用于集成 Traces（链路追踪）和 Metrics（性能指标）
    /// </summary>
    public static class OpenTelemetryConfiguration
    {
        private const string ServiceName = "ApiGateway";
        private const string ServiceVersion = "1.0.0";

        /// <summary>
        /// 添加 OpenTelemetry 链路追踪和指标采集
        /// </summary>
        public static IServiceCollection ConfigureOpenTelemetry(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var otelConfig = configuration.GetSection("OpenTelemetry").Get<OpenTelemetryOptions>()
                ?? new OpenTelemetryOptions();

            // 配置资源信息
            var resource = ResourceBuilder
                .CreateDefault()
                .AddService(
                    serviceName: ServiceName,
                    serviceVersion: ServiceVersion,
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(new Dictionary<string, object>
                {
                    { "deployment.environment", GetEnvironment() },
                    { "host.name", Environment.MachineName },
                });

            var addOtelMethod = typeof(IServiceCollection)
                .GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "AddOpenTelemetry" && m.GetParameters().Length == 1);

            if (addOtelMethod != null)
            {
                var otBuilder = addOtelMethod.Invoke(null, new object[] { services }) as IOpenTelemetryBuilder;

                if (otBuilder != null)
                {
                    // 添加链路追踪
                    if (otelConfig.EnableTraces)
                    {
                        otBuilder.WithTracing(tracing =>
                        {
                            tracing
                                .SetResourceBuilder(resource)
                                .AddAspNetCoreInstrumentation(opts =>
                                {
                                    opts.Filter = (httpContext) =>
                                    {
                                        var path = httpContext.Request.Path.Value ?? "";
                                        return !path.Contains("/health")
                                            && !path.Contains("/swagger")
                                            && !path.Contains("/prometheus");
                                    };
                                    opts.RecordException = true;
                                    opts.EnrichWithHttpRequest = EnrichActivityWithHttpRequest;
                                    opts.EnrichWithHttpResponse = EnrichActivityWithHttpResponse;
                                })
                                .AddHttpClientInstrumentation(opts =>
                                {
                                    opts.RecordException = true;
                                })
                                .AddSource("Gateway.Routes", "Gateway.Services", "Gateway.Protocol", "Gateway.HealthCheck");

                            if (otelConfig.SigNozEnabled)
                            {
                                // SigNoz 通过 OTel Collector 支持 Jaeger 导出
                                // 配置 Jaeger 导出到 SigNoz 的 OTel Collector
                                tracing.AddJaegerExporter(opts =>
                                {
                                    opts.AgentHost = otelConfig.JaegerHost ?? "localhost";
                                    opts.AgentPort = otelConfig.JaegerPort;
                                    opts.MaxPayloadSizeInBytes = 4096;
                                });
                            }
                            else if (otelConfig.JaegerEnabled)
                            {
                                tracing.AddJaegerExporter(opts =>
                                {
                                    opts.AgentHost = otelConfig.JaegerHost ?? "localhost";
                                    opts.AgentPort = otelConfig.JaegerPort;
                                    opts.MaxPayloadSizeInBytes = 4096;
                                });
                            }
                        });
                    }

                    // 添加性能指标采集
                    if (otelConfig.EnableMetrics)
                    {
                        otBuilder.WithMetrics(metrics =>
                        {
                            metrics
                                .SetResourceBuilder(resource)
                                .AddHttpClientInstrumentation()
                                .AddRuntimeInstrumentation()
                                .AddProcessInstrumentation()
                                .AddMeter(
                                    "Gateway.Routes",
                                    "Gateway.Services",
                                    "Gateway.Protocol",
                                    "Gateway.HealthCheck");

                            if (otelConfig.PrometheusEnabled)
                            {
                                metrics.AddPrometheusExporter(opts =>
                                {
                                    opts.ScrapeResponseCacheDurationMilliseconds =
                                        otelConfig.PrometheusScrapeCacheDuration;
                                });
                            }
                        });
                    }
                }
            }

            return services;
        }

        /// <summary>
        /// 富化 Activity，添加 HTTP 请求信息
        /// </summary>
        private static void EnrichActivityWithHttpRequest(Activity activity, HttpRequest request)
        {
            activity.SetTag("http.request_content_length", request.ContentLength);
            activity.SetTag("http.scheme", request.Scheme);
            activity.SetTag("http.target", request.Path + request.QueryString);

            // 添加自定义标签
            if (request.Headers.TryGetValue("X-Request-ID", out var requestId))
            {
                activity.SetTag("request.id", requestId.ToString());
            }

            if (request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
            {
                activity.SetTag("correlation.id", correlationId.ToString());
            }
        }

        /// <summary>
        /// 富化 Activity，添加 HTTP 响应信息
        /// </summary>
        private static void EnrichActivityWithHttpResponse(Activity activity, HttpResponse response)
        {
            activity.SetTag("http.response_content_length", response.ContentLength);
        }

        /// <summary>
        /// 获取运行环境
        /// </summary>
        private static string GetEnvironment()
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "production";
        }
    }

    /// <summary>
    /// OpenTelemetry 配置选项
    /// </summary>
    public class OpenTelemetryOptions
    {
        /// <summary>
        /// 是否启用链路追踪
        /// </summary>
        public bool EnableTraces { get; set; } = true;

        /// <summary>
        /// 是否启用性能指标采集
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// 是否启用 SigNoz OTLP 导出器
        /// </summary>
        public bool SigNozEnabled { get; set; } = true;

        /// <summary>
        /// SigNoz OTLP 采集端点（gRPC）
        /// 默认：http://localhost:4317
        /// </summary>
        public string? SigNozOtlpEndpoint { get; set; } = "http://localhost:4317";

        /// <summary>
        /// 是否启用 Jaeger 导出器
        /// </summary>
        public bool JaegerEnabled { get; set; } = false;

        /// <summary>
        /// Jaeger Agent 主机地址
        /// </summary>
        public string? JaegerHost { get; set; } = "localhost";

        /// <summary>
        /// Jaeger Agent 端口
        /// </summary>
        public int JaegerPort { get; set; } = 6831;

        /// <summary>
        /// 是否启用 Prometheus 导出器
        /// </summary>
        public bool PrometheusEnabled { get; set; } = true;

        /// <summary>
        /// Prometheus 抓取端口
        /// </summary>
        public int PrometheusPort { get; set; } = 9090;

        /// <summary>
        /// Prometheus 抓取缓存持续时间（毫秒）
        /// </summary>
        public int PrometheusScrapeCacheDuration { get; set; } = 3000;

        /// <summary>
        /// 是否启用控制台导出器（调试用）
        /// </summary>
        public bool ConsoleExporterEnabled { get; set; } = false;
    }
}

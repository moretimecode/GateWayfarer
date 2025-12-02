using System.Diagnostics;
using Infrastructure.OpenTelemetry;

namespace Api.Middleware
{
    /// <summary>
    /// OpenTelemetry 中间件，用于记录 HTTP 请求和网关指标
    /// </summary>
    public class OpenTelemetryMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<OpenTelemetryMiddleware> _logger;

        public OpenTelemetryMiddleware(RequestDelegate next, ILogger<OpenTelemetryMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, GatewayMetrics metrics)
        {
            var requestStartTime = Stopwatch.GetTimestamp();
            var method = context.Request.Method;
            var path = context.Request.Path.Value ?? "unknown";

            // 增加活跃连接数
            metrics.ActiveConnections.Add(1);

            // 增加请求计数
            var tags = new KeyValuePair<string, object>[]
            {
                new("http.method", method),
                new("http.path", path),
                new("http.scheme", context.Request.Scheme),
            };
            metrics.RequestCount.Add(1, tags);

            try
            {
                await _next(context);

                var statusCode = context.Response.StatusCode;
                var elapsedMs = GetElapsedMilliseconds(requestStartTime);

                // 记录请求耗时
                metrics.RequestDuration.Record(elapsedMs, new KeyValuePair<string, object>[]
                {
                    new("http.method", method),
                    new("http.status_code", statusCode),
                    new("http.path", path),
                });

                _logger.LogInformation(
                    "请求完成: {Method} {Path} -> {StatusCode} ({Duration}ms)",
                    method, path, statusCode, elapsedMs);
            }
            catch (Exception ex)
            {
                metrics.RequestErrors.Add(1, new KeyValuePair<string, object>[]
                {
                    new("http.method", method),
                    new("http.path", path),
                    new("error.type", ex.GetType().Name),
                });

                _logger.LogError(ex, "请求处理异常: {Method} {Path}", method, path);
                throw;
            }
            finally
            {
                // 减少活跃连接数
                metrics.ActiveConnections.Add(-1);
            }
        }

        private static double GetElapsedMilliseconds(long startTimestamp)
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            return elapsed.TotalMilliseconds;
        }
    }

    /// <summary>
    /// OpenTelemetry 中间件扩展方法
    /// </summary>
    public static class OpenTelemetryMiddlewareExtensions
    {
        public static IApplicationBuilder UseOpenTelemetryMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<OpenTelemetryMiddleware>();
        }
    }
}

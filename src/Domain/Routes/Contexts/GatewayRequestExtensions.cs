using Microsoft.AspNetCore.Http;

namespace Domain.Routes.Contexts
{
    /// <summary>
    /// GatewayRequest的扩展方法
    /// </summary>
    public static class GatewayRequestExtensions
    {
        private const string RouteParamPrefix = "RouteParam:";

        /// <summary>
        /// 获取路由参数值
        /// </summary>
        /// <param name="request">网关请求</param>
        /// <param name="parameterName">参数名称</param>
        /// <returns>参数值，如果不存在则返回null</returns>
        public static string? GetRouteParameter(this GatewayRequest request, string parameterName)
        {
            var key = RouteParamPrefix + parameterName;
            return request.Metadata.TryGetValue(key, out var value) ? value?.ToString() : null;
        }

        /// <summary>
        /// 获取所有路由参数
        /// </summary>
        /// <param name="request">网关请求</param>
        /// <returns>路由参数字典</returns>
        public static Dictionary<string, string> GetAllRouteParameters(this GatewayRequest request)
        {
            return request.Metadata
                .Where(kv => kv.Key.StartsWith(RouteParamPrefix))
                .ToDictionary(
                    kv => kv.Key.Substring(RouteParamPrefix.Length),
                    kv => kv.Value?.ToString() ?? string.Empty
                );
        }

        /// <summary>
        /// 添加路由参数
        /// </summary>
        /// <param name="request">网关请求</param>
        /// <param name="parameterName">参数名称</param>
        /// <param name="value">参数值</param>
        /// <returns>GatewayRequest实例（支持链式调用）</returns>
        public static GatewayRequest WithRouteParameter(this GatewayRequest request, string parameterName, string value)
        {
            var key = RouteParamPrefix + parameterName;
            return request.WithMetadata(key, value);
        }

        /// <summary>
        /// 获取路由配置信息
        /// </summary>
        /// <param name="request">网关请求</param>
        /// <param name="configKey">配置键名</param>
        /// <returns>配置值</returns>
        public static T? GetRouteConfig<T>(this GatewayRequest request, string configKey)
        {
            var key = "Route:" + configKey;
            if (request.Metadata.TryGetValue(key, out var value))
            {
                if (value is T directValue)
                    return directValue;

                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default(T);
                }
            }
            return default(T);
        }

        /// <summary>
        /// 从 HttpContext 创建 GatewayRequest
        /// </summary>
        public static GatewayRequest FromHttpContext(this HttpContext context)
        {
            var request = context.Request;

            // 1. 解析 HTTP 方法
            var httpMethod = ParseHttpMethod(request.Method);

            // 2. 获取路径
            var path = request.Path.Value ?? "/";

            // 3. 创建 GatewayRequest
            var gatewayRequest = new GatewayRequest(path, httpMethod);

            // 4. 设置客户端IP
            gatewayRequest.WithClientIp(GetClientIp(context));

            // 5. 设置 UserAgent
            var userAgent = request.Headers["User-Agent"].ToString();
            if (!string.IsNullOrEmpty(userAgent))
            {
                gatewayRequest.WithUserAgent(userAgent);
            }

            // 6. 设置请求ID（优先使用现有的追踪ID）
            var requestId = request.Headers["X-Request-ID"].FirstOrDefault()
                           ?? request.Headers["X-Correlation-ID"].FirstOrDefault()
                           ?? context.TraceIdentifier
                           ?? Guid.NewGuid().ToString();
            gatewayRequest.WithRequestId(requestId);

            // 7. 复制请求头
            var headers = new Dictionary<string, string>();
            foreach (var header in request.Headers)
            {
                // 只复制单值头
                var value = header.Value.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    headers[header.Key] = value;
                }
            }
            gatewayRequest.WithHeaders(headers);

            // 8. 复制查询参数
            var queryParams = new Dictionary<string, string>();
            foreach (var query in request.Query)
            {
                var value = query.Value.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    queryParams[query.Key] = value;
                }
            }
            gatewayRequest.WithQueryParameters(queryParams);

            // 9. 设置元数据
            gatewayRequest.WithMetadata("Protocol", request.Protocol);
            gatewayRequest.WithMetadata("Scheme", request.Scheme);
            gatewayRequest.WithMetadata("Host", request.Host.ToString());
            gatewayRequest.WithMetadata("ContentType", request.ContentType ?? "");
            gatewayRequest.WithMetadata("ContentLength", request.ContentLength ?? 0);

            return gatewayRequest;
        }

        /// <summary>
        /// 解析 HTTP 方法
        /// </summary>
        private static HttpMethod ParseHttpMethod(string method)
        {
            return method.ToUpperInvariant() switch
            {
                "GET" => HttpMethod.GET,
                "POST" => HttpMethod.POST,
                "PUT" => HttpMethod.PUT,
                "DELETE" => HttpMethod.DELETE,
                "PATCH" => HttpMethod.PATCH,
                "HEAD" => HttpMethod.HEAD,
                "OPTIONS" => HttpMethod.OPTIONS,
                _ => throw new ArgumentException($"不支持的HTTP方法: {method}")
            };
        }

        /// <summary>
        /// 获取客户端真实IP
        /// 优先使用代理头，如果没有则使用 RemoteIpAddress
        /// </summary>
        private static string GetClientIp(HttpContext context)
        {
            // 1. 尝试从 X-Forwarded-For 获取（可能有多个IP，取第一个）
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (ips.Length > 0)
                {
                    return ips[0].Trim();
                }
            }

            // 2. 尝试从 X-Real-IP 获取
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            // 3. 使用 RemoteIpAddress
            var remoteIp = context.Connection.RemoteIpAddress?.ToString();
            return remoteIp ?? "unknown";
        }
    }
}
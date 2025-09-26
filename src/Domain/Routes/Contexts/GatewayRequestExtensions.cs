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
    }
}
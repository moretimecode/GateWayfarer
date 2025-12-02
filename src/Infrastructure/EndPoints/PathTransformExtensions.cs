using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 路径转换扩展方法
    /// 处理通配符 Endpoint 捕获的路径部分
    /// 类似 YARP 的 PathRemovePrefix Transform
    /// </summary>
    public static class PathTransformExtensions
    {
        /// <summary>
        /// 从 HttpContext 提取捕获的路径（{**catch-all}）
        /// </summary>
        /// <param name="context">HttpContext</param>
        /// <param name="catchAllParameterName">通配符参数名，默认为 "catch-all"</param>
        /// <returns>捕获的路径部分，如果未捕获则返回空字符串</returns>
        public static string GetCapturedPath(this HttpContext context, string catchAllParameterName = "catch-all")
        {
            // ASP.NET Core 自动将 {**catch-all} 提取到 RouteValues
            if (context.GetRouteData().Values.TryGetValue(catchAllParameterName, out var value))
            {
                return value?.ToString() ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// 从原始路径中移除前缀，保留通配符捕获部分
        /// 例如：原始路径 /api/users，前缀 /api → 返回 /users
        /// </summary>
        /// <param name="context">HttpContext</param>
        /// <param name="prefix">要移除的前缀</param>
        /// <param name="catchAllParameterName">通配符参数名</param>
        /// <returns>移除前缀后的路径</returns>
        public static string GetTransformedPath(this HttpContext context, string prefix, string catchAllParameterName = "catch-all")
        {
            var capturedPath = context.GetCapturedPath(catchAllParameterName);

            // 重建完整路径
            if (string.IsNullOrEmpty(capturedPath))
            {
                return prefix.TrimEnd('/');
            }

            return $"{prefix.TrimEnd('/')}/{capturedPath}";
        }

        /// <summary>
        /// 获取最终的转发路径
        /// 支持多种转换模式，类似 YARP Transform
        /// </summary>
        /// <param name="context">HttpContext</param>
        /// <param name="originalRoutePath">原始路由配置的路径</param>
        /// <param name="targetPath">目标路径（通常是服务的根路径）</param>
        /// <param name="removeOriginalPrefix">是否移除原始路由前缀</param>
        /// <returns>最终的转发路径</returns>
        public static string GetForwardingPath(
            this HttpContext context,
            string originalRoutePath,
            string targetPath,
            bool removeOriginalPrefix = true)
        {
            var capturedPath = context.GetCapturedPath();

            // 如果没有捕获任何内容（Exact 匹配的情况）
            if (string.IsNullOrEmpty(capturedPath))
            {
                return targetPath.TrimEnd('/');
            }

            // 如果需要移除原始前缀（Prefix 匹配的常见做法）
            if (removeOriginalPrefix)
            {
                return $"{targetPath.TrimEnd('/')}/{capturedPath}";
            }

            // 否则保留完整路径
            return $"{targetPath.TrimEnd('/')}/{originalRoutePath.TrimEnd('/')}/{capturedPath}";
        }

        /// <summary>
        /// 获取 RouteData 中的所有路由参数
        /// 用于调试和日志记录
        /// </summary>
        public static Dictionary<string, object?> GetAllRouteParameters(this HttpContext context)
        {
            return context.GetRouteData().Values
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// 检查是否捕获了通配符参数
        /// </summary>
        public static bool HasCapturedPath(this HttpContext context, string catchAllParameterName = "catch-all")
        {
            return context.GetRouteData().Values.ContainsKey(catchAllParameterName);
        }
    }
}

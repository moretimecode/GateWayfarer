using Domain.Routes;
using Domain.Routes.Configuration;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 动态Endpoint管理器接口
    /// 负责在运行时动态管理ASP.NET Core的Endpoint
    /// </summary>
    public interface IDynamicEndpointManager
    {
        /// <summary>
        /// 添加新的Endpoint
        /// </summary>
        /// <param name="routeConfig">路由配置</param>
        /// <returns>添加结果</returns>
        EndpointOperationResult AddEndpoint(RouteConfigItem routeConfig);

        /// <summary>
        /// 更新现有Endpoint
        /// </summary>
        /// <param name="routeName">路由名称</param>
        /// <param name="newConfig">新的路由配置</param>
        /// <returns>更新结果</returns>
        EndpointOperationResult UpdateEndpoint(string routeName, RouteConfigItem newConfig);

        /// <summary>
        /// 删除Endpoint
        /// </summary>
        /// <param name="routeName">路由名称</param>
        /// <returns>删除结果</returns>
        EndpointOperationResult RemoveEndpoint(string routeName);

        /// <summary>
        /// 清空所有动态Endpoint
        /// </summary>
        /// <returns>清空结果</returns>
        EndpointOperationResult ClearAllEndpoints();

        /// <summary>
        /// 获取当前所有动态Endpoint的信息
        /// </summary>
        /// <returns>Endpoint信息列表</returns>
        List<DynamicEndpointInfo> GetAllEndpoints();

        /// <summary>
        /// 检查指定路由名称的Endpoint是否存在
        /// </summary>
        /// <param name="routeName">路由名称</param>
        /// <returns>是否存在</returns>
        bool Exists(string routeName);
    }

    /// <summary>
    /// Endpoint操作结果
    /// </summary>
    public class EndpointOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = [];
        public string? EndpointId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 动态Endpoint信息
    /// </summary>
    public class DynamicEndpointInfo
    {
        public required string RouteName { get; set; }
        public required string Pattern { get; set; }
        public required string HttpMethods { get; set; }
        public required RouteMatchType MatchType { get; set; }
        public required string ServiceCode { get; set; }
        public int Priority { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = [];
    }
}
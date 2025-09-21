using Domain.Routes.Contexts;

namespace Domain.Routes.Services
{
    /// <summary>
    /// 路由参数提取器接口
    /// </summary>
    public interface IRouteParameterExtractor
    {
        /// <summary>
        /// 支持的匹配类型
        /// </summary>
        RouteMatchType MatchType { get; }
        
        /// <summary>
        /// 提取路由参数
        /// </summary>
        RouteParameters ExtractParameters(string routePath, string requestPath);
    }
}
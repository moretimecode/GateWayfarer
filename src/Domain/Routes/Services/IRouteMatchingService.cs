using Domain.Routes.Contexts;

namespace Domain.Routes.Services
{
    /// <summary>
    /// 路由匹配服务接口
    /// </summary>
    public interface IRouteMatchingService
    {
        /// <summary>
        /// 匹配路由
        /// </summary>
        Task<RouteMatchResult?> MatchAsync(GatewayRequest request);
    }
}
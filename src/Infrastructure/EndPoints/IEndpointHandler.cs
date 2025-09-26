using Domain.Routes.Configuration;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// Endpoint处理器接口
    /// 统一不同类型路由的处理逻辑
    /// </summary>
    public interface IEndpointHandler
    {
        /// <summary>
        /// 处理HTTP请求
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="routeConfig">路由配置</param>
        /// <returns>处理任务</returns>
        Task HandleAsync(HttpContext context, RouteConfigItem routeConfig);

        /// <summary>
        /// 检查当前处理器是否支持指定的路由配置
        /// </summary>
        /// <param name="routeConfig">路由配置</param>
        /// <returns>是否支持</returns>
        bool CanHandle(RouteConfigItem routeConfig);
    }
}
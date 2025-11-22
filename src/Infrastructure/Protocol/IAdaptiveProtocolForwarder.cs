using Microsoft.AspNetCore.Http;
using Domain.Routes.Contexts;

namespace Infrastructure.Protocol;

/// <summary>
/// 自适应协议转发器接口
/// 负责协议检测、协商和转发，使用RouteMatchingService的路由匹配结果
/// </summary>
public interface IAdaptiveProtocolForwarder
{
    /// <summary>
    /// 通过RouteMatchResult转发请求
    /// 使用RouteMatchingService已经完成的路由匹配和负载均衡结果进行协议感知转发
    /// </summary>
    /// <param name="context">HTTP上下文</param>
    /// <param name="routeMatchResult">路由匹配结果，包含选定的目标实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>转发响应</returns>
    Task<ForwardingResponse> ForwardToDestinationAsync(
        HttpContext context, 
        RouteMatchResult routeMatchResult, 
        CancellationToken cancellationToken = default);
}
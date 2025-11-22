using Domain.Routes.Contexts;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Protocol.Streaming;

/// <summary>
/// 流式转发器接口
/// 负责透明地流式转发大数据请求/响应，不处理业务逻辑
/// </summary>
public interface IStreamingForwarder
{
    /// <summary>
    /// 流式转发请求
    /// 自动判断是否使用流式处理，透明转发请求体和响应体
    /// </summary>
    /// <param name="context">HTTP上下文</param>
    /// <param name="routeMatchResult">路由匹配结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>转发响应（包含未读取的响应体流）</returns>
    Task<StreamingForwardingResponse> ForwardAsync(
        HttpContext context,
        RouteMatchResult routeMatchResult,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查请求是否应该使用流式转发
    /// </summary>
    /// <param name="context">HTTP上下文</param>
    /// <returns>是否应该使用流式转发</returns>
    bool ShouldUseStreaming(HttpContext context);
}
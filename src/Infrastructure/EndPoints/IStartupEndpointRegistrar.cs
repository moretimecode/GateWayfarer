using Microsoft.AspNetCore.Routing;

namespace Infrastructure.Endpoints;

/// <summary>
/// 启动时Endpoint注册器接口
/// 负责在应用启动时将所有Route配置转换为Endpoint并注册
/// </summary>
public interface IStartupEndpointRegistrar
{
    /// <summary>
    /// 注册所有启用的Route为Endpoint
    /// </summary>
    /// <param name="endpointRouteBuilder">ASP.NET Core的路由构建器</param>
    void RegisterAllEndpoints(IEndpointRouteBuilder endpointRouteBuilder);

    /// <summary>
    /// 获取已注册的Endpoint数量（用于监控和调试）
    /// </summary>
    int RegisteredEndpointCount { get; }
}

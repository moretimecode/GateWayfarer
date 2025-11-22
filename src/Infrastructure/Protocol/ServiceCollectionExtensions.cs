using Domain.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Infrastructure.Protocol;

/// <summary>
/// 协议相关服务注册扩展
/// </summary>
public static class ProtocolServiceCollectionExtensions
{
    /// <summary>
    /// 注册协议检测服务
    /// </summary>
    public static IServiceCollection AddProtocolDetection(this IServiceCollection services)
    {
        services.TryAddSingleton<IProtocolDetector, HttpContextProtocolDetector>();
        
        return services;
    }
    
    /// <summary>
    /// 注册协议转发服务
    /// </summary>
    public static IServiceCollection AddProtocolSupport(this IServiceCollection services, Action<ProtocolForwardingOptions>? configureOptions = null)
    {
        // 注册协议检测
        services.AddProtocolDetection();
        
        // 注册转发配置选项
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        
        // 注册协议转发器
        services.TryAddSingleton<IAdaptiveProtocolForwarder, AdaptiveProtocolForwarder>();
        
        // 注册HttpClient工厂
        services.TryAddSingleton<IProtocolAwareHttpClientFactory, ProtocolAwareHttpClientFactory>();
        
        // 注册HttpClient
        services.AddHttpClient();
        
        return services;
    }
}

/// <summary>
/// 协议转发配置选项
/// </summary>
public class ProtocolForwardingOptions
{
    /// <summary>
    /// 默认请求超时时间
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 是否启用协议自动协商
    /// </summary>
    public bool EnableProtocolNegotiation { get; set; } = true;

    /// <summary>
    /// 默认的目标协议偏好顺序
    /// </summary>
    public List<ProtocolType> PreferredProtocols { get; set; } = new()
    {
        ProtocolType.Http2,
        ProtocolType.Http11
    };

    /// <summary>
    /// 是否在协议降级时记录警告
    /// </summary>
    public bool LogProtocolDowngrade { get; set; } = true;

    /// <summary>
    /// HttpClient连接池配置
    /// </summary>
    public HttpClientPoolOptions HttpClientPool { get; set; } = new();
}

/// <summary>
/// HttpClient连接池配置
/// </summary>
public class HttpClientPoolOptions
{
    /// <summary>
    /// 连接池中连接的最大空闲时间
    /// </summary>
    public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 最大连接数
    /// </summary>
    public int MaxConnections { get; set; } = 100;

    /// <summary>
    /// 是否启用连接复用
    /// </summary>
    public bool EnableConnectionReuse { get; set; } = true;
}
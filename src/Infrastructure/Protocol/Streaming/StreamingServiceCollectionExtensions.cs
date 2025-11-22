using Infrastructure.Protocol.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Infrastructure.Protocol;

public static class StreamingServiceCollectionExtensions
{
    /// <summary>
    /// 添加流式转发支持
    /// </summary>
    public static IServiceCollection AddStreamingForwarding(
        this IServiceCollection services,
        Action<StreamingForwardingOptions>? configureOptions = null)
    {
        // 1. 注册配置
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            // 默认配置
            services.Configure<StreamingForwardingOptions>(options =>
            {
                options.BufferSize = 81920; // 80KB
                options.StreamTimeout = TimeSpan.FromMinutes(30);
                options.StreamingThreshold = 10 * 1024 * 1024; // 10MB
                options.MaxConcurrentStreams = 100;
                options.EnableStrictHeaderFiltering = true;
                options.AddProxyHeaders = true;
                options.ValidateContentLength = true;
                options.EnableRangeSupport = true;
            });
        }

        // 2. 注册并发限流器（单例）
        services.TryAddSingleton<IStreamingConcurrencyLimiter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StreamingForwardingOptions>>().Value;
            return new StreamingConcurrencyLimiter(options.MaxConcurrentStreams);
        });

        // 3. 注册流式转发器（单例，因为是无状态的）
        services.TryAddSingleton<IStreamingForwarder, StreamingForwarder>();

        return services;
    }
}
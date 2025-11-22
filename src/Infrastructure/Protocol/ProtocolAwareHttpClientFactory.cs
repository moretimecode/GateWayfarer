using Domain.Services.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;

namespace Infrastructure.Protocol;

/// <summary>
/// 协议感知HttpClient工厂实现
/// 为不同协议和服务维护优化的HttpClient实例池
/// </summary>
public class ProtocolAwareHttpClientFactory : IProtocolAwareHttpClientFactory, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<ServiceOptions> _serviceOptions;
    private readonly ILogger<ProtocolAwareHttpClientFactory> _logger;
    private readonly ProtocolForwardingOptions _options;

    private readonly ConcurrentDictionary<string, HttpClient> _clientCache = new();
    private readonly IDisposable? _optionsChangeSubscription;
    private bool _disposed = false;

    public ProtocolAwareHttpClientFactory(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ServiceOptions> serviceOptions,
        ILogger<ProtocolAwareHttpClientFactory> logger,
        IOptions<ProtocolForwardingOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _serviceOptions = serviceOptions;
        _logger = logger;
        _options = options.Value;
        _optionsChangeSubscription = _serviceOptions.OnChange((_, _) => ClearAllCaches());
    }


    public void ClearConnections(string serviceName)
    {
        var keysToRemove = _clientCache.Keys
            .Where(key => key.StartsWith($"{serviceName}:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            if (_clientCache.TryRemove(key, out var client))
            {
                client.Dispose();
                _logger.LogDebug("已释放服务 {ServiceName} 的HttpClient连接", serviceName);
            }
        }
    }


    /// <summary>
    /// 确定目标协议
    /// </summary>
    private ProtocolType DetermineTargetProtocol(ProtocolType? preferredProtocol, ServiceConfigItem serviceConfig)
    {
        // 1. 如果服务有强制协议配置，使用强制协议
        if (serviceConfig.Protocol.ForceProtocolVersion.HasValue)
        {
            return serviceConfig.Protocol.ForceProtocolVersion.Value;
        }

        // 2. 如果有首选协议且服务支持，使用首选协议
        if (preferredProtocol.HasValue && serviceConfig.Protocol.SupportedProtocols.Contains(preferredProtocol.Value))
        {
            return preferredProtocol.Value;
        }

        if (serviceConfig.Protocol.PreferredProtocols.Count > 0)
        {
            return serviceConfig.Protocol.PreferredProtocols[0];
        }

        if (_options.PreferredProtocols.Count > 0)
        {
            return _options.PreferredProtocols[0];
        }

        return ProtocolType.Http2;
    }


    /// <summary>
    /// 获取HTTP版本设置
    /// </summary>
    private static (Version version, HttpVersionPolicy policy) GetHttpVersionSettings(ProtocolType protocol)
    {
        return protocol switch
        {
            ProtocolType.Http10 => (HttpVersion.Version10, HttpVersionPolicy.RequestVersionExact),
            ProtocolType.Http11 => (HttpVersion.Version11, HttpVersionPolicy.RequestVersionExact),
            ProtocolType.Http2 => (HttpVersion.Version20, HttpVersionPolicy.RequestVersionOrLower), // YARP风格自适应
            ProtocolType.Http3 => (HttpVersion.Version30, HttpVersionPolicy.RequestVersionOrLower),
            ProtocolType.Grpc => (HttpVersion.Version20, HttpVersionPolicy.RequestVersionExact), // gRPC需要HTTP/2
            _ => (HttpVersion.Version20, HttpVersionPolicy.RequestVersionOrLower) // 默认尝试HTTP/2
        };
    }

    private static string BuildCacheKey(string serviceName, ProtocolType? preferredProtocol)
    {
        return preferredProtocol.HasValue
            ? $"{serviceName}:{preferredProtocol.Value}"
            : serviceName;
    }

    public HttpClient CreateClient(string serviceName, ProtocolType? preferredProtocol = null)
    {
        var cacheKey = BuildCacheKey(serviceName, preferredProtocol);
        return _clientCache.GetOrAdd(cacheKey, _ => CreateHttpClientInternal(serviceName, preferredProtocol));
    }

    private HttpClient CreateHttpClientInternal(string serviceName, ProtocolType? preferredProtocol)
    {
        var serviceConfig = GetServiceConfig(serviceName);

        if (serviceConfig == null)
        {
            _logger.LogWarning("服务 {ServiceName} 的配置不存在，使用默认HttpClient", serviceName);
            return _httpClientFactory.CreateClient();
        }

        var targetProtocol = DetermineTargetProtocol(preferredProtocol, serviceConfig.Protocol);
        var clientName = $"{serviceName}_{targetProtocol}";
        var httpClient = _httpClientFactory.CreateClient(clientName);

        ConfigureHttpClientForProtocol(httpClient, targetProtocol, serviceConfig.Protocol);

        _logger.LogDebug("创建HttpClient: 服务={ServiceName}, 协议={Protocol}", serviceName, targetProtocol);
        return httpClient;
    }

    /// <summary>
    /// 从ServiceOptions获取服务配置
    /// </summary>
    private ServiceConfigItem? GetServiceConfig(string serviceName)
    {
        var services = _serviceOptions.CurrentValue.Services;
        return services.FirstOrDefault(s => s.Code.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }

    private ProtocolType DetermineTargetProtocol(ProtocolType? preferredProtocol, ProtocolConfigItem protocolConfig)
    {
        if (protocolConfig.ForceProtocolVersion.HasValue)
            return protocolConfig.ForceProtocolVersion.Value;

        if (preferredProtocol.HasValue && protocolConfig.SupportedProtocols.Contains(preferredProtocol.Value))
            return preferredProtocol.Value;

        return protocolConfig.PreferredProtocols.FirstOrDefault();
    }

    private void ConfigureHttpClientForProtocol(HttpClient httpClient, ProtocolType protocol, ProtocolConfigItem protocolConfig)
    {
        var (version, versionPolicy) = GetHttpVersionSettings(protocol);
        httpClient.DefaultRequestVersion = version;
        httpClient.DefaultVersionPolicy = versionPolicy;


        if (protocolConfig.TimeoutSeconds.HasValue)
        {
            httpClient.Timeout = TimeSpan.FromSeconds(protocolConfig.TimeoutSeconds.Value);
        }

        // 默认头部
        foreach (var header in protocolConfig.DefaultHeaders)
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        _logger.LogDebug("HttpClient协议配置完成: 协议={Protocol}, 版本={Version}, 超时={Timeout}s",
            protocol, version, protocolConfig.TimeoutSeconds);
    }

    private void ClearAllCaches()
    {
        _logger.LogInformation("检测到ServiceOptions配置变化，清除HttpClient缓存");

        foreach (var client in _clientCache.Values)
        {
            try
            {
                client?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放HttpClient时发生异常");
            }
        }

        _clientCache.Clear();
    }
    public void Dispose()
    {
        if (_disposed) return;

        foreach (var client in _clientCache.Values)
        {
            try
            {
                client?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放HttpClient时发生异常");
            }
        }

        _clientCache.Clear();
        _disposed = true;

        _logger.LogDebug("ProtocolAwareHttpClientFactory已释放");
    }
}
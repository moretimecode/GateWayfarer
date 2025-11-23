using System.Text.RegularExpressions;
using Domain.Routes.Configuration;
using Droplet.Data.Entities;

namespace Domain.Routes;

public class Route : AggregateRoot
{
    /// <summary>
    /// 路由名称
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// 匹配路径
    /// </summary>
    public string Path { get; private set; }

    /// <summary>
    /// 支持的HTTP方法
    /// </summary>
    public HttpMethod HttpMethods { get; private set; }

    /// <summary>
    /// 路由优先级（数值越大优先级越高）
    /// </summary>
    public int Priority { get; private set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// 路由匹配类型
    /// </summary>
    public RouteMatchType MatchType { get; private set; }

    /// <summary>
    /// 负载均衡类型
    /// </summary>
    public LoadBalanceType LoadBalanceType { get; private set; }

    /// <summary>
    /// 引用的服务代码
    /// </summary>
    public string ServiceCode { get; private set; }

    public RequestBatchingConfig BatchingConfig { get; private set; }
    public RequestForwardingConfig ForwardingConfig { get; private set; }


    private Route()
    {
        Name = string.Empty;
        Path = string.Empty;
        ServiceCode = string.Empty;
    }

    public Route(
        string name,
        string path,
        string serviceCode,
        HttpMethod httpMethods = HttpMethod.GET,
        int priority = 0,
        bool isEnabled = true,
        RouteMatchType matchType = RouteMatchType.Prefix,
        LoadBalanceType loadBalanceType = LoadBalanceType.RoundRobin)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Route name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Route path cannot be empty", nameof(path));

        if (string.IsNullOrWhiteSpace(serviceCode))
            throw new ArgumentException("Service code cannot be empty", nameof(serviceCode));

        Name = name;
        Path = path;
        ServiceCode = serviceCode;
        HttpMethods = httpMethods;
        Priority = priority;
        IsEnabled = isEnabled;
        MatchType = matchType;
        LoadBalanceType = loadBalanceType;
    }

    /// <summary>
    /// 检查请求路径是否匹配此路由
    /// </summary>
    public bool IsMatch(string requestPath, HttpMethod httpMethod)
    {
        if (!IsEnabled)
            return false;

        if (!HttpMethods.HasFlag(httpMethod))
            return false;

        return MatchType switch
        {
            RouteMatchType.Exact => string.Equals(Path, requestPath, StringComparison.OrdinalIgnoreCase),
            RouteMatchType.Prefix => requestPath.StartsWith(Path, StringComparison.OrdinalIgnoreCase),
            RouteMatchType.Regex => Regex.IsMatch(requestPath, Path),
            RouteMatchType.Template => MatchTemplate(Path, requestPath),
            _ => false
        };
    }

    private static bool MatchTemplate(string template, string requestPath)
    {
        var templateSegments = template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = requestPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (templateSegments.Length != pathSegments.Length)
            return false;

        for (int i = 0; i < templateSegments.Length; i++)
        {
            var templateSegment = templateSegments[i];
            var pathSegment = pathSegments[i];

            if (templateSegment.StartsWith("{") && templateSegment.EndsWith("}"))
                continue;

            if (!string.Equals(templateSegment, pathSegment, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 更新路由配置
    /// </summary>
    public void Update(
        string? name = null,
        string? path = null,
        string? serviceCode = null,
        HttpMethod? httpMethods = null,
        int? priority = null,
        bool? isEnabled = null,
        RouteMatchType? matchType = null,
        LoadBalanceType? loadBalanceType = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name;

        if (!string.IsNullOrWhiteSpace(path))
            Path = path;

        if (!string.IsNullOrWhiteSpace(serviceCode))
            ServiceCode = serviceCode;

        if (httpMethods.HasValue)
            HttpMethods = httpMethods.Value;

        if (priority.HasValue)
            Priority = priority.Value;

        if (isEnabled.HasValue)
            IsEnabled = isEnabled.Value;

        if (matchType.HasValue)
            MatchType = matchType.Value;

        if (loadBalanceType.HasValue)
            LoadBalanceType = loadBalanceType.Value;
    }

    /// <summary>
    /// 启用路由
    /// </summary>
    public void Enable()
    {
        IsEnabled = true;
    }

    /// <summary>
    /// 禁用路由
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
    }

    public Route WithBatchingConfig(RequestBatchingConfig batchingConfig)
    {
        BatchingConfig = batchingConfig ?? throw new ArgumentNullException(nameof(batchingConfig));
        return this;
    }

    public Route WithForwardingConfig(RequestForwardingConfig forwardingConfig)
    {
        ForwardingConfig = forwardingConfig ?? throw new ArgumentNullException(nameof(forwardingConfig));
        return this;
    }

    // 添加验证方法
    public void ValidateConfiguration()
    {

        if (BatchingConfig is null)
            BatchingConfig = RequestBatchingConfig.Disabled();

        if (ForwardingConfig is null)
            ForwardingConfig = RequestForwardingConfig.Default();
    }
}
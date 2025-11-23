namespace Domain.Services.Configuration;

public class ServiceOptions
{
    public const string SectionName = "ApiGateway:Services";
    public List<ServiceConfigItem> Services { get; set; } = [];
}

public class ServiceConfigItem
{
    /// <summary>
    /// 服务业务代码
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 服务名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 健康检查配置
    /// </summary>
    public HealthCheckConfigItem HealthCheck { get; set; } = new();

    /// <summary>
    /// 目标实例列表
    /// </summary>
    public List<DestinationConfigItem> Destinations { get; set; } = new();

    /// <summary>
    /// 协议配置
    /// </summary>
    public ProtocolConfigItem Protocol { get; set; } = new();
}

/// <summary>
/// 协议配置项
/// </summary>
public class ProtocolConfigItem
{
    /// <summary>
    /// 支持的协议列表
    /// </summary>
    public List<ProtocolType> SupportedProtocols { get; set; } = new() { ProtocolType.Http2, ProtocolType.Http11 };

    /// <summary>
    /// 首选协议列表（按优先级排序）
    /// </summary>
    public List<ProtocolType> PreferredProtocols { get; set; } = new() { ProtocolType.Http2, ProtocolType.Http11 };

    /// <summary>
    /// 强制使用的协议版本
    /// </summary>
    public ProtocolType? ForceProtocolVersion { get; set; }

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// 默认请求头
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();

    /// <summary>
    /// 是否启用连接复用
    /// </summary>
    public bool EnableConnectionReuse { get; set; } = true;

    /// <summary>
    /// 最大并发请求数（HTTP/2）
    /// </summary>
    public int? MaxConcurrentStreams { get; set; }

    /// <summary>
    /// 协议元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

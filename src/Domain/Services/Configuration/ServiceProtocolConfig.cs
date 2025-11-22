namespace Domain.Services.Configuration
{
    public class ServiceProtocolConfig:ValueObject
    {
        /// <summary>
    /// 服务支持的协议列表
    /// </summary>
    public List<ProtocolType> SupportedProtocols { get; set; } = new()
    {
        ProtocolType.Http2,
        ProtocolType.Http11
    };

    /// <summary>
    /// 首选协议列表（按优先级排序）
    /// </summary>
    public List<ProtocolType> PreferredProtocols { get; set; } = new()
    {
        ProtocolType.Http2,
        ProtocolType.Http11
    };

    /// <summary>
    /// 强制使用的协议版本（覆盖自动协商）
    /// </summary>
    public ProtocolType? ForceProtocolVersion { get; set; }

    /// <summary>
    /// 请求超时时间
    /// </summary>
    public int? Timeout { get; set; }

    /// <summary>
    /// 默认请求头
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = [];

    /// <summary>
    /// 是否启用连接复用
    /// </summary>
    public bool EnableConnectionReuse { get; set; } = true;

    /// <summary>
    /// 最大并发请求数（HTTP/2）
    /// </summary>
    public int? MaxConcurrentStreams { get; set; }

    /// <summary>
    /// 额外配置元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];

    /// <summary>
    /// 创建默认配置
    /// </summary>
    public static ServiceProtocolConfig CreateDefault()
    {
        return new ServiceProtocolConfig();
    }

    /// <summary>
    /// 创建仅HTTP/1.1的配置
    /// </summary>
    public static ServiceProtocolConfig CreateHttp11Only()
    {
        return new ServiceProtocolConfig
        {
            SupportedProtocols = [ProtocolType.Http11],
            PreferredProtocols = [ProtocolType.Http11],
            ForceProtocolVersion = ProtocolType.Http11
        };
    }

    /// <summary>
    /// 创建仅HTTP/2的配置
    /// </summary>
    public static ServiceProtocolConfig CreateHttp2Only()
    {
        return new ServiceProtocolConfig
        {
            SupportedProtocols = [ProtocolType.Http2],
            PreferredProtocols = [ProtocolType.Http2],
            ForceProtocolVersion = ProtocolType.Http2
        };
    }

    /// <summary>
    /// 创建gRPC服务配置
    /// </summary>
    public static ServiceProtocolConfig CreateGrpc()
    {
        return new ServiceProtocolConfig
        {
            SupportedProtocols = [ProtocolType.Grpc],
            PreferredProtocols = [ProtocolType.Grpc],
            ForceProtocolVersion = ProtocolType.Grpc,
            DefaultHeaders = new Dictionary<string, string>
            {
                ["TE"] = "trailers"
            }
        };
    }
    }
}
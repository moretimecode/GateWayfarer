namespace Infrastructure.Protocol.Streaming;

/// <summary>
/// 流式转发配置选项
/// </summary>
public class StreamingForwardingOptions
{
    /// <summary>
    /// 缓冲区大小（字节）
    /// 默认 80KB，平衡内存占用和性能
    /// </summary>
    public int BufferSize { get; set; } = 81920;

    /// <summary>
    /// 流式传输超时时间
    /// 用于大文件传输，默认30分钟
    /// </summary>
    public TimeSpan StreamTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 触发流式转发的Content-Length阈值（字节）
    /// 超过此大小的请求体将使用流式转发
    /// 默认 10MB
    /// </summary>
    public long StreamingThreshold { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// 最大并发流式转发数量
    /// 用于限流，防止资源耗尽
    /// 默认 100
    /// </summary>
    public int MaxConcurrentStreams { get; set; } = 100;

    /// <summary>
    /// 触发流式转发的 Content-Type 列表
    /// </summary>
    public List<string> StreamingContentTypes { get; set; } = new()
    {
        "multipart/form-data",
        "application/octet-stream",
        "video/",
        "audio/",
        "application/pdf",
        "application/zip",
        "image/"
    };

    /// <summary>
    /// 是否启用严格的请求头过滤
    /// 启用后会过滤危险的请求头
    /// </summary>
    public bool EnableStrictHeaderFiltering { get; set; } = true;

    /// <summary>
    /// 请求头黑名单
    /// 这些头不会被转发到后端服务
    /// </summary>
    public HashSet<string> HeaderBlacklist { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host",
        "Connection",
        "Transfer-Encoding",
        "Upgrade",
        "Proxy-Connection",
        "Keep-Alive",
        "TE",
        "Trailer"
    };

    /// <summary>
    /// 是否自动添加代理标识头
    /// 例如 X-Forwarded-For, X-Forwarded-Proto, X-Forwarded-Host
    /// </summary>
    public bool AddProxyHeaders { get; set; } = true;

    /// <summary>
    /// 是否验证 Content-Length
    /// 启用后会检查实际传输字节数是否匹配声明的 Content-Length
    /// </summary>
    public bool ValidateContentLength { get; set; } = true;

    /// <summary>
    /// 是否支持 Range 请求（断点续传）
    /// </summary>
    public bool EnableRangeSupport { get; set; } = true;
}
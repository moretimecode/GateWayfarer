namespace Domain.Routes.Contexts;

/// <summary>
/// 网关请求上下文
/// </summary>
public class GatewayRequest(string path, HttpMethod httpMethod)
{
    /// <summary>
    /// 请求路径
    /// </summary>
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    /// <summary>
    /// HTTP方法
    /// </summary>
    public HttpMethod HttpMethod { get; } = httpMethod;

    /// <summary>
    /// 客户端IP地址
    /// </summary>
    public string? ClientIp { get; set; }

    /// <summary>
    /// 用户代理
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>
    /// 查询参数
    /// </summary>
    public Dictionary<string, string> QueryParameters { get; set; } = [];

    /// <summary>
    /// 请求ID（用于链路追踪）
    /// </summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 请求时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 请求来源
    /// </summary>
    public string Source { get; set; } = "HTTP";

    /// <summary>
    /// 额外的元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 创建HTTP请求上下文的便捷方法
    /// </summary>
    public static GatewayRequest FromHttp(string path, HttpMethod httpMethod, string? clientIp = null, string? userAgent = null)
    {
        return new GatewayRequest(path, httpMethod)
        {
            ClientIp = clientIp,
            UserAgent = userAgent,
            Source = "HTTP"
        };
    }

    /// <summary>
    /// 添加请求头
    /// </summary>
    public GatewayRequest WithHeader(string name, string value)
    {
        Headers[name] = value;
        return this;
    }

    /// <summary>
    /// 批量添加请求头
    /// </summary>
    public GatewayRequest WithHeaders(Dictionary<string, string> headers)
    {
        foreach (var header in headers)
        {
            Headers[header.Key] = header.Value;
        }
        return this;
    }

    /// <summary>
    /// 添加查询参数
    /// </summary>
    public GatewayRequest WithQueryParameter(string name, string value)
    {
        QueryParameters[name] = value;
        return this;
    }

    /// <summary>
    /// 批量添加查询参数
    /// </summary>
    public GatewayRequest WithQueryParameters(Dictionary<string, string> queryParameters)
    {
        foreach (var param in queryParameters)
        {
            QueryParameters[param.Key] = param.Value;
        }
        return this;
    }

    /// <summary>
    /// 添加元数据
    /// </summary>
    public GatewayRequest WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// 设置请求ID
    /// </summary>
    public GatewayRequest WithRequestId(string requestId)
    {
        RequestId = requestId;
        return this;
    }

    /// <summary>
    /// 设置请求来源
    /// </summary>
    public GatewayRequest WithSource(string source)
    {
        Source = source;
        return this;
    }

    /// <summary>
    /// 设置客户端IP
    /// </summary>
    public GatewayRequest WithClientIp(string clientIp)
    {
        ClientIp = clientIp;
        return this;
    }

    /// <summary>
    /// 设置用户代理
    /// </summary>
    public GatewayRequest WithUserAgent(string userAgent)
    {
        UserAgent = userAgent;
        return this;
    }

    public override string ToString()
    {
        return $"[{RequestId}] {HttpMethod} {Path} from {ClientIp} ({Source})";
    }
}

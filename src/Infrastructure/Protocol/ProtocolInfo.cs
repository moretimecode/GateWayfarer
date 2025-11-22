using Domain.Services.Configuration;

namespace Infrastructure.Protocol;

/// <summary>
/// 协议信息值对象
/// 包含检测到的协议详细信息
/// </summary>
public record ProtocolInfo
{
    /// <summary>
    /// 协议类型
    /// </summary>
    public required ProtocolType Type { get; init; }

    /// <summary>
    /// 协议版本字符串（如：HTTP/1.1, HTTP/2）
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// 是否为安全连接（HTTPS、WSS等）
    /// </summary>
    public bool IsSecure { get; init; }

    /// <summary>
    /// 用户代理字符串
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// 协议特有的元数据
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// 检测时间戳
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

    // 便利属性
    /// <summary>
    /// 是否为HTTP协议族
    /// </summary>
    public bool IsHttp => Type.IsHttp();

    /// <summary>
    /// 是否为WebSocket协议
    /// </summary>
    public bool IsWebSocket => Type == ProtocolType.WebSocket;

    /// <summary>
    /// 是否为gRPC协议
    /// </summary>
    public bool IsGrpc => Type == ProtocolType.Grpc;

    /// <summary>
    /// 是否支持多路复用
    /// </summary>
    public bool SupportsMultiplexing => Type.SupportsMultiplexing();

    /// <summary>
    /// 创建HTTP协议信息
    /// </summary>
    public static ProtocolInfo CreateHttp(ProtocolType type, string version, bool isSecure, string? userAgent = null)
    {
        if (!type.IsHttp())
            throw new ArgumentException($"协议类型 {type} 不是HTTP协议", nameof(type));

        return new ProtocolInfo
        {
            Type = type,
            Version = version,
            IsSecure = isSecure,
            UserAgent = userAgent
        };
    }

    /// <summary>
    /// 创建WebSocket协议信息
    /// </summary>
    public static ProtocolInfo CreateWebSocket(bool isSecure, string? userAgent = null, IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new ProtocolInfo
        {
            Type = ProtocolType.WebSocket,
            Version = "WebSocket",
            IsSecure = isSecure,
            UserAgent = userAgent,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// 创建gRPC协议信息
    /// </summary>
    public static ProtocolInfo CreateGrpc(bool isSecure, string? userAgent = null, IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new ProtocolInfo
        {
            Type = ProtocolType.Grpc,
            Version = "gRPC",
            IsSecure = isSecure,
            UserAgent = userAgent,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// 创建未知协议信息（用于错误处理）
    /// </summary>
    public static ProtocolInfo CreateUnknown(string? version = null)
    {
        return new ProtocolInfo
        {
            Type = ProtocolType.Unknown,
            Version = version ?? "Unknown"
        };
    }

    public override string ToString()
    {
        return $"{Type.GetDisplayName()}{(IsSecure ? " (Secure)" : "")}";
    }
}
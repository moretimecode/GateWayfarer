namespace Domain.Services.Configuration;
public enum ProtocolType
{
    /// <summary>
    /// 未知协议 - 默认值，用于错误处理
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// HTTP/1.0 - 历史协议，基本已废弃
    /// </summary>
    Http10 = 1,

    /// <summary>
    /// HTTP/1.1 - 最常见的协议，默认选择
    /// </summary>
    Http11 = 2,

    /// <summary>
    /// HTTP/2 - 现代协议，支持多路复用
    /// </summary>
    Http2 = 3,

    /// <summary>
    /// HTTP/3 - 基于QUIC的最新协议
    /// </summary>
    Http3 = 4,

    /// <summary>
    /// WebSocket - 全双工通信协议
    /// </summary>
    WebSocket = 5,

    /// <summary>
    /// gRPC - 基于HTTP/2的RPC协议
    /// </summary>
    Grpc = 6
}

public static class ProtocolTypeExtensions
{
    /// <summary>
    /// 判断是否为HTTP协议族
    /// </summary>
    public static bool IsHttp(this ProtocolType protocol)
    {
        return protocol is ProtocolType.Http10 or ProtocolType.Http11 or ProtocolType.Http2 or ProtocolType.Http3;
    }

    /// <summary>
    /// 判断是否支持多路复用
    /// </summary>
    public static bool SupportsMultiplexing(this ProtocolType protocol)
    {
        return protocol is ProtocolType.Http2 or ProtocolType.Http3 or ProtocolType.Grpc;
    }

    /// <summary>
    /// 判断是否为安全协议（通常基于TLS）
    /// </summary>
    public static bool IsSecureByDefault(this ProtocolType protocol)
    {
        return protocol is ProtocolType.Http2 or ProtocolType.Http3 or ProtocolType.Grpc;
    }

    /// <summary>
    /// 获取协议的显示名称
    /// </summary>
    public static string GetDisplayName(this ProtocolType protocol)
    {
        return protocol switch
        {
            ProtocolType.Http10 => "HTTP/1.0",
            ProtocolType.Http11 => "HTTP/1.1",
            ProtocolType.Http2 => "HTTP/2",
            ProtocolType.Http3 => "HTTP/3",
            ProtocolType.WebSocket => "WebSocket",
            ProtocolType.Grpc => "gRPC",
            ProtocolType.Unknown => "Unknown",
            _ => protocol.ToString()
        };
    }

    /// <summary>
    /// 获取协议优先级（数值越高优先级越高）
    /// </summary>
    public static int GetPriority(this ProtocolType protocol)
    {
        return (int)protocol;
    }
}
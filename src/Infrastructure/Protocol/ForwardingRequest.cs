using Domain.Services.Configuration;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text;

namespace Infrastructure.Protocol;

/// <summary>
/// 转发请求模型
/// 封装了要转发到下游服务的请求信息
/// </summary>
public class ForwardingRequest
{
    /// <summary>
    /// HTTP方法
    /// </summary>
    public required HttpMethod Method { get; set; }

    /// <summary>
    /// 目标URI
    /// </summary>
    public required Uri TargetUri { get; set; }

    /// <summary>
    /// 客户端使用的协议
    /// </summary>
    public required ProtocolType ClientProtocol { get; set; }

    /// <summary>
    /// 首选的服务端协议（null表示自动协商）
    /// </summary>
    public ProtocolType? PreferredServerProtocol { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>
    /// 请求体
    /// </summary>
    public Stream? Body { get; set; }

    /// <summary>
    /// 请求超时时间
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// 额外的元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];

    private static readonly string[] _skipHeaders =
        [
            // 连接相关头部
            "host", "connection", "keep-alive", "transfer-encoding",
            "upgrade", "proxy-connection", "proxy-authorization",
            
            // 安全相关头部（需要网关重新生成）
            "authorization", "cookie", "set-cookie",
            
            // 网关特有头部
            "x-forwarded-for", "x-forwarded-proto", "x-forwarded-host",
            "x-real-ip", "x-original-uri"
        ];

    /// <summary>
    /// 从HttpContext创建转发请求
    /// </summary>
    public static async Task<ForwardingRequest> FromHttpContextAsync(
        HttpContext context,
        Uri targetUri,
        ProtocolType clientProtocol)
    {
        var request = new ForwardingRequest
        {
            Method = new HttpMethod(context.Request.Method),
            TargetUri = targetUri,
            ClientProtocol = clientProtocol
        };

        // 复制请求头
        foreach (var header in context.Request.Headers)
        {
            if (ShouldForwardHeader(header.Key))
            {
                request.Headers[header.Key] = header.Value.ToString();
            }
        }

        // 处理特殊头部
        ProcessSpecialHeaders(context.Request, request);

        // 复制请求体
        if (context.Request.Body != null &&
            context.Request.ContentLength.HasValue &&
            context.Request.ContentLength > 0)
        {
            var bodyContent = new MemoryStream();
            await context.Request.Body.CopyToAsync(bodyContent);
            bodyContent.Position = 0;
            request.Body = bodyContent;
        }

        return request;
    }

    /// <summary>
    /// 创建简单的GET请求
    /// </summary>
    public static ForwardingRequest CreateGet(Uri targetUri, ProtocolType clientProtocol = ProtocolType.Http11)
    {
        return new ForwardingRequest
        {
            Method = HttpMethod.Get,
            TargetUri = targetUri,
            ClientProtocol = clientProtocol

        };
    }

    /// <summary>
    /// 创建POST请求
    /// </summary>
    public static ForwardingRequest CreatePost(Uri targetUri, Stream? body = null, ProtocolType clientProtocol = ProtocolType.Http11)
    {
        return new ForwardingRequest
        {
            Method = HttpMethod.Post,
            TargetUri = targetUri,
            ClientProtocol = clientProtocol,
            Body = body
        };
    }

    /// <summary>
    /// 判断是否应该转发指定的头部
    /// </summary>
    private static bool ShouldForwardHeader(string headerName)
    {
        // 不应该转发的头部列表（按照反向代理最佳实践）
        return !_skipHeaders.Contains(headerName.ToLowerInvariant());
    }

    /// <summary>
    /// 处理特殊头部（如X-Forwarded-*）
    /// </summary>
    private static void ProcessSpecialHeaders(HttpRequest originalRequest, ForwardingRequest forwardingRequest)
    {
        // 添加X-Forwarded-For头部
        var clientIp = originalRequest.HttpContext.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(clientIp))
        {
            var existingXFF = originalRequest.Headers["X-Forwarded-For"].FirstOrDefault();
            forwardingRequest.Headers["X-Forwarded-For"] = string.IsNullOrEmpty(existingXFF)
                ? clientIp
                : $"{existingXFF}, {clientIp}";
        }

        // 添加X-Forwarded-Proto头部
        forwardingRequest.Headers["X-Forwarded-Proto"] = originalRequest.Scheme;

        // 添加X-Forwarded-Host头部
        forwardingRequest.Headers["X-Forwarded-Host"] = originalRequest.Host.ToString();

        // 设置正确的Host头部
        forwardingRequest.Headers["Host"] = forwardingRequest.TargetUri.Authority;
    }

    /// <summary>
    /// 转换为HttpRequestMessage
    /// </summary>
    public HttpRequestMessage ToHttpRequestMessage()
    {
        var request = new HttpRequestMessage(Method, TargetUri);

        // 设置请求头
        foreach (var header in Headers)
        {
            // 区分内容头部和请求头部
            if (IsContentHeader(header.Key))
            {
                // 内容头部需要在设置Content之后添加
                continue;
            }
            else
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // 设置请求体
        if (Body != null)
        {
            request.Content = new StreamContent(Body);

            // 添加内容头部
            foreach (var header in Headers.Where(h => IsContentHeader(h.Key)))
            {
                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // 设置协议版本
        request.Version = GetHttpVersion(PreferredServerProtocol ?? ClientProtocol);
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower; // YARP风格的自适应

        return request;
    }

    /// <summary>
    /// 判断是否为内容头部
    /// </summary>
    private static bool IsContentHeader(string headerName)
    {
        var contentHeaders = new[]
        {
            "content-type", "content-length", "content-encoding",
            "content-disposition", "content-range", "content-location"
        };

        return contentHeaders.Contains(headerName.ToLowerInvariant());
    }

    /// <summary>
    /// 获取对应的HttpVersion
    /// </summary>
    private static Version GetHttpVersion(ProtocolType protocolType)
    {
        return protocolType switch
        {
            ProtocolType.Http10 => HttpVersion.Version10,
            ProtocolType.Http11 => HttpVersion.Version11,
            ProtocolType.Http2 => HttpVersion.Version20,
            ProtocolType.Http3 => HttpVersion.Version30,
            _ => HttpVersion.Version11 // 默认使用HTTP/1.1
        };
    }

    public override string ToString()
    {
        return $"{Method} {TargetUri} ({ClientProtocol})";
    }
}
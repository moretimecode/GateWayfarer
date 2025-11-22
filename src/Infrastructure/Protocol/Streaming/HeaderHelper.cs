using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;

namespace Infrastructure.Protocol.Streaming;

/// <summary>
/// 请求头处理静态辅助类
/// 提供请求头过滤、转换、添加代理头等功能
/// </summary>
public static class HeaderHelper
{
    /// <summary>
    /// 过滤请求头，移除危险或不必要的头
    /// </summary>
    public static Dictionary<string, string[]> FilterRequestHeaders(
        IHeaderDictionary sourceHeaders,
        HashSet<string> blacklist)
    {
        var filtered = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in sourceHeaders)
        {
            // 跳过黑名单中的头
            if (blacklist.Contains(header.Key))
                continue;

            // 跳过Content-*头（由HttpContent单独处理）
            if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                continue;

            filtered[header.Key] = header.Value.ToArray();
        }

        return filtered;
    }

    /// <summary>
    /// 添加代理标识头
    /// </summary>
    public static void AddProxyHeaders(
        HttpRequestMessage httpRequest,
        HttpContext context)
    {
        var request = context.Request;
        var connection = context.Connection;

        // X-Forwarded-For
        var forwardedFor = request.Headers["X-Forwarded-For"].FirstOrDefault();
        var clientIp = connection.RemoteIpAddress?.ToString();
        
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            httpRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", $"{forwardedFor}, {clientIp}");
        }
        else if (!string.IsNullOrEmpty(clientIp))
        {
            httpRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", clientIp);
        }

        // X-Forwarded-Proto
        var scheme = request.Scheme;
        httpRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", scheme);

        // X-Forwarded-Host
        var host = request.Host.ToString();
        httpRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", host);

        // X-Real-IP
        if (!string.IsNullOrEmpty(clientIp))
        {
            httpRequest.Headers.TryAddWithoutValidation("X-Real-IP", clientIp);
        }
    }

    /// <summary>
    /// 复制Content-*头到HttpContent
    /// </summary>
    public static void CopyContentHeaders(
        IHeaderDictionary sourceHeaders,
        HttpContent httpContent)
    {
        foreach (var header in sourceHeaders)
        {
            if (!header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                continue;

            // Content-Type 和 Content-Length 特殊处理
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                if (MediaTypeHeaderValue.TryParse(header.Value.ToString(), out var mediaType))
                {
                    httpContent.Headers.ContentType = mediaType;
                }
            }
            else if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(header.Value.ToString(), out var length))
                {
                    httpContent.Headers.ContentLength = length;
                }
            }
            else
            {
                // 其他Content-*头
                httpContent.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }
    }

    /// <summary>
    /// 判断是否是有效的响应头（可以写入到客户端响应）
    /// </summary>
    public static bool IsValidResponseHeader(string headerName)
    {
        // Transfer-Encoding 由ASP.NET Core自动处理
        return !headerName.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase);
    }
}
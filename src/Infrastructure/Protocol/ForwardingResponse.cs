using Domain.Services.Configuration;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace Infrastructure.Protocol;

/// <summary>
/// 转发响应模型
/// 封装了从下游服务返回的响应信息
/// </summary>
public class ForwardingResponse : IAsyncDisposable
{
    private HttpResponseMessage? _httpResponse;
    private bool _disposed;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// HTTP状态码
    /// </summary>
    public int StatusCode { get; private set; }

    /// <summary>
    /// 错误消息（失败时）
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// 响应头
    /// </summary>
    public Dictionary<string, string[]> Headers { get; private set; }

    /// <summary>
    /// 实际使用的协议
    /// </summary>
    public ProtocolType UsedProtocol { get; private set; }

    /// <summary>
    /// 转发耗时
    /// </summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>
    /// 响应体内容（已读取）
    /// </summary>
    public byte[]? Content { get; private set; }

    private ForwardingResponse()
    {
        Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static async Task<ForwardingResponse> CreateSuccessAsync(
        HttpResponseMessage httpResponse,
        ProtocolType usedProtocol,
        TimeSpan duration)
    {
        var response = new ForwardingResponse
        {
            IsSuccess = true,
            StatusCode = (int)httpResponse.StatusCode,
            UsedProtocol = usedProtocol,
            Duration = duration,
            _httpResponse = httpResponse
        };

        // 提取响应头
        foreach (var header in httpResponse.Headers)
        {
            response.Headers[header.Key] = header.Value.ToArray();
        }

        if (httpResponse.Content?.Headers != null)
        {
            foreach (var header in httpResponse.Content.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        // 读取响应体
        if (httpResponse.Content != null)
        {
            response.Content = await httpResponse.Content.ReadAsByteArrayAsync();
        }

        return response;
    }

    /// <summary>
    /// 创建失败响应
    /// </summary>
    public static ForwardingResponse CreateFailure(
        string errorMessage,
        TimeSpan duration,
        int statusCode = 502)
    {
        return new ForwardingResponse
        {
            IsSuccess = false,
            StatusCode = statusCode,
            ErrorMessage = errorMessage,
            Duration = duration
        };
    }

    /// <summary>
    /// 写出响应到HTTP上下文
    /// </summary>
    public async Task WriteToHttpContextAsync(HttpContext context)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ForwardingResponse));

        try
        {
            // 1. 写入状态码
            context.Response.StatusCode = StatusCode;

            // 2. 写入响应头
            foreach (var header in Headers)
            {
                if (!header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Headers[header.Key] = header.Value;
                }
            }

            // 3. 写入响应体
            if (IsSuccess && Content != null && Content.Length > 0)
            {
                await context.Response.Body.WriteAsync(Content);
            }
            else if (!IsSuccess && !string.IsNullOrEmpty(ErrorMessage))
            {
                await context.Response.WriteAsync(ErrorMessage);
            }
        }
        finally
        {
            await DisposeAsync();
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        if (_httpResponse != null)
        {
            _httpResponse.Dispose();
            _httpResponse = null;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public static ForwardingResponse CreateTimeout(TimeSpan elapsed, string errorMessage)
    {
        return new ForwardingResponse
        {
            IsSuccess = false,
            StatusCode = 504,
            ErrorMessage = errorMessage,
            Duration = elapsed
        };
    }
}
using Domain.Services.Configuration;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Protocol.Streaming;

/// <summary>
/// 流式转发响应
/// 封装HTTP响应，但响应体Stream保持未读取状态，延迟到实际写出时才读取
/// </summary>
public class StreamingForwardingResponse : IAsyncDisposable
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
    /// 内容长度（如果已知）
    /// </summary>
    public long? ContentLength { get; private set; }

    /// <summary>
    /// 内容类型
    /// </summary>
    public string? ContentType { get; private set; }

    /// <summary>
    /// 是否支持Range（断点续传）
    /// </summary>
    public bool SupportsRange { get; private set; }

    private StreamingForwardingResponse()
    {
        Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static StreamingForwardingResponse CreateSuccess(
        HttpResponseMessage httpResponse,
        ProtocolType usedProtocol,
        TimeSpan duration)
    {
        var response = new StreamingForwardingResponse
        {
            IsSuccess = true,
            StatusCode = (int)httpResponse.StatusCode,
            UsedProtocol = usedProtocol,
            Duration = duration,
            _httpResponse = httpResponse
        };

        response.ExtractHeaders(httpResponse);
        return response;
    }

    /// <summary>
    /// 创建失败响应
    /// </summary>
    public static StreamingForwardingResponse CreateFailure(
        string errorMessage,
        TimeSpan duration,
        int statusCode = 502)
    {
        return new StreamingForwardingResponse
        {
            IsSuccess = false,
            StatusCode = statusCode,
            ErrorMessage = errorMessage,
            Duration = duration
        };
    }

    /// <summary>
    /// 创建超时响应
    /// </summary>
    public static StreamingForwardingResponse CreateTimeout(TimeSpan duration)
    {
        return new StreamingForwardingResponse
        {
            IsSuccess = false,
            StatusCode = 504,
            ErrorMessage = "Gateway Timeout",
            Duration = duration
        };
    }

    /// <summary>
    /// 流式写出响应到HTTP上下文
    /// 此方法会消费响应体Stream，调用后会自动释放资源
    /// </summary>
    public async Task WriteToHttpContextAsync(
        HttpContext context,
        int bufferSize = 81920,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(StreamingForwardingResponse));

        try
        {
            // 1. 写入状态码
            context.Response.StatusCode = StatusCode;

            // 2. 写入响应头
            foreach (var header in Headers)
            {
                if (HeaderHelper.IsValidResponseHeader(header.Key))
                {
                    context.Response.Headers[header.Key] = header.Value;
                }
            }

            // 3. 流式复制响应体
            if (IsSuccess && _httpResponse?.Content != null)
            {
                await using var responseStream = await _httpResponse.Content.ReadAsStreamAsync(cancellationToken);
                
                await responseStream.CopyToAsync(
                    context.Response.Body,
                    bufferSize,
                    cancellationToken);
            }
            else if (!IsSuccess && !string.IsNullOrEmpty(ErrorMessage))
            {
                await context.Response.WriteAsync(ErrorMessage, cancellationToken);
            }
        }
        finally
        {
            await DisposeAsync();
        }
    }

    /// <summary>
    /// 提取HTTP响应头
    /// </summary>
    private void ExtractHeaders(HttpResponseMessage httpResponse)
    {
        // 提取响应头
        foreach (var header in httpResponse.Headers)
        {
            Headers[header.Key] = header.Value.ToArray();
        }

        // 提取Content头
        if (httpResponse.Content?.Headers != null)
        {
            foreach (var header in httpResponse.Content.Headers)
            {
                Headers[header.Key] = header.Value.ToArray();
            }

            ContentLength = httpResponse.Content.Headers.ContentLength;
            ContentType = httpResponse.Content.Headers.ContentType?.ToString();
            SupportsRange = httpResponse.Content.Headers.ContentRange != null ||
                           httpResponse.Headers.AcceptRanges.Contains("bytes");
        }
    }

    /// <summary>
    /// 获取协议类型
    /// </summary>
    public static ProtocolType GetProtocolTypeFromVersion(Version version)
    {
        return version.Major switch
        {
            3 => ProtocolType.Http3,
            2 => ProtocolType.Http2,
            1 when version.Minor == 1 => ProtocolType.Http11,
            1 when version.Minor == 0 => ProtocolType.Http10,
            _ => ProtocolType.Http11
        };
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
}
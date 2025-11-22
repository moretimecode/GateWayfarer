using Domain.Routes.Contexts;
using Domain.Services.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Infrastructure.Protocol.Streaming;

/// <summary>
/// 流式转发器实现
/// 透明地流式转发请求/响应，不处理业务逻辑
/// </summary>
public class StreamingForwarder : IStreamingForwarder
{
    private readonly IProtocolDetector _protocolDetector;
    private readonly IProtocolAwareHttpClientFactory _httpClientFactory;
    private readonly IStreamingConcurrencyLimiter _concurrencyLimiter;
    private readonly IOptionsMonitor<ServiceOptions> _serviceOptions;
    private readonly ILogger<StreamingForwarder> _logger;
    private readonly StreamingForwardingOptions _options;

    public StreamingForwarder(
        IProtocolDetector protocolDetector,
        IProtocolAwareHttpClientFactory httpClientFactory,
        IStreamingConcurrencyLimiter concurrencyLimiter,
        IOptionsMonitor<ServiceOptions> serviceOptions,
        ILogger<StreamingForwarder> logger,
        IOptions<StreamingForwardingOptions> options)
    {
        _protocolDetector = protocolDetector;
        _httpClientFactory = httpClientFactory;
        _concurrencyLimiter = concurrencyLimiter;
        _serviceOptions = serviceOptions;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 检查请求是否应该使用流式转发
    /// </summary>
    public bool ShouldUseStreaming(HttpContext context)
    {
        var request = context.Request;

        // 1. 检查Content-Type
        var contentType = request.ContentType?.ToLowerInvariant();
        if (contentType != null)
        {
            foreach (var streamingType in _options.StreamingContentTypes)
            {
                if (contentType.Contains(streamingType.ToLowerInvariant()))
                {
                    return true;
                }
            }
        }

        // 2. 检查Content-Length
        if (request.ContentLength.HasValue && 
            request.ContentLength.Value > _options.StreamingThreshold)
        {
            return true;
        }

        // 3. 检查是否是Range请求（断点续传）
        if (_options.EnableRangeSupport && request.Headers.ContainsKey("Range"))
        {
            return true;
        }

        // 4. 检查Accept头
        var accept = request.Headers.Accept.ToString().ToLowerInvariant();
        if (accept.Contains("application/octet-stream") ||
            accept.Contains("video/") ||
            accept.Contains("audio/"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 流式转发请求
    /// </summary>
    public async Task<StreamingForwardingResponse> ForwardAsync(
        HttpContext context,
        RouteMatchResult routeMatchResult,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // 1. 尝试获取并发许可
        var acquired = await _concurrencyLimiter.TryAcquireAsync(
            TimeSpan.FromSeconds(5),
            cancellationToken);

        if (!acquired)
        {
            stopwatch.Stop();
            _logger.LogWarning("流式转发服务繁忙，当前并发: {Current}/{Max}",
                _concurrencyLimiter.CurrentCount,
                _concurrencyLimiter.MaxCount);

            return StreamingForwardingResponse.CreateFailure(
                "流式转发服务繁忙，请稍后重试",
                stopwatch.Elapsed,
                503);
        }

        try
        {
            var serviceCode = routeMatchResult.Route.ServiceCode;
            var destination = routeMatchResult.SelectedDestination;

            _logger.LogInformation("开始流式转发: 服务={ServiceCode}, 目标={Destination}, ContentLength={ContentLength}",
                serviceCode, destination.GetAddress(), context.Request.ContentLength);

            // 2. 检测客户端协议
            var protocolDetectionResult = _protocolDetector.DetectProtocol(context);
            if (!protocolDetectionResult.IsSuccess)
            {
                stopwatch.Stop();
                return StreamingForwardingResponse.CreateFailure(
                    $"协议检测失败: {protocolDetectionResult.ErrorMessage}",
                    stopwatch.Elapsed);
            }

            var clientProtocolInfo = protocolDetectionResult.ProtocolInfo!;

            // 3. 构建目标URI
            var targetPath = context.Request.Path + context.Request.QueryString;
            var targetUri = BuildTargetUri(destination.GetAddress(), targetPath);

            // 4. 获取HttpClient（协议自适应）
            var httpClient = _httpClientFactory.CreateClient(serviceCode, clientProtocolInfo.Type);

            // 5. 创建流式HTTP请求
            var httpRequest = CreateStreamingRequest(context, targetUri);

            // 6. 发送请求（不等待完整响应，立即返回）
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.StreamTimeout);

            var httpResponse = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead, // 关键：只读取响应头就返回
                timeoutCts.Token);

            stopwatch.Stop();

            // 7. 创建响应（响应体仍然是Stream，未读取）
            var usedProtocol = StreamingForwardingResponse.GetProtocolTypeFromVersion(httpResponse.Version);
            var response = StreamingForwardingResponse.CreateSuccess(httpResponse, usedProtocol, stopwatch.Elapsed);

            _logger.LogInformation("流式转发成功: 服务={ServiceCode}, 协议={Protocol}, 耗时={Duration}ms, ContentLength={ContentLength}",
                serviceCode, usedProtocol, stopwatch.ElapsedMilliseconds, response.ContentLength);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning("流式转发被取消");
            return StreamingForwardingResponse.CreateFailure("请求被取消", stopwatch.Elapsed, 499);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("流式转发超时: {Timeout}", _options.StreamTimeout);
            return StreamingForwardingResponse.CreateTimeout(stopwatch.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "HTTP请求异常");
            return StreamingForwardingResponse.CreateFailure($"HTTP请求异常: {ex.Message}", stopwatch.Elapsed, 502);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "流式转发失败");
            return StreamingForwardingResponse.CreateFailure($"流式转发失败: {ex.Message}", stopwatch.Elapsed);
        }
        finally
        {
            // 释放并发许可
            _concurrencyLimiter.Release();
        }
    }

    /// <summary>
    /// 创建流式HTTP请求
    /// </summary>
    private HttpRequestMessage CreateStreamingRequest(HttpContext context, Uri targetUri)
    {
        var method = new HttpMethod(context.Request.Method);
        var httpRequest = new HttpRequestMessage(method, targetUri);

        // 1. 过滤并复制请求头
        Dictionary<string, string[]> filteredHeaders;
        
        if (_options.EnableStrictHeaderFiltering)
        {
            filteredHeaders = HeaderHelper.FilterRequestHeaders(
                context.Request.Headers,
                _options.HeaderBlacklist);
        }
        else
        {
            // 不过滤，直接复制（排除Content-*）
            filteredHeaders = new Dictionary<string, string[]>();
            foreach (var header in context.Request.Headers)
            {
                if (!header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) &&
                    !header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    filteredHeaders[header.Key] = header.Value.ToArray();
                }
            }
        }

        // 复制到HttpRequestMessage
        foreach (var header in filteredHeaders)
        {
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // 2. 添加代理标识头
        if (_options.AddProxyHeaders)
        {
            HeaderHelper.AddProxyHeaders(httpRequest, context);
        }

        // 3. 流式复制请求体（关键：不读取整个请求体到内存）
        if (HasRequestBody(context.Request))
        {
            var streamContent = new StreamContent(context.Request.Body, _options.BufferSize);

            // 复制Content-*头
            HeaderHelper.CopyContentHeaders(context.Request.Headers, streamContent);

            httpRequest.Content = streamContent;
        }

        return httpRequest;
    }

    /// <summary>
    /// 判断请求是否有Body
    /// </summary>
    private bool HasRequestBody(HttpRequest request)
    {
        return request.ContentLength > 0 ||
               (request.Body.CanRead &&
                request.Method != "GET" &&
                request.Method != "HEAD" &&
                request.Method != "DELETE" &&
                request.Method != "TRACE");
    }

    /// <summary>
    /// 构建目标URI
    /// </summary>
    private Uri BuildTargetUri(string baseUrl, string path)
    {
        var baseUri = new Uri(baseUrl.TrimEnd('/'));
        var targetPath = path.TrimStart('/');
        return new Uri(baseUri, targetPath);
    }
}
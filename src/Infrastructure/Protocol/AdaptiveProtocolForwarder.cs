using Domain.Routes.Contexts;
using Domain.Services.Configuration;
using Infrastructure.Protocol.Streaming;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Infrastructure.Protocol;

/// <summary>
/// 自适应协议转发器实现
/// 负责协议检测、协商和转发，使用RouteMatchingService的路由匹配结果
/// </summary>
public class AdaptiveProtocolForwarder : IAdaptiveProtocolForwarder
{
    private readonly IProtocolDetector _protocolDetector;
    private readonly IProtocolAwareHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<ServiceOptions> _serviceOptions;
    private readonly ILogger<AdaptiveProtocolForwarder> _logger;
    private readonly ProtocolForwardingOptions _options;

    public AdaptiveProtocolForwarder(
        IProtocolDetector protocolDetector,
        IProtocolAwareHttpClientFactory httpClientFactory,
        IOptionsMonitor<ServiceOptions> serviceOptions,
        ILogger<AdaptiveProtocolForwarder> logger,
        IOptions<ProtocolForwardingOptions> options)
    {
        _protocolDetector = protocolDetector;
        _httpClientFactory = httpClientFactory;
        _serviceOptions = serviceOptions;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 通过RouteMatchResult转发请求
    /// </summary>
    public async Task<ForwardingResponse> ForwardToDestinationAsync(
        HttpContext context,
        RouteMatchResult routeMatchResult,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var serviceCode = routeMatchResult.Route.ServiceCode;
            var destination = routeMatchResult.SelectedDestination;

            _logger.LogDebug("开始协议转发: 服务={ServiceCode}, 目标={Destination}",
                serviceCode, destination.GetAddress());

            // 1. 检测客户端协议
            var protocolDetectionResult = _protocolDetector.DetectProtocol(context);
            if (!protocolDetectionResult.IsSuccess)
            {
                _logger.LogWarning("协议检测失败: {Error}", protocolDetectionResult.ErrorMessage);
                return ForwardingResponse.CreateFailure(
                    $"协议检测失败: {protocolDetectionResult.ErrorMessage}",
                    stopwatch.Elapsed);
            }

            var clientProtocolInfo = protocolDetectionResult.ProtocolInfo!;
            _logger.LogDebug("检测到客户端协议: {Protocol}", clientProtocolInfo.Type);

            // 2. 构建目标URI（直接使用当前请求路径）
            var targetPath = context.Request.Path + context.Request.QueryString;
            var targetUri = BuildTargetUri(destination.GetAddress(), targetPath);

            // 3. 创建转发请求
            var forwardingRequest = await ForwardingRequest.FromHttpContextAsync(
                context, targetUri, clientProtocolInfo.Type);

            // 4. 设置协议偏好
            SetProtocolPreference(forwardingRequest, clientProtocolInfo, serviceCode);

            // 5. 获取专用HttpClient
            var httpClient = _httpClientFactory.CreateClient(serviceCode, forwardingRequest.PreferredServerProtocol);

            // 6. 执行转发
            return await ExecuteForwardingAsync(forwardingRequest, httpClient, serviceCode, cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "转发失败: 服务={ServiceCode}", routeMatchResult.Route.ServiceCode);
            return ForwardingResponse.CreateFailure($"转发失败: {ex.Message}", stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// 设置协议偏好
    /// </summary>
    private void SetProtocolPreference(ForwardingRequest forwardingRequest, ProtocolInfo clientProtocolInfo, string serviceCode)
    {
        var serviceConfig = GetServiceConfig(serviceCode);

        if (serviceConfig?.Protocol != null)
        {
            // 使用服务配置的协议偏好
            SetServiceProtocolPreference(forwardingRequest, clientProtocolInfo, serviceConfig.Protocol);
        }
        else
        {
            // 使用全局协议偏好
            SetGlobalProtocolPreference(forwardingRequest, clientProtocolInfo);
        }
    }

    /// <summary>
    /// 基于服务配置设置协议偏好
    /// </summary>
    private void SetServiceProtocolPreference(ForwardingRequest forwardingRequest, ProtocolInfo clientProtocolInfo, ProtocolConfigItem protocolConfig)
    {
        // 1. 强制协议
        if (protocolConfig.ForceProtocolVersion.HasValue)
        {
            forwardingRequest.PreferredServerProtocol = protocolConfig.ForceProtocolVersion.Value;
            _logger.LogDebug("使用服务强制协议: {Protocol}", protocolConfig.ForceProtocolVersion.Value);
            return;
        }

        // 2. 禁用协议协商
        if (!_options.EnableProtocolNegotiation)
        {
            forwardingRequest.PreferredServerProtocol = clientProtocolInfo.Type;
            return;
        }

        // 3. 客户端协议在服务支持列表中
        if (protocolConfig.SupportedProtocols.Contains(clientProtocolInfo.Type))
        {
            forwardingRequest.PreferredServerProtocol = clientProtocolInfo.Type;
            _logger.LogDebug("使用客户端协议: {Protocol}", clientProtocolInfo.Type);
            return;
        }

        // 4. 服务首选协议
        var preferredProtocol = protocolConfig.PreferredProtocols.FirstOrDefault();
        forwardingRequest.PreferredServerProtocol = preferredProtocol;
        _logger.LogDebug("使用服务首选协议: {Protocol}", preferredProtocol);
    }

    /// <summary>
    /// 设置全局协议偏好
    /// </summary>
    private void SetGlobalProtocolPreference(ForwardingRequest forwardingRequest, ProtocolInfo clientProtocolInfo)
    {
        if (!_options.EnableProtocolNegotiation)
        {
            forwardingRequest.PreferredServerProtocol = clientProtocolInfo.Type;
            return;
        }

        // 使用全局首选协议
        forwardingRequest.PreferredServerProtocol = _options.PreferredProtocols.FirstOrDefault();
        _logger.LogDebug("使用全局首选协议: {Protocol}", forwardingRequest.PreferredServerProtocol);
    }

    /// <summary>
    /// 执行转发
    /// </summary>
    private async Task<ForwardingResponse> ExecuteForwardingAsync(
        ForwardingRequest forwardingRequest,
        HttpClient httpClient,
        string serviceCode,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeout = forwardingRequest.Timeout ?? _options.DefaultTimeout;

        try
        {
            var httpRequestMessage = forwardingRequest.ToHttpRequestMessage();
            timeoutCts.CancelAfter(timeout);

            var httpResponse = await httpClient.SendAsync(
                httpRequestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);

            stopwatch.Stop();

            // 检测实际使用的协议
            var usedProtocol = StreamingForwardingResponse.GetProtocolTypeFromVersion(httpResponse.Version);

            // 记录协议协商结果
            LogProtocolNegotiation(forwardingRequest, usedProtocol, serviceCode);

            // 创建响应
            var response = await ForwardingResponse.CreateSuccessAsync(httpResponse, usedProtocol, stopwatch.Elapsed);

            _logger.LogDebug("转发成功: 服务={ServiceCode}, 协议={Protocol}, 耗时={Duration}ms",
                serviceCode, usedProtocol, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            string message = $"转发超时: 服务={serviceCode}, 超时时间={timeout}";
            _logger.LogWarning(message);
            return ForwardingResponse.CreateTimeout(stopwatch.Elapsed, message);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "HTTP请求异常: 服务={ServiceCode}", serviceCode);
            return ForwardingResponse.CreateFailure($"HTTP请求异常: {ex.Message}", stopwatch.Elapsed, 502);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "转发异常: 服务={ServiceCode}", serviceCode);
            return ForwardingResponse.CreateFailure($"转发异常: {ex.Message}", stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// 从ServiceOptions获取服务配置
    /// </summary>
    private ServiceConfigItem? GetServiceConfig(string serviceCode)
    {
        var services = _serviceOptions.CurrentValue.Services;
        return services.FirstOrDefault(s => s.Code.Equals(serviceCode, StringComparison.OrdinalIgnoreCase));
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

    /// <summary>
    /// 记录协议协商结果
    /// </summary>
    private void LogProtocolNegotiation(ForwardingRequest request, ProtocolType usedProtocol, string serviceCode)
    {
        var clientProtocol = request.ClientProtocol;
        var preferredProtocol = request.PreferredServerProtocol;

        if (preferredProtocol.HasValue && usedProtocol != preferredProtocol.Value)
        {
            if (_options.LogProtocolDowngrade)
            {
                _logger.LogWarning("协议协商降级: 服务={ServiceCode}, 客户端={ClientProtocol}, 期望={PreferredProtocol}, 实际={UsedProtocol}",
                    serviceCode, clientProtocol, preferredProtocol.Value, usedProtocol);
            }
        }
        else
        {
            _logger.LogDebug("协议协商成功: 服务={ServiceCode}, 客户端={ClientProtocol}, 服务端={UsedProtocol}",
                serviceCode, clientProtocol, usedProtocol);
        }
    }
}
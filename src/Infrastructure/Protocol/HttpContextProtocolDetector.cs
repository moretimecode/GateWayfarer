using Domain.Services.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;

namespace Infrastructure.Protocol;

/// <summary>
/// 基于HttpContext的协议检测器实现
/// 通过分析HTTP请求的各种特征来识别协议类型
/// </summary>
public class HttpContextProtocolDetector : IProtocolDetector
{
    private readonly ILogger<HttpContextProtocolDetector> _logger;

    // 支持的协议类型（按检测优先级排序）
    private static readonly IReadOnlyList<ProtocolType> _supportedProtocols = new[]
    {
        ProtocolType.Grpc,     
        ProtocolType.WebSocket,
        ProtocolType.Http3,    
        ProtocolType.Http2,   
        ProtocolType.Http11,    
        ProtocolType.Http10 
    };

    public HttpContextProtocolDetector(ILogger<HttpContextProtocolDetector> logger)
    {
        _logger = logger;
    }

    public string Name => "HttpContext Protocol Detector";
    public int Priority => 100;
    public IReadOnlyList<ProtocolType> SupportedProtocols => _supportedProtocols;

    public ProtocolDetectionResult DetectProtocol(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();

        try
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(context.Request);

            _logger.LogDebug("开始检测协议: {RequestPath} {Method}", 
                context.Request.Path, context.Request.Method);

            if (IsGrpcRequest(context.Request))
            {
                var grpcInfo = CreateGrpcProtocolInfo(context.Request, warnings);
                stopwatch.Stop();
                
                _logger.LogDebug("检测到gRPC协议: {Protocol}", grpcInfo);
                return ProtocolDetectionResult.Success(grpcInfo, stopwatch.Elapsed, warnings);
            }

            // 2. 检测WebSocket升级请求
            if (IsWebSocketUpgradeRequest(context.Request))
            {
                var wsInfo = CreateWebSocketProtocolInfo(context.Request, warnings);
                stopwatch.Stop();

                _logger.LogDebug("检测到WebSocket升级请求: {Protocol}", wsInfo);
                return ProtocolDetectionResult.Success(wsInfo, stopwatch.Elapsed, warnings);
            }

            // 3. 检测HTTP协议版本
            var httpProtocolInfo = DetectHttpProtocol(context.Request, warnings);
            stopwatch.Stop();

            _logger.LogDebug("检测到HTTP协议: {Protocol}", httpProtocolInfo);
            return ProtocolDetectionResult.Success(httpProtocolInfo, stopwatch.Elapsed, warnings);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "协议检测失败");
            
            return ProtocolDetectionResult.Failure(
                $"协议检测失败: {ex.Message}", 
                stopwatch.Elapsed);
        }
    }

    public bool CanDetect(ProtocolType protocol)
    {
        return _supportedProtocols.Contains(protocol);
    }

    /// <summary>
    /// 检测gRPC请求
    /// </summary>
    private bool IsGrpcRequest(HttpRequest request)
    {
        try
        {
            var hasGrpcContentType = !string.IsNullOrEmpty(request.ContentType) &&
                                   request.ContentType.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase);

            if (hasGrpcContentType)
            {
                _logger.LogDebug("通过Content-Type检测到gRPC: {ContentType}", request.ContentType);
                return true;
            }

            var hasGrpcHeaders = request.Headers.ContainsKey("grpc-encoding") ||
                               request.Headers.ContainsKey("grpc-accept-encoding") ||
                               request.Headers.ContainsKey("grpc-status") ||
                               request.Headers.ContainsKey("grpc-message");

            var hasTrailersSupport = request.Headers.TryGetValue("TE", out var teValues) &&
                                   teValues.Any(v => v.Contains("trailers", StringComparison.OrdinalIgnoreCase));

            var isHttp2OrAbove = request.Protocol.StartsWith("HTTP/2", StringComparison.OrdinalIgnoreCase) ||
                               request.Protocol.StartsWith("HTTP/3", StringComparison.OrdinalIgnoreCase);

            var isGrpc = hasGrpcHeaders && (hasTrailersSupport || isHttp2OrAbove);

            if (isGrpc)
            {
                _logger.LogDebug("通过头部特征检测到gRPC: HasGrpcHeaders={HasGrpcHeaders}, HasTrailers={HasTrailers}, Protocol={Protocol}",
                    hasGrpcHeaders, hasTrailersSupport, request.Protocol);
            }

            return isGrpc;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "gRPC检测过程中发生异常");
            return false;
        }
    }

    /// <summary>
    /// 检测WebSocket升级请求
    /// </summary>
    private bool IsWebSocketUpgradeRequest(HttpRequest request)
    {
        try
        {
            // RFC 6455: WebSocket协议升级的标准检测
            var hasUpgradeHeader = request.Headers.TryGetValue("Upgrade", out var upgradeValues) &&
                                 upgradeValues.Any(v => string.Equals(v, "websocket", StringComparison.OrdinalIgnoreCase));

            var hasConnectionUpgrade = request.Headers.TryGetValue("Connection", out var connectionValues) &&
                                     connectionValues.Any(v => v.Contains("Upgrade", StringComparison.OrdinalIgnoreCase));

            var hasWebSocketKey = request.Headers.ContainsKey("Sec-WebSocket-Key");
            
            var hasWebSocketVersion = request.Headers.TryGetValue("Sec-WebSocket-Version", out var versionValues) &&
                                    versionValues.Any(v => v == "13"); // WebSocket协议版本13是标准版本

            var isWebSocketUpgrade = hasUpgradeHeader && hasConnectionUpgrade && hasWebSocketKey && hasWebSocketVersion;

            if (isWebSocketUpgrade)
            { 
                _logger.LogDebug("检测到WebSocket升级请求: Upgrade={Upgrade}, Connection={Connection}, Key={HasKey}, Version={Version}",
                   upgradeValues ,connectionValues, hasWebSocketKey, versionValues);
            }

            return isWebSocketUpgrade;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebSocket检测过程中发生异常");
            return false;
        }
    }

    /// <summary>
    /// 检测HTTP协议版本
    /// </summary>
    private ProtocolInfo DetectHttpProtocol(HttpRequest request, List<string> warnings)
    {
        var protocolString = request.Protocol;
        var isSecure = request.IsHttps;
        var userAgent = request.Headers.UserAgent.FirstOrDefault();

        // 解析协议版本
        var protocolType = ParseHttpProtocolVersion(protocolString);

        // HTTP/2协议的额外验证
        if (protocolType == ProtocolType.Http2)
        {
            if (!isSecure)
            {
                warnings.Add("HTTP/2 over cleartext (h2c) detected, which is less common");
            }

            // 检查是否有HTTP/2特有的伪头部
            if (request.Headers.Any(h => h.Key.StartsWith(":")))
            {
                _logger.LogDebug("检测到HTTP/2伪头部");
            }
        }

        // HTTP/3协议的额外验证
        if (protocolType == ProtocolType.Http3)
        {
            if (!isSecure)
            {
                warnings.Add("HTTP/3 requires QUIC/TLS, but non-secure connection detected");
            }
        }

        return ProtocolInfo.CreateHttp(protocolType, protocolString, isSecure, userAgent);
    }

    /// <summary>
    /// 解析HTTP协议版本字符串
    /// </summary>
    private ProtocolType ParseHttpProtocolVersion(string protocolString)
    {
        if (string.IsNullOrWhiteSpace(protocolString))
        {
            _logger.LogWarning("协议字符串为空，默认使用HTTP/1.1");
            return ProtocolType.Http11;
        }

        return protocolString.ToUpperInvariant().Trim() switch
        {
            "HTTP/1.0" => ProtocolType.Http10,
            "HTTP/1.1" => ProtocolType.Http11,
            "HTTP/2" => ProtocolType.Http2,
            "HTTP/2.0" => ProtocolType.Http2,
            "HTTP/3" => ProtocolType.Http3,
            "HTTP/3.0" => ProtocolType.Http3,
            _ => HandleUnknownProtocol(protocolString)
        };
    }

    /// <summary>
    /// 处理未知协议
    /// </summary>
    private ProtocolType HandleUnknownProtocol(string protocolString)
    {
        _logger.LogWarning("未知的协议字符串: {Protocol}，默认使用HTTP/1.1", protocolString);
        
        // 尝试从协议字符串中推断
        if (protocolString.Contains("2", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("协议字符串包含'2'，推测为HTTP/2: {Protocol}", protocolString);
            return ProtocolType.Http2;
        }

        if (protocolString.Contains("3", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("协议字符串包含'3'，推测为HTTP/3: {Protocol}", protocolString);
            return ProtocolType.Http3;
        }

        // 默认返回HTTP/1.1
        return ProtocolType.Http11;
    }

    /// <summary>
    /// 创建gRPC协议信息
    /// </summary>
    private ProtocolInfo CreateGrpcProtocolInfo(HttpRequest request, List<string> warnings)
    {
        var isSecure = request.IsHttps;
        var userAgent = request.Headers.UserAgent.FirstOrDefault();
        
        var metadata = new Dictionary<string, object>();
        
        // 收集gRPC相关的头部信息
        if (request.Headers.TryGetValue("grpc-encoding", out var encoding))
        {
            metadata["grpc-encoding"] = encoding.ToString();
        }
        
        if (request.Headers.TryGetValue("grpc-accept-encoding", out var acceptEncoding))
        {
            metadata["grpc-accept-encoding"] = acceptEncoding.ToString();
        }

        if (!string.IsNullOrEmpty(request.ContentType))
        {
            metadata["content-type"] = request.ContentType;
        }

        // 检查是否为gRPC-Web（在HTTP/1.1上运行的gRPC）
        if (!request.Protocol.StartsWith("HTTP/2", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"gRPC detected on {request.Protocol}, which may be gRPC-Web or non-standard implementation");
            metadata["grpc-variant"] = "grpc-web";
        }

        return ProtocolInfo.CreateGrpc(isSecure, userAgent, metadata);
    }

    /// <summary>
    /// 创建WebSocket协议信息
    /// </summary>
    private ProtocolInfo CreateWebSocketProtocolInfo(HttpRequest request, List<string> warnings)
    {
        var isSecure = request.IsHttps;
        var userAgent = request.Headers.UserAgent.FirstOrDefault();

        var metadata = new Dictionary<string, object>();

        // 收集WebSocket相关信息
        if (request.Headers.TryGetValue("Sec-WebSocket-Version", out var version))
        {
            metadata["websocket-version"] = version.ToString();
        }

        if (request.Headers.TryGetValue("Sec-WebSocket-Protocol", out var protocols))
        {
            metadata["websocket-protocols"] = protocols.ToArray();
        }

        if (request.Headers.TryGetValue("Sec-WebSocket-Extensions", out var extensions))
        {
            metadata["websocket-extensions"] = extensions.ToString();
        }

        return ProtocolInfo.CreateWebSocket(isSecure, userAgent, metadata);
    }
}
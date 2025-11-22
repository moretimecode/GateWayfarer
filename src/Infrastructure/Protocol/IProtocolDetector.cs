using Domain.Services.Configuration;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Protocol;

/// <summary>
/// 协议检测器接口
/// 负责从HTTP上下文中检测客户端使用的协议
/// </summary>
public interface IProtocolDetector
{
    /// <summary>
    /// 检测HTTP上下文中的协议信息
    /// </summary>
    /// <param name="context">HTTP上下文</param>
    /// <returns>协议检测结果</returns>
    ProtocolDetectionResult DetectProtocol(HttpContext context);

    /// <summary>
    /// 判断检测器是否支持检测指定协议
    /// </summary>
    /// <param name="protocol">协议类型</param>
    /// <returns>是否支持</returns>
    bool CanDetect(ProtocolType protocol);

    /// <summary>
    /// 获取检测器支持的所有协议类型
    /// </summary>
    IReadOnlyList<ProtocolType> SupportedProtocols { get; }

    /// <summary>
    /// 检测器名称（用于日志和诊断）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 检测器优先级（数值越高优先级越高）
    /// </summary>
    int Priority { get; }
}
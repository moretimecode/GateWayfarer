using Domain.Services.Configuration;

namespace Infrastructure.Protocol;

/// <summary>
/// 协议感知的HttpClient工厂接口
/// 为不同的服务和协议创建优化的HttpClient实例
/// </summary>
public interface IProtocolAwareHttpClientFactory
{
    /// <summary>
    /// 为指定服务创建HttpClient
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="preferredProtocol">首选协议</param>
    /// <returns>HttpClient实例</returns>
    HttpClient CreateClient(string serviceName, ProtocolType? preferredProtocol = null);


    /// <summary>
    /// 释放指定服务的所有HttpClient连接
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    void ClearConnections(string serviceName);
}
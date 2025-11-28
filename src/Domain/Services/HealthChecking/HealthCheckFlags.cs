using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Services.HealthChecking
{
    [Flags]
    public enum HealthCheckFlags : byte
    {
        None = 0,
        IsSuccessful = 1,      // 第1位：检查是否成功
        IsTimeout = 2,         // 第2位：是否超时
        IsNetworkError = 4,    // 第3位：是否网络错误
        IsServerError = 8,     // 第4位：是否服务器错误（5xx）
        IsClientError = 16,    // 第5位：是否客户端错误（4xx）
        Slow = 32,        // 第6位：预留（如：是否慢请求）
        Reserved2 = 64,        // 第7位：预留（如：是否熔断触发）
        Reserved3 = 128        // 第8位：预留（如：是否人工标记）
    }

}
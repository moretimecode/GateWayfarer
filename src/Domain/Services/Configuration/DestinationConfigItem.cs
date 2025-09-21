using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Services.Configuration
{
    public class DestinationConfigItem
    {
        /// <summary>
        /// 实例代码
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 协议类型 (Http/Https)
        /// </summary>
        public Protocol Protocol { get; set; } = Protocol.Http;
        
        /// <summary>
        /// 主机地址
        /// </summary>
        public string Host { get; set; } = string.Empty;
        
        /// <summary>
        /// 端口号
        /// </summary>
        public int Port { get; set; }
        
        /// <summary>
        /// 权重 (用于负载均衡)
        /// </summary>
        public int Weight { get; set; } = 1;
        
        /// <summary>
        /// 超时时间 (秒)
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
        
        /// <summary>
        /// 健康检查路径 (可选，为空时使用服务级别的配置)
        /// </summary>
        public string? HealthPath { get; set; }
        
        /// <summary>
        /// 元数据信息
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
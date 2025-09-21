using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Services
{
    public class Destination : ValueObject
    {
        /// <summary>
        /// 目标实例的业务唯一标识
        /// </summary>
        public string Code { get; private set; }

        /// <summary>
        /// 协议类型
        /// </summary>
        public Protocol Protocol { get; private set; }

        /// <summary>
        /// 主机地址（域名或IP）
        /// </summary>
        public string Host { get; private set; }

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// 负载均衡权重
        /// </summary>
        public int Weight { get; private set; }

        /// <summary>
        /// 请求超时时间（秒）
        /// </summary>
        public int TimeoutSeconds { get; private set; }

        /// <summary>
        /// 健康检查路径（可选，如果为空则使用Service的HealthCheckConfig.ActivePath）
        /// </summary>
        public string? HealthPath { get; private set; }

        /// <summary>
        /// 扩展元数据
        /// </summary>
        public Dictionary<string, string> Metadata { get; private set; }

        // EF Core 需要的私有构造函数
        private Destination()
        {
            Code = string.Empty;
            Host = string.Empty;
            Metadata = new Dictionary<string, string>();
        }

        public Destination(
            string code,
            Protocol protocol,
            string host,
            int port,
            int weight = 1,
            int timeoutSeconds = 30,
            string? healthPath = null,
            Dictionary<string, string>? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Code cannot be empty", nameof(code));

            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host cannot be empty", nameof(host));

            if (port <= 0 || port > 65535)
                throw new ArgumentException("Port must be between 1 and 65535", nameof(port));

            if (weight <= 0)
                throw new ArgumentException("Weight must be greater than 0", nameof(weight));

            if (timeoutSeconds <= 0)
                throw new ArgumentException("Timeout must be greater than 0", nameof(timeoutSeconds));

            Code = code;
            Protocol = protocol;
            Host = host;
            Port = port;
            Weight = weight;
            TimeoutSeconds = timeoutSeconds;
            HealthPath = healthPath;
            Metadata = metadata ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// 获取完整的服务地址
        /// </summary>
        public string GetAddress()
        {
            var uriBuilder = new UriBuilder
            {
                Scheme = Protocol.ToString().ToLowerInvariant(),
                Host = Host,
                Port = Port
            };
            return uriBuilder.Uri.ToString().TrimEnd('/');
        }

        /// <summary>
        /// 获取健康检查的完整URL
        /// </summary>
        public string GetHealthCheckUrl(string defaultHealthPath = "/health")
        {
            var path = !string.IsNullOrWhiteSpace(HealthPath) ? HealthPath : defaultHealthPath;

            // 确保路径以 / 开头
            if (!path.StartsWith('/'))
                path = "/" + path;

            var uriBuilder = new UriBuilder
            {
                Scheme = Protocol.ToString().ToLowerInvariant(),
                Host = Host,
                Port = Port,
                Path = path
            };

            return uriBuilder.Uri.ToString();
        }

        public IEnumerable<object> GetEqualityComponents()
        {
            yield return Code;
            yield return Protocol;
            yield return Host;
            yield return Port;
            yield return Weight;
            yield return TimeoutSeconds;
            yield return HealthPath ?? string.Empty;

            foreach (var kvp in Metadata.OrderBy(x => x.Key))
            {
                yield return kvp.Key;
                yield return kvp.Value;
            }
        }
    }

}
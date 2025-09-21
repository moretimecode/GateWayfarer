using Domain.Services;

namespace Domain.Routes.Services
{
    /// <summary>
    /// 负载均衡策略接口
    /// </summary>
    public interface ILoadBalanceStrategy
    {
        /// <summary>
        /// 策略类型
        /// </summary>
        LoadBalanceType Type { get; }
        
        /// <summary>
        /// 选择目标实例
        /// </summary>
        Destination SelectDestination(List<Destination> destinations, LoadBalanceContext context);
    }

    public class LoadBalanceContext
    {
        public string RequestPath { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string ClientIp { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
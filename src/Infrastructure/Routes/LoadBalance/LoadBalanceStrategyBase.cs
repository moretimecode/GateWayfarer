using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Routes.LoadBalance
{
    /// <summary>
    /// 负载均衡策略抽象基类
    /// </summary>
    public abstract class LoadBalanceStrategyBase : ILoadBalanceStrategy
    {
        protected readonly ILogger Logger;

        protected LoadBalanceStrategyBase(ILogger logger)
        {
            Logger = logger;
        }

        public abstract LoadBalanceType Type { get; }

        public virtual Destination SelectDestination(List<Destination> destinations, LoadBalanceContext context)
        {
            // 基础验证
            if (!destinations.Any())
            {
                throw new InvalidOperationException("No destinations available for load balancing");
            }

            if (destinations.Count == 1)
            {
                Logger.LogDebug("Only one destination available, returning it directly");
                return destinations.First();
            }

            // 调用具体策略实现
            return SelectDestinationCore(destinations, context);
        }

        /// <summary>
        /// 具体策略实现
        /// </summary>
        protected abstract Destination SelectDestinationCore(List<Destination> destinations, LoadBalanceContext context);

        /// <summary>
        /// 生成安全的随机数
        /// </summary>
        protected static int GetRandomInt(int maxValue)
        {
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var value = Math.Abs(BitConverter.ToInt32(bytes, 0));
            return value % maxValue;
        }

        /// <summary>
        /// 根据权重计算总权重
        /// </summary>
        protected static int CalculateTotalWeight(List<Destination> destinations)
        {
            return destinations.Sum(d => d.Weight);
        }
    }
}
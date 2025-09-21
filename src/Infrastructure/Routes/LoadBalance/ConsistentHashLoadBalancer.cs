using System.Security.Cryptography;
using System.Text;
using Domain.Routes;
using Domain.Routes.Services;
using Infrastructure.Routes.LoadBalance;
using Microsoft.Extensions.Logging;

namespace Domain.Services.LoadBalancing
{
    /// <summary>
    /// 一致性哈希负载均衡器
    /// </summary>
    public class ConsistentHashLoadBalancer(ILogger<ConsistentHashLoadBalancer> logger) : LoadBalanceStrategyBase(logger)
    {
        private readonly SortedDictionary<uint, Destination> _ring = [];
        private readonly int _virtualNodes = 150;
        private readonly SHA1 _hasher = SHA1.Create();

        public override LoadBalanceType Type => LoadBalanceType.ConsistentHash;

        /// <summary>
        /// 添加节点到哈希环
        /// </summary>
        public void AddDestination(Destination destination)
        {
            for (int i = 0; i < _virtualNodes; i++)
            {
                var virtualKey = $"{destination.Code}:{i}";
                var hash = ComputeHash(virtualKey);
                _ring[hash] = destination;
            }
        }

        /// <summary>
        /// 从哈希环移除节点
        /// </summary>
        public void RemoveDestination(Destination destination)
        {
            for (int i = 0; i < _virtualNodes; i++)
            {
                var virtualKey = $"{destination.Code}:{i}";
                var hash = ComputeHash(virtualKey);
                _ring.Remove(hash);
            }
        }

        /// <summary>
        /// 根据key选择destination
        /// </summary>
        public Destination? SelectDestination(string key)
        {
            if (_ring.Count == 0) return null;

            var hash = ComputeHash(key);
            
            var node = _ring.FirstOrDefault(kvp => kvp.Key >= hash);

            return node.Key == 0 ? _ring.First().Value : node.Value;
        }

        protected override Destination SelectDestinationCore(List<Destination> destinations, LoadBalanceContext context)
        {
            throw new NotImplementedException();
        }

        private uint ComputeHash(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = _hasher.ComputeHash(bytes);
            return BitConverter.ToUInt32(hash, 0);
        }
    }
}
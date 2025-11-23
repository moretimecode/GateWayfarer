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
            // 确保哈希环与当前可用的目标节点同步
            SyncRingWithDestinations(destinations);

            // 从上下文中获取哈希键，如果没有则使用默认值
            var hashKey = GetHashKeyFromContext(context);

            var selectedDestination = SelectDestination(hashKey);

            if (selectedDestination == null)
            {
                Logger.LogWarning("No destination found for hash key: {HashKey}, falling back to first available destination", hashKey);
                return destinations.FirstOrDefault() ?? throw new InvalidOperationException("No available destinations");
            }

            Logger.LogInformation("Selected destination {DestinationCode} for hash key: {HashKey}",
                selectedDestination.Code, hashKey);

            return selectedDestination;
        }

        /// <summary>
        /// 同步哈希环与当前可用的目标节点
        /// </summary>
        private void SyncRingWithDestinations(List<Destination> currentDestinations)
        {
            lock (_lockObject)
            {
                // 获取当前环中所有唯一的目标节点
                var currentRingDestinations = _ring.Values.Distinct().ToList();

                // 移除不再可用的节点
                var destinationsToRemove = currentRingDestinations
                    .Where(dest => !currentDestinations.Any(cd => cd.Code == dest.Code))
                    .ToList();

                foreach (var dest in destinationsToRemove)
                {
                    RemoveDestination(dest);
                }

                // 添加新增的节点
                var destinationsToAdd = currentDestinations
                    .Where(cd => !currentRingDestinations.Any(rd => rd.Code == cd.Code))
                    .ToList();

                foreach (var dest in destinationsToAdd)
                {
                    AddDestination(dest);
                }
            }
        }

        /// <summary>
        /// 从上下文中获取哈希键
        /// </summary>
        private string GetHashKeyFromContext(LoadBalanceContext context)
        {
            if (!string.IsNullOrEmpty(context.ClientIp))
            {
                return context.ClientIp;
            }

            // 如果连客户端IP都没有，使用请求路径
            if (!string.IsNullOrEmpty(context.RequestPath))
            {
                return context.RequestPath;
            }

            // 最后回退到随机数
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 在哈希环中查找下一个节点
        /// </summary>
        private Destination FindNextNode(uint hash)
        {
            // 查找第一个键大于等于当前哈希的节点
            foreach (var kvp in _ring)
            {
                if (kvp.Key >= hash)
                {
                    return kvp.Value;
                }
            }

            // 如果没找到，回到环的开头（环状结构）
            return _ring.First().Value;
        }

        private uint ComputeHash(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = _hasher.ComputeHash(bytes);
            return BitConverter.ToUInt32(hash, 0);
        }
    }
}
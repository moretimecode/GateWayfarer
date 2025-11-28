using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Routes.LoadBalance
{
    public class LeastConnectionsStrategy : LoadBalanceStrategyBase
    {
        private static readonly Dictionary<string, int> _connectionCounts = new();
        private static readonly System.Threading.Lock _lock = new();
        public LeastConnectionsStrategy(ILogger<LeastConnectionsStrategy> logger) : base(logger)
        {
        }

        public override LoadBalanceType Type => LoadBalanceType.LeastConnections;

        protected override Destination SelectDestinationCore(List<Destination> destinations, LoadBalanceContext context)
        {
            lock (_lock)
            {
                var selected = destinations
                    .OrderBy(d => GetConnectionCount(d.Code))
                    .ThenBy(d => d.Code)
                    .First();

                IncrementConnectionCount(selected.Code);

                Logger.LogDebug("LeastConnections selected destination: {DestinationCode} (connections: {ConnectionCount})",
                    selected.Code, GetConnectionCount(selected.Code));

                return selected;
            }
        }

        private static int GetConnectionCount(string destinationCode)
        {
            return _connectionCounts.TryGetValue(destinationCode, out var count) ? count : 0;
        }

        private static void IncrementConnectionCount(string destinationCode)
        {
            _connectionCounts[destinationCode] = GetConnectionCount(destinationCode) + 1;
        }

        // 这个方法应该在请求完成时调用，减少连接数
        public static void DecrementConnectionCount(string destinationCode)
        {
            lock (_lock)
            {
                if (_connectionCounts.TryGetValue(destinationCode, out var count) && count > 0)
                {
                    _connectionCounts[destinationCode] = count - 1;
                }
            }
        }
    }
}
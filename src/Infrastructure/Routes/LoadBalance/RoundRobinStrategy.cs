using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Routes.LoadBalance
{
    public class RoundRobinStrategy : LoadBalanceStrategyBase
    {
        private static long _counter = 0;

        public RoundRobinStrategy(ILogger<RoundRobinStrategy> logger) : base(logger)
        {
        }

        public override LoadBalanceType Type => LoadBalanceType.RoundRobin;

        protected override Destination SelectDestinationCore(List<Destination> destinations, LoadBalanceContext context)
        {
            var index = (int)(Interlocked.Increment(ref _counter) % destinations.Count);
            var selected = destinations[index];
            
            Logger.LogDebug("RoundRobin selected destination: {DestinationCode}", selected.Code);
            return selected;
        }
    }
}
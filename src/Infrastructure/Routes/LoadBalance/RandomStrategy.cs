using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Routes.LoadBalance
{
    public class RandomStrategy(ILogger<RandomStrategy> logger) : LoadBalanceStrategyBase(logger)
    {
        public override LoadBalanceType Type => LoadBalanceType.Random;

        protected override Destination SelectDestinationCore(List<Destination> destinations, LoadBalanceContext context)
        {
            var index = GetRandomInt(destinations.Count);
            var selected = destinations[index];
            
            Logger.LogDebug("Random selected destination: {DestinationCode}", selected.Code);
            return selected;
        }
    }
}
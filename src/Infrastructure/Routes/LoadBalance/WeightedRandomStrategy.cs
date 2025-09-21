using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Routes.LoadBalance
{
    public class WeightedRandomStrategy : LoadBalanceStrategyBase
    {
        public WeightedRandomStrategy(ILogger<WeightedRandomStrategy> logger) : base(logger)
        {
        }

        public override LoadBalanceType Type => LoadBalanceType.WeightedRandom;

        protected override Destination SelectDestinationCore(List<Destination> destinations, LoadBalanceContext context)
        {
            var totalWeight = CalculateTotalWeight(destinations);
            var randomWeight = GetRandomInt(totalWeight);
            
            var currentWeight = 0;
            foreach (var destination in destinations)
            {
                currentWeight += destination.Weight;
                if (randomWeight < currentWeight)
                {
                    Logger.LogDebug("WeightedRandom selected destination: {DestinationCode} (weight: {Weight})", 
                        destination.Code, destination.Weight);
                    return destination;
                }
            }
            
            return destinations.First();
        }
    }
}
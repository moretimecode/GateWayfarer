using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Routes.LoadBalance
{
    public class WeightedRoundRobinStrategy : LoadBalanceStrategyBase
    {
        public WeightedRoundRobinStrategy(ILogger<WeightedRoundRobinStrategy> logger) : base(logger)
        {
        }

        public override LoadBalanceType Type => LoadBalanceType.WeightedRoundRobin;

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
                    Logger.LogDebug("WeightedRoundRobin selected destination: {DestinationCode} (weight: {Weight})", 
                        destination.Code, destination.Weight);
                    return destination;
                }
            }
            
            return destinations.First();
        }
    }
}
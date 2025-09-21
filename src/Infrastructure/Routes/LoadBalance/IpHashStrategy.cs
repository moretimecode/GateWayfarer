using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Routes.LoadBalance
{
    public class IpHashStrategy : LoadBalanceStrategyBase
    {
        public IpHashStrategy(ILogger<IpHashStrategy> logger) : base(logger)
        {
        }

        public override LoadBalanceType Type => LoadBalanceType.IpHash;

        protected override Destination SelectDestinationCore(List<Destination> destinations, LoadBalanceContext context)
        {
            var hashInput = !string.IsNullOrEmpty(context.ClientIp) ? context.ClientIp : context.RequestPath;
            var hash = ComputeHash(hashInput);
            var index = Math.Abs(hash) % destinations.Count;
            var selected = destinations[index];
            
            Logger.LogDebug("IpHash selected destination: {DestinationCode} for hash input: {HashInput}", 
                selected.Code, hashInput);
            return selected;
        }

        private static int ComputeHash(string input)
        {
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToInt32(hash, 0);
        }
    }
}
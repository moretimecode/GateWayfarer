using Domain.Services;
using Domain.Services.HealthChecking.Services;
using Infrastructure.Services.HealthPolicyCalculators;

namespace Infrastructure.Services
{
    /// <summary>
    /// 健康策略计算器工厂实现
    /// </summary>
    public class HealthPolicyCalculatorFactory : IHealthPolicyCalculatorFactory
    {
        private readonly Dictionary<HealthCheckPolicy, IHealthPolicyCalculator> _calculators;

        public HealthPolicyCalculatorFactory()
        {
            _calculators = new Dictionary<HealthCheckPolicy, IHealthPolicyCalculator>
            {
                { HealthCheckPolicy.StopOnFirstFailure, new StopOnFirstFailureCalculator() },
                { HealthCheckPolicy.ConsecutiveFailures, new ConsecutiveFailuresCalculator() },
                { HealthCheckPolicy.ContinueOnFailure, new ContinueOnFailureCalculator() }
            };
        }

        public IHealthPolicyCalculator GetCalculator(HealthCheckPolicy policy)
        {
            if (_calculators.TryGetValue(policy, out var calculator))
            {
                return calculator;
            }

            throw new NotSupportedException($"Health check policy '{policy}' is not supported");
        }

        public void RegisterCalculator(IHealthPolicyCalculator calculator)
        {
            _calculators[calculator.SupportedPolicy] = calculator;
        }
    }
}
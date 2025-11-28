using Domain.Services;
using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;

namespace Infrastructure.Services.HealthPolicyCalculators
{
    /// <summary>
    /// 首次失败策略计算器
    /// </summary>
    public class StopOnFirstFailureCalculator : IHealthPolicyCalculator
    {
        public HealthCheckPolicy SupportedPolicy => HealthCheckPolicy.StopOnFirstFailure;

        public HealthStatus Calculate(ReadOnlySpan<CompactHealthMetrics> metrics, HealthCheckConfig config)
        {
            if (metrics.Length == 0)
            {
                return HealthStatus.CreateUnknown("No metrics data");
            }

            var latest = metrics[0];

            if (latest.IsSuccessful)
            {
                return HealthStatus.CreateHealthy(1.0, latest.ResponseTimeMs);
            }
            else
            {
                return HealthStatus.CreateUnhealthy(1, "First failure detected");
            }
        }
    }
}
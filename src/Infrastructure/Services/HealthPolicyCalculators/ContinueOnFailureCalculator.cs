using Domain.Services;
using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;

namespace Infrastructure.Services.HealthPolicyCalculators
{
    /// <summary>
    /// 继续尝试策略计算器
    /// </summary>
    public class ContinueOnFailureCalculator : IHealthPolicyCalculator
    {
        public HealthCheckPolicy SupportedPolicy => HealthCheckPolicy.ContinueOnFailure;

        public HealthStatus Calculate(ReadOnlySpan<CompactHealthMetrics> metrics, HealthCheckConfig config)
        {
            if (metrics.Length == 0)
            {
                return HealthStatus.CreateUnknown("No metrics data");
            }

            var stats = CalculateStats(metrics);

            return new HealthStatus
            {
                StatusType = HealthStatusType.Healthy, 
                SuccessRate = stats.successRate,
                AverageResponseTimeMs = stats.avgResponseTime,
                HealthScore = Math.Max(0.1, stats.successRate),
                Description = $"继续尝试 - 成功率: {stats.successRate:P1}, 响应时间: {stats.avgResponseTime:F0}ms",
                LastCheckTime = DateTimeOffset.UtcNow
            };
        }

        private static (double successRate, double avgResponseTime) CalculateStats(ReadOnlySpan<CompactHealthMetrics> metrics)
        {
            int successCount = 0;
            long totalResponseTime = 0;

            foreach (var metric in metrics)
            {
                if (metric.IsSuccessful)
                {
                    successCount++;
                }
                totalResponseTime += metric.ResponseTimeMs;
            }

            var successRate = (double)successCount / metrics.Length;
            var avgResponseTime = (double)totalResponseTime / metrics.Length;

            return (successRate, avgResponseTime);
        }
    }
}
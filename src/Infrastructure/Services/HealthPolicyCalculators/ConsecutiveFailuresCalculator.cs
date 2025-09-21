using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Services;
using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;

namespace Infrastructure.Services.HealthPolicyCalculators
{
    /// <summary>
    /// 连续失败策略计算器
    /// </summary>
    public class ConsecutiveFailuresCalculator : IHealthPolicyCalculator
    {
        public HealthCheckPolicy SupportedPolicy => HealthCheckPolicy.ConsecutiveFailures;

        public HealthStatus Calculate(ReadOnlySpan<CompactHealthMetrics> metrics, HealthCheckConfig config)
        {
            if (metrics.Length == 0)
            {
                return HealthStatus.CreateUnknown("No metrics data");
            }

            var (consecutiveFailures, consecutiveSuccesses) = CalculateConsecutiveCounts(metrics);
            
            var stats = CalculateStats(metrics);

            var statusType = DetermineStatusType(consecutiveFailures, consecutiveSuccesses, config);
            
            return CreateHealthStatus(statusType, stats, consecutiveFailures, consecutiveSuccesses);
        }

        private static (int failures, int successes) CalculateConsecutiveCounts(ReadOnlySpan<CompactHealthMetrics> metrics)
        {
            int consecutiveFailures = 0;
            int consecutiveSuccesses = 0;
            
            foreach (var metric in metrics)
            {
                if (metric.IsSuccessful)
                {
                    if (consecutiveFailures == 0)
                        consecutiveSuccesses++;
                    else
                        break;
                }
                else
                {
                    if (consecutiveSuccesses == 0)
                        consecutiveFailures++;
                    else
                        break;
                }
            }
            
            return (consecutiveFailures, consecutiveSuccesses);
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

        private static HealthStatusType DetermineStatusType(int consecutiveFailures, int consecutiveSuccesses, HealthCheckConfig config)
        {
            if (consecutiveFailures >= config.FailureThreshold)
            {
                return HealthStatusType.Unhealthy;
            }
            
            if (consecutiveSuccesses >= config.SuccessThreshold && consecutiveFailures > 0)
            {
                return HealthStatusType.Recovering;
            }
            
            return HealthStatusType.Healthy;
        }

        private static HealthStatus CreateHealthStatus(
            HealthStatusType statusType, 
            (double successRate, double avgResponseTime) stats,
            int consecutiveFailures,
            int consecutiveSuccesses)
        {
            var description = statusType switch
            {
                HealthStatusType.Healthy => $"健康 - 成功率: {stats.successRate:P1}, 响应时间: {stats.avgResponseTime:F0}ms",
                HealthStatusType.Unhealthy => $"不健康 - 连续失败: {consecutiveFailures}次",
                HealthStatusType.Recovering => $"恢复中 - 连续成功: {consecutiveSuccesses}次",
                _ => "状态未知"
            };

            return new HealthStatus
            {
                StatusType = statusType,
                SuccessRate = stats.successRate,
                AverageResponseTimeMs = stats.avgResponseTime,
                ConsecutiveFailures = consecutiveFailures,
                ConsecutiveSuccesses = consecutiveSuccesses,
                HealthScore = CalculateHealthScore(stats.successRate, stats.avgResponseTime),
                Description = description,
                LastCheckTime = DateTimeOffset.UtcNow
            };
        }

        private static double CalculateHealthScore(double successRate, double avgResponseTime)
        {
            var score = successRate * 0.8; 
            var responseTimeScore = Math.Max(0, 1.0 - (avgResponseTime / 5000.0));
            score += responseTimeScore * 0.2;
            return Math.Max(0.0, Math.Min(1.0, score));
        }
    }
}
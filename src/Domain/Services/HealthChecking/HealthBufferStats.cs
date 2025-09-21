using System.Runtime.InteropServices;

namespace Domain.Services.HealthChecking
{
    /// <summary>
    /// 健康缓冲区统计信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HealthBufferStats : IEquatable<HealthBufferStats>
    {
        /// <summary>
        /// 总检查次数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 成功次数
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// 失败次数
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// 成功率 (0.0 - 1.0)
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// 平均响应时间（毫秒）
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// 最大响应时间（毫秒）
        /// </summary>
        public double MaxResponseTimeMs { get; set; }

        /// <summary>
        /// 最小响应时间（毫秒）
        /// </summary>
        public double MinResponseTimeMs { get; set; }

        /// <summary>
        /// 连续失败次数
        /// </summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// 连续成功次数
        /// </summary>
        public int ConsecutiveSuccesses { get; set; }

        /// <summary>
        /// 最新检查时间戳（Unix毫秒，避免nullable的开销）
        /// </summary>
        public long LatestCheckTimestampMs { get; set; }

        /// <summary>
        /// 最新检查是否成功
        /// </summary>
        public bool LatestCheckSuccess { get; set; }

        // 总内存占用：4+4+4+8+8+8+8+4+4+8+1 = 61字节（紧密排列）

        #region 属性访问器

        /// <summary>
        /// 获取最新检查时间
        /// </summary>
        public readonly DateTimeOffset? LatestCheckTime
        {
            get
            {
                return LatestCheckTimestampMs > 0 
                    ? DateTimeOffset.FromUnixTimeMilliseconds(LatestCheckTimestampMs)
                    : null;
            }
        }

        /// <summary>
        /// 是否有数据
        /// </summary>
        public readonly bool HasData => TotalCount > 0;

        /// <summary>
        /// 失败率
        /// </summary>
        public readonly double FailureRate => 1.0 - SuccessRate;

        #endregion

        #region 工厂方法

        /// <summary>
        /// 创建空的统计信息
        /// </summary>
        public static HealthBufferStats Empty => new HealthBufferStats
        {
            TotalCount = 0,
            SuccessCount = 0,
            FailureCount = 0,
            SuccessRate = 0.0,
            AverageResponseTimeMs = 0.0,
            MaxResponseTimeMs = 0.0,
            MinResponseTimeMs = 0.0,
            ConsecutiveFailures = 0,
            ConsecutiveSuccesses = 0,
            LatestCheckTimestampMs = 0,
            LatestCheckSuccess = false
        };

        /// <summary>
        /// 从指标数据创建统计信息
        /// </summary>
        public static HealthBufferStats CreateFromMetrics(ReadOnlySpan<CompactHealthMetrics> metrics)
        {
            if (metrics.Length == 0)
                return Empty;

            int successCount = 0;
            int failureCount = 0;
            long totalResponseTime = 0;
            int consecutiveFailures = 0;
            int consecutiveSuccesses = 0;
            double maxResponseTime = 0;
            double minResponseTime = double.MaxValue;

            var latest = metrics[0];
            bool countingFailures = true;
            bool countingSuccesses = true;

            for (int i = 0; i < metrics.Length; i++)
            {
                var metric = metrics[i];
                if (metric.IsSuccessful)
                {
                    successCount++;
                    if (countingSuccesses && !countingFailures)
                    {
                        consecutiveSuccesses++;
                    }
                    else
                    {
                        countingFailures = false;
                    }
                }
                else
                {
                    failureCount++;
                    if (countingFailures && !countingSuccesses)
                    {
                        consecutiveFailures++;
                    }
                    else
                    {
                        countingSuccesses = false;
                    }
                }

                // 统计响应时间
                totalResponseTime += metric.ResponseTimeMs;
                maxResponseTime = Math.Max(maxResponseTime, metric.ResponseTimeMs);
                minResponseTime = Math.Min(minResponseTime, metric.ResponseTimeMs);
            }

            return new HealthBufferStats
            {
                TotalCount = metrics.Length,
                SuccessCount = successCount,
                FailureCount = failureCount,
                SuccessRate = metrics.Length > 0 ? (double)successCount / metrics.Length : 0,
                AverageResponseTimeMs = metrics.Length > 0 ? (double)totalResponseTime / metrics.Length : 0,
                MaxResponseTimeMs = maxResponseTime,
                MinResponseTimeMs = minResponseTime == double.MaxValue ? 0 : minResponseTime,
                ConsecutiveFailures = consecutiveFailures,
                ConsecutiveSuccesses = consecutiveSuccesses,
                LatestCheckTimestampMs = latest.TimestampMs,
                LatestCheckSuccess = latest.IsSuccessful
            };
        }

        #endregion

        #region 业务方法

        /// <summary>
        /// 获取健康状态描述
        /// </summary>
        public readonly string GetHealthDescription()
        {
            if (TotalCount == 0)
                return "无检查数据";

            return $"成功率: {SuccessRate:P1}, 平均响应: {AverageResponseTimeMs:F1}ms, 总计: {TotalCount}次";
        }

        /// <summary>
        /// 判断是否有足够的数据
        /// </summary>
        public readonly bool HasSufficientData(int minimumChecks = 1)
        {
            return TotalCount >= minimumChecks;
        }

        /// <summary>
        /// 计算健康分数 (0-100)
        /// </summary>
        public readonly int CalculateHealthScore()
        {
            if (TotalCount == 0) return 0;

            var score = SuccessRate * 100; // 基础分数基于成功率

            // 根据响应时间调整分数
            if (AverageResponseTimeMs <= 100) score += 0;      // 100ms以内不扣分
            else if (AverageResponseTimeMs <= 500) score -= 5; // 100-500ms扣5分
            else if (AverageResponseTimeMs <= 1000) score -= 15; // 500ms-1s扣15分
            else score -= 25; // 超过1s扣25分

            // 根据连续失败情况调整分数
            if (ConsecutiveFailures > 0)
            {
                score -= ConsecutiveFailures * 10; // 每次连续失败扣10分
            }

            return Math.Max(0, Math.Min(100, (int)score));
        }

        #endregion

        #region 标准接口实现

        public readonly bool Equals(HealthBufferStats other)
        {
            return TotalCount == other.TotalCount &&
                   SuccessCount == other.SuccessCount &&
                   FailureCount == other.FailureCount &&
                   Math.Abs(SuccessRate - other.SuccessRate) < 0.0001 &&
                   Math.Abs(AverageResponseTimeMs - other.AverageResponseTimeMs) < 0.001 &&
                   LatestCheckTimestampMs == other.LatestCheckTimestampMs &&
                   LatestCheckSuccess == other.LatestCheckSuccess;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is HealthBufferStats other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(
                TotalCount, 
                SuccessCount, 
                SuccessRate, 
                AverageResponseTimeMs, 
                LatestCheckTimestampMs,
                LatestCheckSuccess);
        }

        public static bool operator ==(HealthBufferStats left, HealthBufferStats right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HealthBufferStats left, HealthBufferStats right)
        {
            return !left.Equals(right);
        }

        public override readonly string ToString()
        {
            return GetHealthDescription();
        }

        #endregion
    }
}
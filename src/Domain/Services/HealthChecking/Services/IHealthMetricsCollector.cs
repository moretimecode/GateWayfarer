using System.Runtime.InteropServices;

namespace Domain.Services.HealthChecking.Services
{
    /// <summary>
    /// 健康指标收集器接口
    /// </summary>
    public interface IHealthMetricsCollector
    {
        /// <summary>
        /// 启动健康指标收集
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 停止健康指标收集
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 手动收集指定destination的指标（用于测试）
        /// </summary>
        Task<CompactHealthMetrics> CollectMetricsAsync(string destinationCode, Destination destination);

        /// <summary>
        /// 获取收集器统计信息
        /// </summary>
        CollectorStats GetStats();

        /// <summary>
        /// 更新destination列表（服务发现变更时调用）
        /// </summary>
        Task UpdateDestinationsAsync(IEnumerable<string> destinationCodes);
    }

    /// <summary>
    /// 收集器统计信息
    /// </summary>
    public class CollectorStats
    {
        public int TotalDestinations { get; set; }
        public int ActiveCollections { get; set; }
        public int SuccessfulCollections { get; set; }
        public int FailedCollections { get; set; }
        public int CurrentConcurrency { get; set; }
        public double AverageCollectionTimeMs { get; set; }
        public DateTimeOffset LastCollectionTime { get; set; }

        public double SuccessRate => TotalCollections > 0 ? (double)SuccessfulCollections / TotalCollections : 0;
        public int TotalCollections => SuccessfulCollections + FailedCollections;

        public CollectorStats()
        {
            this.LastCollectionTime = DateTimeOffset.UtcNow;
        }

        public CollectorStats(
            int totalDestinations,
            int activeCollections,
            int successfulCollections,
            int failedCollections,
            int currentConcurrency,
            double averageCollectionTimeMs,
            DateTimeOffset lastCollectionTime)
        {
            TotalDestinations = totalDestinations;
            ActiveCollections = activeCollections;
            SuccessfulCollections = successfulCollections;
            FailedCollections = failedCollections;
            CurrentConcurrency = currentConcurrency;
            AverageCollectionTimeMs = averageCollectionTimeMs;
        }


        /// <summary>
        /// 获取描述信息
        /// </summary>
        public string GetDescription()
        {
            return $"总数: {TotalDestinations}, 成功: {SuccessfulCollections}, 失败: {FailedCollections}, " +
                   $"成功率: {SuccessRate:P1}, 平均耗时: {AverageCollectionTimeMs:F1}ms, 当前并发: {CurrentConcurrency}";
        }

        /// <summary>
        /// 获取详细统计信息
        /// </summary>
        public string GetDetailedDescription()
        {
            return $"""
                总目标数: {TotalDestinations}
                当前活跃收集: {ActiveCollections}
                成功收集: {SuccessfulCollections}
                失败收集: {FailedCollections}
                总收集次数: {TotalCollections}
                成功率: {SuccessRate:P2}
                平均耗时: {AverageCollectionTimeMs:F2}ms
                当前并发数: {CurrentConcurrency}
                最后收集时间: {LastCollectionTime:yyyy-MM-dd HH:mm:ss}
                """;
        }

        /// <summary>
        /// 判断统计信息是否有效
        /// </summary>
        public bool IsValid => TotalDestinations >= 0 && CurrentConcurrency >= 0;
    }
}
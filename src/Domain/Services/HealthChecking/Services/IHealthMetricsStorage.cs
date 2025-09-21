using Domain.Services.HealthChecking;

namespace Domain.Services.HealthChecking.Services
{
    /// <summary>
    /// 健康指标存储接口
    /// </summary>
    public interface IHealthMetricsStorage
    {
        /// <summary>
        /// 存储健康检查指标
        /// </summary>
        void StoreMetrics(string destinationCode, CompactHealthMetrics metrics);

        /// <summary>
        /// 获取指定目标的最近N条记录
        /// </summary>
       ReadOnlySpan<CompactHealthMetrics>GetRecentMetrics(string destinationCode, int count);

        /// <summary>
        /// 检查是否有足够的数据进行健康计算
        /// </summary>
        bool HasSufficientData(string destinationCode, int requiredDataPoints);

        /// <summary>
        /// 获取指定目标的统计信息
        /// </summary>
        HealthBufferStats GetStats(string destinationCode);
    }
}
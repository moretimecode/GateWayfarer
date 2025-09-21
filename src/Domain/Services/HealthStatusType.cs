namespace Domain.Services.HealthChecking
{
    /// <summary>
    /// 健康状态枚举
    /// </summary>
    public enum HealthStatusType
    {
        /// <summary>
        /// 未知状态（没有足够的数据）
        /// </summary>
        Unknown = 0,
        
        /// <summary>
        /// 健康状态
        /// </summary>
        Healthy = 1,
        
        /// <summary>
        /// 不健康状态
        /// </summary>
        Unhealthy = 2,
        
        /// <summary>
        /// 降级状态（部分功能可用）
        /// </summary>
        Degraded = 3,
        
        /// <summary>
        /// 正在恢复中
        /// </summary>
        Recovering = 4
    }

    /// <summary>
    /// 健康状态详情（统一的健康状态类）
    /// </summary>
    public class HealthStatus
    {
        /// <summary>
        /// 健康状态类型
        /// </summary>
        public HealthStatusType StatusType { get; set; }
        
        /// <summary>
        /// 是否健康（兼容原有设计）
        /// </summary>
        public bool IsHealthy => StatusType == HealthStatusType.Healthy || StatusType == HealthStatusType.Recovering;
        
        /// <summary>
        /// 最后检查时间
        /// </summary>
        public DateTimeOffset LastCheckTime { get; set; }
        
        /// <summary>
        /// 连续失败次数
        /// </summary>
        public int ConsecutiveFailures { get; set; }
        
        /// <summary>
        /// 连续成功次数
        /// </summary>
        public int ConsecutiveSuccesses { get; set; }
        
        /// <summary>
        /// 最后错误信息
        /// </summary>
        public string? LastError { get; set; }
        
        /// <summary>
        /// 健康分数 (0.0 - 1.0)
        /// </summary>
        public double HealthScore { get; set; }
        
        /// <summary>
        /// 成功率
        /// </summary>
        public double SuccessRate { get; set; }
        
        /// <summary>
        /// 平均响应时间(毫秒)
        /// </summary>
        public double AverageResponseTimeMs { get; set; }
        
        /// <summary>
        /// 状态描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// 分析的数据点数量
        /// </summary>
        public int DataPointsAnalyzed { get; set; }
        
        /// <summary>
        /// 是否有足够的数据进行判断
        /// </summary>
        public bool HasSufficientData => DataPointsAnalyzed >= MinRequiredDataPoints;
        
        /// <summary>
        /// 最少需要的数据点数量
        /// </summary>
        public const int MinRequiredDataPoints = 3;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public HealthStatus()
        {
            LastCheckTime = DateTimeOffset.UtcNow;
            StatusType = HealthStatusType.Unknown;
        }

        /// <summary>
        /// 创建未知状态
        /// </summary>
        public static HealthStatus CreateUnknown(string? reason = null)
        {
            return new HealthStatus
            {
                StatusType = HealthStatusType.Unknown,
                Description = reason ?? "状态未知",
                LastCheckTime = DateTimeOffset.UtcNow
            };
        }

        /// <summary>
        /// 创建健康状态
        /// </summary>
        public static HealthStatus CreateHealthy(double successRate = 1.0, double avgResponseTime = 0.0)
        {
            return new HealthStatus
            {
                StatusType = HealthStatusType.Healthy,
                SuccessRate = successRate,
                AverageResponseTimeMs = avgResponseTime,
                HealthScore = Math.Min(1.0, successRate * 0.8 + (1.0 - Math.Min(avgResponseTime / 1000.0, 1.0)) * 0.2),
                Description = $"健康 - 成功率: {successRate:P1}, 响应时间: {avgResponseTime:F0}ms",
                LastCheckTime = DateTimeOffset.UtcNow
            };
        }

        /// <summary>
        /// 创建不健康状态
        /// </summary>
        public static HealthStatus CreateUnhealthy(int consecutiveFailures, string? error = null)
        {
            return new HealthStatus
            {
                StatusType = HealthStatusType.Unhealthy,
                ConsecutiveFailures = consecutiveFailures,
                LastError = error,
                HealthScore = 0.0,
                Description = $"不健康 - 连续失败: {consecutiveFailures}",
                LastCheckTime = DateTimeOffset.UtcNow
            };
        }
    }
}
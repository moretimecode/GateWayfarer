namespace Domain.Services
{
    /// <summary>
    /// 健康检查策略
    /// </summary>
    public enum HealthCheckPolicy
    {
        /// <summary>
        /// 首次失败就标记为不健康
        /// </summary>
        StopOnFirstFailure = 0,

        /// <summary>
        /// 连续失败达到阈值才标记为不健康
        /// </summary>
        ConsecutiveFailures = 1,

        /// <summary>
        /// 继续尝试，不标记为不健康（只记录但不影响路由）
        /// </summary>
        ContinueOnFailure = 2
    }
}
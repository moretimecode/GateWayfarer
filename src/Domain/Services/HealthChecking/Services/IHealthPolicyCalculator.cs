namespace Domain.Services.HealthChecking.Services
{
       public interface IHealthPolicyCalculator
    {
        /// <summary>
        /// 支持的策略类型
        /// </summary>
        HealthCheckPolicy SupportedPolicy { get; }

        /// <summary>
        /// 计算健康状态
        /// </summary>
        /// <param name="metrics">历史指标数据</param>
        /// <param name="config">健康检查配置</param>
        /// <returns>健康状态</returns>
        HealthStatus Calculate(ReadOnlySpan<CompactHealthMetrics> metrics, HealthCheckConfig config);
    }
}
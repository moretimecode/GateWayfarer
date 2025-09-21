using Domain.Services.HealthChecking;

namespace Domain.Services.Services
{
    public interface IHealthCalculationService
    {
        /// <summary>
        /// 判断目标实例是否健康
        /// </summary>
        /// <param name="destination">目标实例</param>
        /// <param name="healthCheckConfig">健康检查配置</param>
        /// <returns>是否健康</returns>
       bool IsHealthy(Destination destination, HealthCheckConfig healthCheckConfig);

        /// <summary>
        /// 获取目标实例的详细健康状态
        /// </summary>
        /// <param name="destination">目标实例</param>
        /// <param name="healthCheckConfig">健康检查配置</param>
        /// <returns>健康状态详情</returns>
        HealthStatus GetHealthStatus(Destination destination, HealthCheckConfig healthCheckConfig);
    }

}
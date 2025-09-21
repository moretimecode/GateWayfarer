using Domain.Services;
using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;
using Domain.Services.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    /// <summary>
    /// 健康计算服务实现（基于现有接口和配置）
    /// </summary>
    public class HealthCalculationService : IHealthCalculationService
    {
        private readonly IHealthMetricsStorage _storage;
        private readonly IHealthPolicyCalculatorFactory _calculatorFactory;
        private readonly ILogger<HealthCalculationService> _logger;

        public HealthCalculationService(
            IHealthMetricsStorage storage,
            IHealthPolicyCalculatorFactory calculatorFactory,
            ILogger<HealthCalculationService> logger)
        {
            _storage = storage;
            _calculatorFactory = calculatorFactory;
            _logger = logger;
        }

        /// <summary>
        /// 判断目标实例是否健康
        /// </summary>
        public  bool IsHealthy(Destination destination, HealthCheckConfig healthCheckConfig)
        {
            var healthStatus =  GetHealthStatus(destination, healthCheckConfig);
            return healthStatus.IsHealthy;
        }

        /// <summary>
        /// 获取目标实例的详细健康状态
        /// </summary>
        public  HealthStatus GetHealthStatus(Destination destination, HealthCheckConfig healthCheckConfig)
        {
            if (destination is null)
            {
                return HealthStatus.CreateUnknown("Invalid destination");
            }

            if (healthCheckConfig is null)
            {
                return HealthStatus.CreateUnknown("Invalid health check config");
            }

            try
            {
                if (!healthCheckConfig.ActiveEnabled)
                {
                    return HealthStatus.CreateHealthy();
                }

                if (!_storage.HasSufficientData(destination.Code, healthCheckConfig.FailureThreshold))
                {
                    return HealthStatus.CreateUnknown("Insufficient health data");
                }

                var recentMetrics = _storage.GetRecentMetrics(destination.Code, 50);

                if (recentMetrics.Length == 0)
                {
                    return HealthStatus.CreateUnknown("No recent health metrics");
                }

                var calculator = _calculatorFactory.GetCalculator(healthCheckConfig.Policy);
                var healthStatus = calculator.Calculate(recentMetrics, healthCheckConfig);

                return healthStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "计算destination {Code} 健康状态时发生错误", destination.Code);
                return HealthStatus.CreateUnknown($"Calculation error: {ex.Message}");
            }
        }
    }
}
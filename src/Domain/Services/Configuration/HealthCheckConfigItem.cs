namespace Domain.Services.Configuration
{
    public class HealthCheckConfigItem
    {
         /// <summary>
        /// 是否启用主动健康检查
        /// </summary>
        public bool ActiveEnabled { get; set; } = true;
        
        /// <summary>
        /// 主动健康检查间隔 (秒)
        /// </summary>
        public int ActiveIntervalSeconds { get; set; } = 30;
        
        /// <summary>
        /// 主动健康检查超时时间 (秒)
        /// </summary>
        public int ActiveTimeoutSeconds { get; set; } = 5;
        
        /// <summary>
        /// 主动健康检查路径
        /// </summary>
        public string ActivePath { get; set; } = "/health";
        
        /// <summary>
        /// 故障阈值 (连续失败多少次标记为不健康)
        public int FailureThreshold { get; set; } = 3;
        
        /// <summary>
        /// 成功阈值 (连续成功多少次标记为健康)
        /// </summary>
        public int SuccessThreshold { get; set; } = 2;
        
        /// <summary>
        /// 是否启用被动健康检查
        /// </summary>
        public bool PassiveEnabled { get; set; } = false;
        
        /// <summary>
        /// 被动健康检查失败率阈值 (0-1之间的小数)
        /// </summary>
        public double PassiveFailureRateThreshold { get; set; } = 0.1;
        
        /// <summary>
        /// 被动健康检查最小请求数
        /// </summary>
        public int PassiveMinRequestCount { get; set; } = 10;
        
        /// <summary>
        /// 被动健康检查重新激活周期 (秒)
        /// </summary>
        public int PassiveReactivationPeriodSeconds { get; set; } = 120;

        /// <summary>
        /// 健康检查策略 (StopOnFirstFailure/ContinueOnFailure)
        /// </summary>
        public HealthCheckPolicy Policy { get; set; } = HealthCheckPolicy.StopOnFirstFailure; 
    }
}
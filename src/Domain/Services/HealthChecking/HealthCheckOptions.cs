namespace Domain.Services.HealthChecking
{
    /// <summary>
    /// 健康检查配置选项
    /// </summary>
    public class HealthCheckOptions
    {
        public const string SectionName = "HealthCheck";

        /// <summary>
        /// 是否启用健康检查（默认启用）
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 检查间隔（默认5秒）
        /// </summary>
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 单次请求超时时间（默认5秒，最大60秒）
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 默认健康检查路径
        /// </summary>
        public string DefaultPath { get; set; } = "/health";

        /// <summary>
        /// 最大并发检查数（0表示自动计算）
        /// </summary>
        public int MaxConcurrency { get; set; } = 0;

        /// <summary>
        /// 线程池管理策略
        /// </summary>
        public ThreadPoolStrategy ThreadPoolStrategy { get; set; } = ThreadPoolStrategy.Dynamic;

        /// <summary>
        /// 重试配置
        /// </summary>
        public RetryOptions Retry { get; set; } = new();

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        public void Validate()
        {
            if (Interval <= TimeSpan.Zero)
                throw new ArgumentException("Interval must be positive");

            if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(1))
                throw new ArgumentException("Timeout must be between 1ms and 60s");

            if (MaxConcurrency < 0)
                throw new ArgumentException("MaxConcurrency must be non-negative");
        }
    }

    /// <summary>
    /// 线程池管理策略
    /// </summary>
    public enum ThreadPoolStrategy
    {
        /// <summary>
        /// 动态调整（根据成功率调整）
        /// </summary>
        Dynamic,

        /// <summary>
        /// 根据实例数静态计算
        /// </summary>
        Static,

        /// <summary>
        /// 使用系统默认线程池
        /// </summary>
        Default
    }

    /// <summary>
    /// 重试配置
    /// </summary>
    public class RetryOptions
    {
        /// <summary>
        /// 是否启用重试
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxAttempts { get; set; } = 2;

        /// <summary>
        /// 重试延迟
        /// </summary>
        public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(500);
    }
}
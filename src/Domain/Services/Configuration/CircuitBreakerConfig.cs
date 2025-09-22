namespace Domain.Services.Configuration
{
    public class CircuitBreakerConfig : ValueObject
    {

        // 连续失败多少次后触发熔断（默认5次）
        public int FailureThreshold { get; private set; }

        // 熔断后多长时间尝试恢复（默认60秒）
        public TimeSpan OpenTimeout { get; private set; }

        // 最小请求数量，低于此数量不触发熔断（默认10个）
        public int MinimumThroughput { get; private set; }

        // 失败率阈值，超过此比例触发熔断（默认50%）
        public double FailureRateThreshold { get; private set; }

        // 是否启用熔断器
        public bool Enabled { get; private set; }

        private CircuitBreakerConfig(int failureThreshold, TimeSpan openTimeout,
            int minimumThroughput, double failureRateThreshold, bool enabled)
        {
            FailureThreshold = failureThreshold;
            OpenTimeout = openTimeout;
            MinimumThroughput = minimumThroughput;
            FailureRateThreshold = failureRateThreshold;
            Enabled = enabled;
        }

        public static CircuitBreakerConfig Create(int failureThreshold = 5,
            TimeSpan? openTimeout = null, int minimumThroughput = 10,
            double failureRateThreshold = 0.5, bool enabled = true)
        {
            if (failureThreshold <= 0)
                throw new ArgumentException("Failure threshold must be greater than 0", nameof(failureThreshold));

            if (failureRateThreshold < 0 || failureRateThreshold > 1)
                throw new ArgumentException("Failure rate threshold must be between 0 and 1", nameof(failureRateThreshold));

            return new CircuitBreakerConfig(
                failureThreshold,
                openTimeout ?? TimeSpan.FromSeconds(60),
                minimumThroughput,
                failureRateThreshold,
                enabled);
        }

        public static CircuitBreakerConfig Default() => Create();

        public IEnumerable<object> GetEqualityComponents()
        {
            yield return FailureThreshold;
            yield return OpenTimeout;
            yield return MinimumThroughput;
            yield return FailureRateThreshold;
            yield return Enabled;
        }
    }
}
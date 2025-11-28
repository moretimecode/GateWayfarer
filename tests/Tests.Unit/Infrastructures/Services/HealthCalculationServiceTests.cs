using Domain.Services;
using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;
using Domain.Services.Services;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Unit.Infrastructures.Services
{
    /// <summary>
    /// HealthCalculationService 单元测试 - 使用 Fake 对象
    /// </summary>
    public class HealthCalculationServiceTests
    {
        private readonly FakeHealthMetricsStorage _fakeStorage;
        private readonly Mock<IHealthPolicyCalculatorFactory> _mockCalculatorFactory;
        private readonly Mock<ILogger<HealthCalculationService>> _mockLogger;
        private readonly HealthCalculationService _service;

        public HealthCalculationServiceTests()
        {
            _fakeStorage = new FakeHealthMetricsStorage();
            _mockCalculatorFactory = new Mock<IHealthPolicyCalculatorFactory>();
            _mockLogger = new Mock<ILogger<HealthCalculationService>>();

            _service = new HealthCalculationService(
                _fakeStorage,
                _mockCalculatorFactory.Object,
                _mockLogger.Object);
        }

        #region IsHealthy Tests

        [Fact]
        public void IsHealthy_WhenDestinationIsNull_ReturnsFalse()
        {
            // Arrange
            var healthCheckConfig = CreateHealthCheckConfig();

            // Act
            var result = _service.IsHealthy(null, healthCheckConfig);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsHealthy_WhenHealthCheckConfigIsNull_ReturnsFalse()
        {
            // Arrange
            var destination = CreateDestination();

            // Act
            var result = _service.IsHealthy(destination, null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsHealthy_WhenActiveCheckDisabled_ReturnsTrue()
        {
            // Arrange
            var destination = CreateDestination();
            var healthCheckConfig = CreateHealthCheckConfig(activeEnabled: false);

            // Act
            var result = _service.IsHealthy(destination, healthCheckConfig);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsHealthy_WhenMetricsIndicateHealthy_ReturnsTrue()
        {
            // Arrange
            var destination = CreateDestination();
            var healthCheckConfig = CreateHealthCheckConfig();
            var metrics = CreateCompactHealthMetrics(5, isHealthy: true);

            _fakeStorage.SetupMetrics(destination.Code, metrics, sufficientData: true);
            SetupFakeCalculatorForHealthy(healthCheckConfig);

            // Act
            var result = _service.IsHealthy(destination, healthCheckConfig);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsHealthy_WhenMetricsIndicateUnhealthy_ReturnsFalse()
        {
            // Arrange
            var destination = CreateDestination();
            var healthCheckConfig = CreateHealthCheckConfig();
            var metrics = CreateCompactHealthMetrics(5, isHealthy: false);

            _fakeStorage.SetupMetrics(destination.Code, metrics, sufficientData: true);
            SetupFakeCalculatorForUnhealthy(healthCheckConfig);

            // Act
            var result = _service.IsHealthy(destination, healthCheckConfig);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region GetHealthStatus Tests

        [Fact]
        public void GetHealthStatus_WhenDestinationIsNull_ReturnsUnknownStatus()
        {
            // Arrange
            var healthCheckConfig = CreateHealthCheckConfig();

            // Act
            var result = _service.GetHealthStatus(null, healthCheckConfig);

            // Assert
            Assert.Equal(HealthStatusType.Unknown, result.StatusType);
            Assert.Contains("Invalid destination", result.Description);
        }

        [Fact]
        public void GetHealthStatus_WhenHealthCheckConfigIsNull_ReturnsUnknownStatus()
        {
            // Arrange
            var destination = CreateDestination();

            // Act
            var result = _service.GetHealthStatus(destination, null);

            // Assert
            Assert.Equal(HealthStatusType.Unknown, result.StatusType);
            Assert.Contains("Invalid health check config", result.Description);
        }

        [Fact]
        public void GetHealthStatus_WhenActiveCheckDisabled_ReturnsHealthyStatus()
        {
            // Arrange
            var destination = CreateDestination();
            var healthCheckConfig = CreateHealthCheckConfig(activeEnabled: false);

            // Act
            var result = _service.GetHealthStatus(destination, healthCheckConfig);

            // Assert
            Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        }

        [Fact]
        public void GetHealthStatus_WhenInsufficientData_ReturnsUnknownStatus()
        {
            // Arrange
            var destination = CreateDestination();
            var healthCheckConfig = CreateHealthCheckConfig();

            _fakeStorage.SetupMetrics(destination.Code, Array.Empty<CompactHealthMetrics>(), sufficientData: false);

            // Act
            var result = _service.GetHealthStatus(destination, healthCheckConfig);

            // Assert
            Assert.Equal(HealthStatusType.Unknown, result.StatusType);
            Assert.Contains("Insufficient health data", result.Description);
        }

        [Fact]
        public void GetHealthStatus_WhenNoRecentMetrics_ReturnsUnknownStatus()
        {
            // Arrange
            var destination = CreateDestination();
            var healthCheckConfig = CreateHealthCheckConfig();

            _fakeStorage.SetupMetrics(destination.Code, Array.Empty<CompactHealthMetrics>(), sufficientData: true);

            // Act
            var result = _service.GetHealthStatus(destination, healthCheckConfig);

            // Assert
            Assert.Equal(HealthStatusType.Unknown, result.StatusType);
            Assert.Contains("No recent health metrics", result.Description);
        }

        [Fact]
        public void GetHealthStatus_WithHealthyMetrics_ReturnsHealthyStatus()
        {
            // Arrange
            var destination = CreateDestination();
            var healthCheckConfig = CreateHealthCheckConfig();
            var metrics = CreateCompactHealthMetrics(10, isHealthy: true);

            _fakeStorage.SetupMetrics(destination.Code, metrics, sufficientData: true);
            SetupFakeCalculatorForHealthy(healthCheckConfig);

            // Act
            var result = _service.GetHealthStatus(destination, healthCheckConfig);

            // Assert
            Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        }

        [Fact]
        public void GetHealthStatus_WithUnhealthyMetrics_ReturnsUnhealthyStatus()
        {
            // Arrange
            var destination = CreateDestination();
            var healthCheckConfig = CreateHealthCheckConfig();
            var metrics = CreateCompactHealthMetrics(10, isHealthy: false);

            _fakeStorage.SetupMetrics(destination.Code, metrics, sufficientData: true);
            SetupFakeCalculatorForUnhealthy(healthCheckConfig);

            // Act
            var result = _service.GetHealthStatus(destination, healthCheckConfig);

            // Assert
            Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        }

        [Fact]
        public void GetHealthStatus_WhenCalculatorThrowsException_ReturnsUnknownStatus()
        {
            // Arrange
            var destination = CreateDestination();
            var healthCheckConfig = CreateHealthCheckConfig();
            var metrics = CreateCompactHealthMetrics(5, isHealthy: true);

            _fakeStorage.SetupMetrics(destination.Code, metrics, sufficientData: true);

            // ✅ 使用 Fake Calculator 抛出异常，而不是 Mock
            SetupFakeCalculatorToThrowException(healthCheckConfig);

            // Act
            var result = _service.GetHealthStatus(destination, healthCheckConfig);

            // Assert
            Assert.Equal(HealthStatusType.Unknown, result.StatusType);
            Assert.Contains("Calculation error", result.Description);
            VerifyLoggerErrorCalled();
        }

        [Theory]
        [InlineData(10, true)]
        [InlineData(50, true)]
        [InlineData(100, true)]
        public void GetHealthStatus_WithVariousMetricCounts_SuccessfullyCalculates(int metricCount, bool expectedHealthy)
        {
            // Arrange
            var destination = CreateDestination();
            var healthCheckConfig = CreateHealthCheckConfig();
            var metrics = CreateCompactHealthMetrics(metricCount, isHealthy: expectedHealthy);

            _fakeStorage.SetupMetrics(destination.Code, metrics, sufficientData: true);

            if (expectedHealthy)
                SetupFakeCalculatorForHealthy(healthCheckConfig);
            else
                SetupFakeCalculatorForUnhealthy(healthCheckConfig);

            // Act
            var result = _service.GetHealthStatus(destination, healthCheckConfig);

            // Assert
            Assert.NotEqual(HealthStatusType.Unknown, result.StatusType);
        }

        #endregion

        #region Helper Methods

        private Destination CreateDestination(string code = "service-1")
        {
            return new Destination(code, Protocol.Http, "localhost", 8080);
        }

        private HealthCheckConfig CreateHealthCheckConfig(
            bool activeEnabled = true,
            int failureThreshold = 3,
            HealthCheckPolicy healthCheckPolicy = HealthCheckPolicy.ConsecutiveFailures)
        {
            return new HealthCheckConfig(activeEnabled, 30, 5, "/health", failureThreshold, policy: healthCheckPolicy);
        }

        private CompactHealthMetrics[] CreateCompactHealthMetrics(int count, bool isHealthy)
        {
            var metrics = new List<CompactHealthMetrics>();
            var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            for (int i = 0; i < count; i++)
            {
                var metric = isHealthy
                    ? CompactHealthMetrics.CreateSuccess(
                        timestampMs: baseTimestamp - (i * 1000),
                        responseTimeMs: (ushort)Random.Shared.Next(10, 200),
                        statusCode: 200)
                    : CompactHealthMetrics.CreateFailure(
                        timestampMs: baseTimestamp - (i * 1000),
                        responseTimeMs: (ushort)Random.Shared.Next(100, 500),
                        statusCode: 500,
                        additionalFlags: HealthCheckFlags.IsServerError);

                metrics.Add(metric);
            }
            return metrics.ToArray();
        }

        /// <summary>
        /// ✅ 使用 Fake Calculator 实现，支持 ReadOnlySpan 参数
        /// </summary>
        private void SetupFakeCalculatorForHealthy(HealthCheckConfig healthCheckConfig)
        {
            var fakeCalculator = new FakeHealthPolicyCalculator(
                isHealthy: true,
                successRate: 0.95,
                avgResponseTime: 50);

            _mockCalculatorFactory
                .Setup(x => x.GetCalculator(healthCheckConfig.Policy))
                .Returns(fakeCalculator);
        }

        /// <summary>
        /// ✅ 使用 Fake Calculator 实现，支持 ReadOnlySpan 参数
        /// </summary>
        private void SetupFakeCalculatorForUnhealthy(HealthCheckConfig healthCheckConfig)
        {
            var fakeCalculator = new FakeHealthPolicyCalculator(
                isHealthy: false,
                consecutiveFailures: 5,
                error: "High failure rate");

            _mockCalculatorFactory
                .Setup(x => x.GetCalculator(healthCheckConfig.Policy))
                .Returns(fakeCalculator);
        }

        /// <summary>
        /// ✅ 使用 Fake Calculator 实现，支持抛出异常
        /// </summary>
        private void SetupFakeCalculatorToThrowException(HealthCheckConfig healthCheckConfig)
        {
            var fakeCalculator = new FakeHealthPolicyCalculator(throwException: true);

            _mockCalculatorFactory
                .Setup(x => x.GetCalculator(healthCheckConfig.Policy))
                .Returns(fakeCalculator);
        }

        private void VerifyLoggerErrorCalled()
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion
    }

    /// <summary>
    /// Fake 实现 - IHealthMetricsStorage 的测试实现
    /// </summary>
    public class FakeHealthMetricsStorage : IHealthMetricsStorage
    {
        private readonly Dictionary<string, CompactHealthMetrics[]> _metricsData = new();
        private readonly Dictionary<string, HealthBufferStats> _statsData = new();
        private readonly Dictionary<string, bool> _sufficientDataFlags = new();

        public void SetupMetrics(string destinationCode, CompactHealthMetrics[] metrics, bool sufficientData)
        {
            _metricsData[destinationCode] = metrics;
            _sufficientDataFlags[destinationCode] = sufficientData;

            // 同时更新统计信息
            _statsData[destinationCode] = CalculateStats(metrics);
        }

        public void StoreMetrics(string destinationCode, CompactHealthMetrics metrics)
        {
            if (!_metricsData.ContainsKey(destinationCode))
            {
                _metricsData[destinationCode] = Array.Empty<CompactHealthMetrics>();
            }

            var existing = _metricsData[destinationCode].ToList();
            existing.Insert(0, metrics);

            // 保持最近 1000 条记录
            if (existing.Count > 1000)
                existing = existing.Take(1000).ToList();

            _metricsData[destinationCode] = existing.ToArray();
        }

        public ReadOnlySpan<CompactHealthMetrics> GetRecentMetrics(string destinationCode, int count)
        {
            if (_metricsData.TryGetValue(destinationCode, out var metrics))
            {
                var recent = metrics.Take(count).ToArray();
                return new ReadOnlySpan<CompactHealthMetrics>(recent);
            }
            return new ReadOnlySpan<CompactHealthMetrics>();
        }

        public bool HasSufficientData(string destinationCode, int requiredDataPoints)
        {
            if (_sufficientDataFlags.TryGetValue(destinationCode, out var sufficient))
            {
                return sufficient;
            }

            if (_metricsData.TryGetValue(destinationCode, out var metrics))
            {
                return metrics.Length >= requiredDataPoints;
            }

            return false;
        }

        public HealthBufferStats GetStats(string destinationCode)
        {
            if (_statsData.TryGetValue(destinationCode, out var stats))
            {
                return stats;
            }

            if (_metricsData.TryGetValue(destinationCode, out var metrics))
            {
                return CalculateStats(metrics);
            }

            return new HealthBufferStats();
        }

        private static HealthBufferStats CalculateStats(CompactHealthMetrics[] metrics)
        {
            if (metrics.Length == 0)
                return new HealthBufferStats();

            var successCount = metrics.Count(m => m.IsSuccessful);
            var avgResponseTime = metrics.Length > 0
                ? metrics.Average(m => m.ResponseTimeMs)
                : 0;

            return new HealthBufferStats
            {
                TotalCount = metrics.Length,
                SuccessCount = successCount,
                FailureCount = metrics.Length - successCount,
                SuccessRate = metrics.Length > 0 ? (double)successCount / metrics.Length : 0,
                AverageResponseTimeMs = avgResponseTime
            };
        }
    }

    /// <summary>
    /// ✅ Fake 实现 - IHealthPolicyCalculator 的测试实现
    /// 解决 ReadOnlySpan<T> 无法被 Mock 的问题
    /// </summary>
    public class FakeHealthPolicyCalculator : IHealthPolicyCalculator
    {
        private readonly bool _isHealthy;
        private readonly double _successRate;
        private readonly double _avgResponseTime;
        private readonly int _consecutiveFailures;
        private readonly string? _error;
        private readonly bool _throwException;

        public HealthCheckPolicy SupportedPolicy => HealthCheckPolicy.ConsecutiveFailures;

        // 健康场景
        public FakeHealthPolicyCalculator(bool isHealthy, double successRate, double avgResponseTime)
        {
            _isHealthy = isHealthy;
            _successRate = successRate;
            _avgResponseTime = avgResponseTime;
            _throwException = false;
        }

        // 不健康场景
        public FakeHealthPolicyCalculator(bool isHealthy, int consecutiveFailures, string? error)
        {
            _isHealthy = isHealthy;
            _consecutiveFailures = consecutiveFailures;
            _error = error;
            _throwException = false;
        }

        // 异常场景
        public FakeHealthPolicyCalculator(bool throwException)
        {
            _throwException = throwException;
        }

        /// <summary>
        /// ✅ 关键：接收 ReadOnlySpan 参数，Fake 实现可以直接处理
        /// </summary>
        public HealthStatus Calculate(ReadOnlySpan<CompactHealthMetrics> metrics, HealthCheckConfig config)
        {
            if (_throwException)
            {
                throw new InvalidOperationException("Calculator error");
            }

            if (_isHealthy)
            {
                return HealthStatus.CreateHealthy(successRate: _successRate, avgResponseTime: _avgResponseTime);
            }
            else
            {
                return HealthStatus.CreateUnhealthy(consecutiveFailures: _consecutiveFailures, error: _error);
            }
        }
    }
}
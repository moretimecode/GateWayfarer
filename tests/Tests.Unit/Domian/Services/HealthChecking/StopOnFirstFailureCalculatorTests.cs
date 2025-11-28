using Domain.Services;
using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;
using Infrastructure.Services.HealthPolicyCalculators;
using Xunit;

namespace Tests.Unit.Infrastructure.Services.HealthPolicyCalculators;

/// <summary>
/// StopOnFirstFailureCalculator 单元测试
/// 策略：只要最新一次检查失败，就标记为不健康
/// </summary>
public class StopOnFirstFailureCalculatorTests
{
    private readonly StopOnFirstFailureCalculator _calculator;
    private readonly HealthCheckConfig _defaultConfig;

    public StopOnFirstFailureCalculatorTests()
    {
        _calculator = new StopOnFirstFailureCalculator();
        _defaultConfig = new HealthCheckConfig(); // 使用默认配置
    }

    #region 基础属性测试

    [Fact]
    public void SupportedPolicy_ReturnsStopOnFirstFailure()
    {
        // Assert
        Assert.Equal(HealthCheckPolicy.StopOnFirstFailure, _calculator.SupportedPolicy);
    }

    #endregion

    #region Calculate 测试 - 空数据

    [Fact]
    public void Calculate_EmptyMetrics_ReturnsUnknown()
    {
        // Arrange
        var metrics = ReadOnlySpan<CompactHealthMetrics>.Empty;

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Unknown, result.StatusType);
        Assert.False(result.IsHealthy);
        Assert.Equal("No metrics data", result.Description);
    }

    #endregion

    #region Calculate 测试 - 单条数据（成功）

    [Fact]
    public void Calculate_SingleSuccessMetric_ReturnsHealthy()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 100)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.True(result.IsHealthy);
        Assert.Equal(1.0, result.SuccessRate);
        Assert.Equal(100.0, result.AverageResponseTimeMs);
        Assert.True(result.HealthScore > 0.8); // 成功率100%，响应快，分数应该很高
    }

    [Fact]
    public void Calculate_SingleSuccessWithSlowResponse_ReturnsHealthyButLowerScore()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 2000)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.True(result.IsHealthy);
        Assert.Equal(1.0, result.SuccessRate);
        Assert.Equal(2000.0, result.AverageResponseTimeMs);
        Assert.True(result.HealthScore >= 0.79 && result.HealthScore <= 0.81);
    }

    #endregion

    #region Calculate 测试 - 单条数据（失败）

    [Fact]
    public void Calculate_SingleFailureMetric_ReturnsUnhealthy()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 500, responseTimeMs: 200)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.False(result.IsHealthy);
        Assert.Equal(1, result.ConsecutiveFailures);
        Assert.Equal(0.0, result.HealthScore);
        Assert.Equal("First failure detected", result.LastError);
    }

    [Fact]
    public void Calculate_SingleTimeoutMetric_ReturnsUnhealthy()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsTimeout, responseTimeMs: 5000)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.False(result.IsHealthy);
        Assert.Equal(1, result.ConsecutiveFailures);
        Assert.Equal(0.0, result.HealthScore);
    }

    [Fact]
    public void Calculate_StatusCode429_ReturnsUnhealthy()
    {
        // Arrange - 429 Too Many Requests
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 429)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.False(result.IsHealthy);
    }

    [Fact]
    public void Calculate_StatusCode503_ReturnsUnhealthy()
    {
        // Arrange - 503 Service Unavailable
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 503)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.False(result.IsHealthy);
    }

    #endregion

    #region Calculate 测试 - 只看最新一条（核心逻辑）

    [Fact]
    public void Calculate_LatestIsSuccess_IgnoresPreviousFailures()
    {
        // Arrange - 索引0是最新的（成功），历史有失败
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful, statusCode: 200, responseTimeMs: 100),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 503)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert - 只看最新的，所以是健康
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.True(result.IsHealthy);
        Assert.Equal(1.0, result.SuccessRate);
        Assert.Equal(100.0, result.AverageResponseTimeMs);
    }

    [Fact]
    public void Calculate_LatestIsFailure_IgnoresPreviousSuccesses()
    {
        // Arrange - 索引0是最新的（失败），历史全是成功
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful, statusCode: 200),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, statusCode: 200)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert - 只看最新的，所以是不健康
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.False(result.IsHealthy);
        Assert.Equal(1, result.ConsecutiveFailures);
    }

    [Fact]
    public void Calculate_LatestIsTimeout_IgnoresHistoricalData()
    {
        // Arrange - 最新的是超时
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsTimeout, responseTimeMs: 5000),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 100),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 150)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.False(result.IsHealthy);
    }

    [Fact]
    public void Calculate_AllSuccess_StillOnlyChecksLatest()
    {
        // Arrange - 全部成功
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 100),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 120),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 90)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.True(result.IsHealthy);
        Assert.Equal(1.0, result.SuccessRate);
        Assert.Equal(100.0, result.AverageResponseTimeMs); // 只看索引0
    }

    [Fact]
    public void Calculate_AllFailure_StillOnlyChecksLatest()
    {
        // Arrange - 全部失败
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 504)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.False(result.IsHealthy);
        Assert.Equal(1, result.ConsecutiveFailures);
    }

    #endregion

    #region Calculate 测试 - 边界情况

    [Fact]
    public void Calculate_LargeDataset_OnlyChecksFirst()
    {
        var metrics = new CompactHealthMetrics[100];
        for (int i = 0; i < 100; i++)
        {
            var isSuccess = i > 0;
            metrics[i] = CreateMetrics(
                timestampMs: (100 - i) * 1000,
                flags: isSuccess ? HealthCheckFlags.IsSuccessful : HealthCheckFlags.None,
                statusCode: isSuccess ? (ushort)200 : (ushort)500,
                responseTimeMs: (ushort)(100 + i)
            );
        }

        var result = _calculator.Calculate(metrics, _defaultConfig);

        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.False(result.IsHealthy);
    }

    [Fact]
    public void Calculate_VeryHighResponseTime_HandlesCorrectly()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: ushort.MaxValue)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.Equal((double)ushort.MaxValue, result.AverageResponseTimeMs);
        Assert.Equal(0.8, result.HealthScore, precision: 2);
    }

    [Fact]
    public void Calculate_ZeroResponseTime_HandlesCorrectly()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 0)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.Equal(0.0, result.AverageResponseTimeMs);
        // 健康分数 = 1.0 * 0.8 + (1.0 - 0) * 0.2 = 1.0
        Assert.Equal(1.0, result.HealthScore);
    }

    #endregion

    #region Calculate 测试 - 验证返回的时间戳

    [Fact]
    public void Calculate_SetsLastCheckTime()
    {
        // Arrange
        var beforeTest = DateTimeOffset.UtcNow;
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);
        var afterTest = DateTimeOffset.UtcNow;

        // Assert
        Assert.True(result.LastCheckTime >= beforeTest);
        Assert.True(result.LastCheckTime <= afterTest);
    }

    #endregion

    #region Helper Methods

    private CompactHealthMetrics CreateMetrics(
        long timestampMs,
        ushort responseTimeMs = 100,
        ushort statusCode = 200,
        HealthCheckFlags flags = HealthCheckFlags.IsSuccessful)
    {
        return new CompactHealthMetrics
        {
            TimestampMs = timestampMs,
            ResponseTimeMs = responseTimeMs,
            StatusCode = statusCode,
            Flags = flags
        };
    }

    #endregion
}
using Domain.Services;
using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;
using Infrastructure.Services.HealthPolicyCalculators;
using Xunit;

namespace Tests.Unit.Infrastructure.Services.HealthPolicyCalculators;

/// <summary>
/// ContinueOnFailureCalculator 单元测试
/// 策略：即使失败也继续标记为健康，永远返回 Healthy
/// </summary>
public class ContinueOnFailureCalculatorTests
{
    private readonly ContinueOnFailureCalculator _calculator;
    private readonly HealthCheckConfig _defaultConfig;

    public ContinueOnFailureCalculatorTests()
    {
        _calculator = new ContinueOnFailureCalculator();
        _defaultConfig = new HealthCheckConfig();
    }

    #region 基础属性测试

    [Fact]
    public void SupportedPolicy_ReturnsContinueOnFailure()
    {
        // Assert
        Assert.Equal(HealthCheckPolicy.ContinueOnFailure, _calculator.SupportedPolicy);
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

    #region Calculate 测试 - 核心策略：始终返回 Healthy

    [Fact]
    public void Calculate_SingleSuccess_ReturnsHealthy()
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
    }

    [Fact]
    public void Calculate_SingleFailure_StillReturnsHealthy()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 500)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert - 核心：即使失败也返回 Healthy
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.True(result.IsHealthy);
        Assert.Equal(0.0, result.SuccessRate); // 但成功率正确计算
        Assert.Equal(0, result.ConsecutiveFailures); // Continue 策略不跟踪连续失败
    }

    [Fact]
    public void Calculate_AllFailures_StillReturnsHealthy()
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

        // Assert - 核心：全失败也返回 Healthy
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.True(result.IsHealthy);
        Assert.Equal(0.0, result.SuccessRate);
    }

    [Fact]
    public void Calculate_AllTimeouts_StillReturnsHealthy()
    {
        // Arrange - 全部超时
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsTimeout, responseTimeMs: 5000),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsTimeout, responseTimeMs: 5000),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsTimeout, responseTimeMs: 5000)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.True(result.IsHealthy);
        Assert.Equal(0.0, result.SuccessRate);
        Assert.Equal(5000.0, result.AverageResponseTimeMs);
    }

    [Fact]
    public void Calculate_MixedSuccessAndFailure_ReturnsHealthy()
    {
        // Arrange - 混合场景：50% 成功
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 100),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 120),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 503)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.True(result.IsHealthy);
        Assert.Equal(0.5, result.SuccessRate); // 50% 成功率
    }

    #endregion

    #region Calculate 测试 - 成功率计算

    [Fact]
    public void Calculate_CalculatesSuccessRateCorrectly_AllSuccess()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.Equal(1.0, result.SuccessRate); // 100%
    }

    [Fact]
    public void Calculate_CalculatesSuccessRateCorrectly_80Percent()
    {
        // Arrange - 5条记录，4成功1失败
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.Equal(0.8, result.SuccessRate); // 80%
    }

    [Fact]
    public void Calculate_CalculatesSuccessRateCorrectly_20Percent()
    {
        // Arrange - 5条记录，1成功4失败
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 504),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsTimeout)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.Equal(0.2, result.SuccessRate); // 20%
    }

    [Fact]
    public void Calculate_CalculatesSuccessRateCorrectly_ZeroPercent()
    {
        // Arrange - 全部失败
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsTimeout),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 503)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.Equal(0.0, result.SuccessRate); // 0%
    }

    #endregion

    #region Calculate 测试 - 平均响应时间计算

    [Fact]
    public void Calculate_CalculatesAverageResponseTime_SingleMetric()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 123)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(123.0, result.AverageResponseTimeMs);
    }

    [Fact]
    public void Calculate_CalculatesAverageResponseTime_MultipleMetrics()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 100),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 200),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 300),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 400)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(250.0, result.AverageResponseTimeMs); // (100+200+300+400)/4 = 250
    }

    [Fact]
    public void Calculate_AverageResponseTime_IncludesFailures()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 100),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 500, responseTimeMs: 500),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 200)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        // 失败的请求也计入平均响应时间
        Assert.True(result.AverageResponseTimeMs > 250); // (100+500+200)/3 ≈ 267
    }

    [Fact]
    public void Calculate_AverageResponseTime_IncludesTimeouts()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 100),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsTimeout, responseTimeMs: 5000)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(2550.0, result.AverageResponseTimeMs); // (100+5000)/2 = 2550
    }

    [Fact]
    public void Calculate_AverageResponseTime_ZeroResponseTime()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 0)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(0.0, result.AverageResponseTimeMs);
    }

    #endregion

    #region Calculate 测试 - 健康分数计算

    [Fact]
    public void Calculate_HealthScore_AllSuccess_FastResponse()
    {
        // Arrange - 100% 成功，响应快
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 100),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 150),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 200)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        // successRate=1.0, avgResponseTime=150ms
        // healthScore = 1.0 * 0.8 + (1.0 - min(150/1000, 1.0)) * 0.2
        //             = 0.8 + 0.85 * 0.2 = 0.97
        Assert.True(result.HealthScore > 0.95);
    }

    [Fact]
    public void Calculate_HealthScore_AllSuccess_SlowResponse()
    {
        // Arrange - 100% 成功，但响应慢
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 2000),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 2500),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 3000)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(1, result.HealthScore, precision: 2);
    }

    [Fact]
    public void Calculate_HealthScore_LowSuccessRate_FastResponse()
    {
        // Arrange - 20% 成功，响应快
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 100),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 504),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 500)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        Assert.Equal(0.2, result.HealthScore);
    }

    [Fact]
    public void Calculate_HealthScore_AllFailure()
    {
        // Arrange - 0% 成功
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 503)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        Assert.Equal(0.1, result.HealthScore);
    }

    #endregion

    #region Calculate 测试 - 边界情况

    [Fact]
    public void Calculate_LargeDataset_CalculatesCorrectly()
    {
        // Arrange - 100条记录
        var metrics = new CompactHealthMetrics[100];
        var successCount = 0;
        var totalResponseTime = 0L;

        for (int i = 0; i < 100; i++)
        {
            var isSuccess = i % 3 == 0; // 每3条成功1条
            if (isSuccess) successCount++;

            var responseTime = (ushort)(100 + i);
            totalResponseTime += responseTime;

            metrics[i] = CreateMetrics(
                timestampMs: (100 - i) * 1000,
                flags: isSuccess ? HealthCheckFlags.IsSuccessful : HealthCheckFlags.None,
                statusCode: isSuccess ? (ushort)200 : (ushort)500,
                responseTimeMs: responseTime
            );
        }

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.Equal((double)successCount / 100, result.SuccessRate);
        Assert.Equal((double)totalResponseTime / 100, result.AverageResponseTimeMs);
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
    }

    [Fact]
    public void Calculate_StatusCode429_TreatedAsFailure()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 429),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, statusCode: 200)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType); // 仍然 Healthy
        Assert.Equal(0.5, result.SuccessRate); // 但成功率受影响
    }

    [Fact]
    public void Calculate_StatusCode503_TreatedAsFailure()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, statusCode: 200)
        };

        // Act
        var result = _calculator.Calculate(metrics, _defaultConfig);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.Equal(0.5, result.SuccessRate);
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
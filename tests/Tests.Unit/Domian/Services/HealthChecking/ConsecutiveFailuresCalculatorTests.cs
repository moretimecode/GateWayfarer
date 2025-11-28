using Domain.Services;
using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;
using Infrastructure.Services.HealthPolicyCalculators;
using Xunit;

namespace Tests.Unit.Infrastructure.Services.HealthPolicyCalculators;

/// <summary>
/// ConsecutiveFailuresCalculator 单元测试
/// 策略：基于连续失败/成功次数判断健康状态
/// </summary>
public class ConsecutiveFailuresCalculatorTests
{
    private readonly ConsecutiveFailuresCalculator _calculator;

    public ConsecutiveFailuresCalculatorTests()
    {
        _calculator = new ConsecutiveFailuresCalculator();
    }

    #region 基础属性测试

    [Fact]
    public void SupportedPolicy_ReturnsConsecutiveFailures()
    {
        // Assert
        Assert.Equal(HealthCheckPolicy.ConsecutiveFailures, _calculator.SupportedPolicy);
    }

    #endregion

    #region Calculate 测试 - 空数据

    [Fact]
    public void Calculate_EmptyMetrics_ReturnsUnknown()
    {
        // Arrange
        var metrics = ReadOnlySpan<CompactHealthMetrics>.Empty;
        var config = new HealthCheckConfig();

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Unknown, result.StatusType);
        Assert.False(result.IsHealthy);
        Assert.Equal("No metrics data", result.Description);
    }

    #endregion

    #region Calculate 测试 - Healthy 状态

    [Fact]
    public void Calculate_AllSuccess_ReturnsHealthy()
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
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.True(result.IsHealthy);
        Assert.Equal(5, result.ConsecutiveSuccesses);
        Assert.Equal(0, result.ConsecutiveFailures);
        Assert.Equal(1.0, result.SuccessRate);
    }

    [Fact]
    public void Calculate_RecentSuccessAfterFailures_ReturnsHealthy()
    {
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.True(result.IsHealthy);
        Assert.Equal(2, result.ConsecutiveSuccesses);
        Assert.Equal(0, result.ConsecutiveFailures);
    }

    [Fact]
    public void Calculate_BelowFailureThreshold_ReturnsHealthy()
    {
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.True(result.IsHealthy);
        Assert.Equal(2, result.ConsecutiveFailures);
        Assert.Equal(0, result.ConsecutiveSuccesses);
    }

    #endregion

    #region Calculate 测试 - Unhealthy 状态

    [Fact]
    public void Calculate_ReachFailureThreshold_ReturnsUnhealthy()
    {
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 504),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.False(result.IsHealthy);
        Assert.Equal(3, result.ConsecutiveFailures);
        Assert.Equal(0, result.ConsecutiveSuccesses);
    }

    [Fact]
    public void Calculate_ExceedFailureThreshold_ReturnsUnhealthy()
    {
        // Arrange - 连续失败5次，超过阈值3
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 6000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.None, statusCode: 504),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.False(result.IsHealthy);
        Assert.Equal(5, result.ConsecutiveFailures);
    }

    [Fact]
    public void Calculate_AllFailures_ReturnsUnhealthy()
    {
        // Arrange - 全部失败
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 504)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 2,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.False(result.IsHealthy);
        Assert.Equal(3, result.ConsecutiveFailures);
        Assert.Equal(0.0, result.SuccessRate);
    }

    [Fact]
    public void Calculate_TimeoutsCountAsFailures()
    {
        // Arrange - 连续超时达到阈值
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsTimeout, responseTimeMs: 5000),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsTimeout, responseTimeMs: 5000),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsTimeout, responseTimeMs: 5000)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.Equal(3, result.ConsecutiveFailures);
    }

    #endregion

    #region Calculate 测试 - Recovering 状态

    [Fact]
    public void Calculate_BelowSuccessThreshold_ReturnsRecovering()
    {
        // Arrange - 从不健康恢复中，成功1次但未达到阈值2，且之前有过连续失败
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.IsTimeout),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 504),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.False(result.IsHealthy);
        Assert.Equal(0, result.ConsecutiveSuccesses);
        Assert.Equal(4, result.ConsecutiveFailures);
    }

    #endregion

    #region Calculate 测试 - 连续计数逻辑

    [Fact]
    public void Calculate_ConsecutiveFailures_CountsCorrectly()
    {
        // Arrange - 最近连续失败4次
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 504),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(4, result.ConsecutiveFailures);
        Assert.Equal(0, result.ConsecutiveSuccesses);
    }

    [Fact]
    public void Calculate_ConsecutiveSuccesses_CountsCorrectly()
    {
        // Arrange - 最近连续成功4次
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 500)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(4, result.ConsecutiveSuccesses);
        Assert.Equal(0, result.ConsecutiveFailures);
    }

    [Fact]
    public void Calculate_InterruptedSequence_ResetsCount()
    {
        // Arrange - 成功中断了失败序列
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful), // 中断
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 504),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 500)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(2, result.ConsecutiveFailures); // 只计算最近的2次
        Assert.Equal(0, result.ConsecutiveSuccesses);
    }

    [Fact]
    public void Calculate_AlternatingResults_CountsOnlyLatest()
    {
        // Arrange - 交替成功失败
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 504)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(1, result.ConsecutiveFailures);
        Assert.Equal(0, result.ConsecutiveSuccesses);
        Assert.Equal(HealthStatusType.Healthy, result.StatusType); // 未达阈值
    }

    #endregion

    #region Calculate 测试 - 阈值配置

    [Fact]
    public void Calculate_CustomFailureThreshold_WorksCorrectly()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 6000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.None, statusCode: 504),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 503)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 5,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.Equal(5, result.ConsecutiveFailures);
    }

    [Fact]
    public void Calculate_CustomSuccessThreshold_WorksCorrectly()
    {
        // Arrange - 连续成功3次，阈值是3
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 500)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 3
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.Equal(3, result.ConsecutiveSuccesses);
    }

    [Fact]
    public void Calculate_FailureThreshold1_ImmediateUnhealthy()
    {
        // Arrange - 阈值为1，一次失败就不健康
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 1,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.Equal(1, result.ConsecutiveFailures);
    }

    [Fact]
    public void Calculate_SuccessThreshold1_ImmediateHealthy()
    {
        // Arrange - 阈值为1，一次成功就健康
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.IsSuccessful),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.None, statusCode: 500),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 503),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 504)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 1
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.Equal(1, result.ConsecutiveSuccesses);
    }

    #endregion

    #region Calculate 测试 - 成功率和响应时间

    [Fact]
    public void Calculate_CalculatesSuccessRate()
    {
        // Arrange - 10条记录，6成功4失败
        var metrics = new CompactHealthMetrics[10];
        for (int i = 0; i < 10; i++)
        {
            var isSuccess = i < 6;
            metrics[i] = CreateMetrics(
                timestampMs: (10 - i) * 1000,
                flags: isSuccess ? HealthCheckFlags.IsSuccessful : HealthCheckFlags.None,
                statusCode: isSuccess ? (ushort)200 : (ushort)500
            );
        }
        var config = new HealthCheckConfig(
            failureThreshold: 5,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(0.6, result.SuccessRate); // 60%
    }

    [Fact]
    public void Calculate_CalculatesAverageResponseTime()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 100),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 200),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.None, statusCode: 500, responseTimeMs: 300),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 400)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(250.0, result.AverageResponseTimeMs); // (100+200+300+400)/4
    }

    #endregion

    #region Calculate 测试 - 健康分数

    [Fact]
    public void Calculate_HealthScore_BasedOnSuccessRateAndResponseTime()
    {
        // Arrange - 高成功率，快响应
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 5000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 100),
            CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 150),
            CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 120),
            CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 130),
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 500)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.True(result.HealthScore > 0.8); // 80%成功率+快响应
    }

    #endregion

    #region Calculate 测试 - 边界情况

    [Fact]
    public void Calculate_SingleMetric_Success()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 1
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Healthy, result.StatusType);
        Assert.Equal(1, result.ConsecutiveSuccesses);
    }

    [Fact]
    public void Calculate_SingleMetric_Failure()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None, statusCode: 500)
        };
        var config = new HealthCheckConfig(
            failureThreshold: 1,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        // Assert
        Assert.Equal(HealthStatusType.Unhealthy, result.StatusType);
        Assert.Equal(1, result.ConsecutiveFailures);
    }

    [Fact]
    public void Calculate_LargeDataset_PerformanceTest()
    {
        // Arrange - 1000条记录
        var metrics = new CompactHealthMetrics[1000];
        for (int i = 0; i < 1000; i++)
        {
            metrics[i] = CreateMetrics(
                timestampMs: (1000 - i) * 1000,
                flags: i % 2 == 0 ? HealthCheckFlags.IsSuccessful : HealthCheckFlags.None,
                statusCode: i % 2 == 0 ? (ushort)200 : (ushort)500
            );
        }
        var config = new HealthCheckConfig(
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var result = _calculator.Calculate(metrics, config);

        Assert.NotNull(result);
        Assert.Equal(0, result.ConsecutiveFailures);
        Assert.Equal(1, result.ConsecutiveSuccesses);
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
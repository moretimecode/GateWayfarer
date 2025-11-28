using Domain.Services.HealthChecking;
using Xunit;

namespace Tests.Unit.Domain.Services.HealthChecking;

/// <summary>
/// HealthBufferStats 单元测试
/// </summary>
public class HealthBufferStatsTests
{
    #region Empty 测试

    [Fact]
    public void Empty_HasZeroValues()
    {
        // Act
        var stats = HealthBufferStats.Empty;

        // Assert
        Assert.Equal(0, stats.TotalCount);
        Assert.Equal(0, stats.SuccessCount);
        Assert.Equal(0, stats.FailureCount);
        Assert.Equal(0, stats.SuccessRate);
        Assert.Equal(0, stats.AverageResponseTimeMs);
        Assert.Equal(0, stats.MaxResponseTimeMs);
        Assert.Equal(0, stats.MinResponseTimeMs);
        Assert.Equal(0, stats.ConsecutiveFailures);
        Assert.Equal(0, stats.ConsecutiveSuccesses);
        Assert.Equal(0, stats.LatestCheckTimestampMs);
        Assert.False(stats.LatestCheckSuccess);
    }

    [Fact]
    public void Empty_HasNoData()
    {
        // Act
        var stats = HealthBufferStats.Empty;

        // Assert
        Assert.False(stats.HasData);
    }

    [Fact]
    public void Empty_LatestCheckTimeIsNull()
    {
        // Act
        var stats = HealthBufferStats.Empty;

        // Assert
        Assert.Null(stats.LatestCheckTime);
    }

    #endregion

    #region CreateFromMetrics 测试 - 空数据

    [Fact]
    public void CreateFromMetrics_EmptySpan_ReturnsEmpty()
    {
        // Arrange
        var metrics = ReadOnlySpan<CompactHealthMetrics>.Empty;

        // Act
        var stats = HealthBufferStats.CreateFromMetrics(metrics);

        // Assert
        Assert.Equal(HealthBufferStats.Empty, stats);
    }

    #endregion

    #region CreateFromMetrics 测试 - 单个指标

    [Fact]
    public void CreateFromMetrics_SingleSuccessMetric_CorrectStats()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(isSuccessful: true, responseTimeMs: 100, timestampMs: 1000)
        };

        // Act
        var stats = HealthBufferStats.CreateFromMetrics(metrics);

        // Assert
        Assert.Equal(1, stats.TotalCount);
        Assert.Equal(1, stats.SuccessCount);
        Assert.Equal(0, stats.FailureCount);
        Assert.Equal(1.0, stats.SuccessRate);
        Assert.Equal(100, stats.AverageResponseTimeMs);
        Assert.Equal(100, stats.MaxResponseTimeMs);
        Assert.Equal(100, stats.MinResponseTimeMs);
        Assert.Equal(1, stats.ConsecutiveSuccesses);
        Assert.Equal(0, stats.ConsecutiveFailures);
        Assert.Equal(1000, stats.LatestCheckTimestampMs);
        Assert.True(stats.LatestCheckSuccess);
    }

    [Fact]
    public void CreateFromMetrics_SingleFailureMetric_CorrectStats()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(isSuccessful: false, responseTimeMs: 5000, timestampMs: 2000)
        };

        // Act
        var stats = HealthBufferStats.CreateFromMetrics(metrics);

        // Assert
        Assert.Equal(1, stats.TotalCount);
        Assert.Equal(0, stats.SuccessCount);
        Assert.Equal(1, stats.FailureCount);
        Assert.Equal(0, stats.SuccessRate);
        Assert.Equal(5000, stats.AverageResponseTimeMs);
        Assert.Equal(1, stats.ConsecutiveFailures);
        Assert.Equal(0, stats.ConsecutiveSuccesses);
        Assert.Equal(2000, stats.LatestCheckTimestampMs);
        Assert.False(stats.LatestCheckSuccess);
    }

    #endregion

    #region CreateFromMetrics 测试 - 多个指标

    [Fact]
    public void CreateFromMetrics_AllSuccessful_CorrectStats()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(true, 100, 1000),
            CreateMetrics(true, 200, 2000),
            CreateMetrics(true, 150, 3000)
        };

        // Act
        var stats = HealthBufferStats.CreateFromMetrics(metrics);

        // Assert
        Assert.Equal(3, stats.TotalCount);
        Assert.Equal(3, stats.SuccessCount);
        Assert.Equal(0, stats.FailureCount);
        Assert.Equal(1.0, stats.SuccessRate);
        Assert.Equal(150, stats.AverageResponseTimeMs); // (100+200+150)/3
        Assert.Equal(200, stats.MaxResponseTimeMs);
        Assert.Equal(100, stats.MinResponseTimeMs);
        Assert.Equal(3, stats.ConsecutiveSuccesses);
        Assert.Equal(0, stats.ConsecutiveFailures);
    }

    [Fact]
    public void CreateFromMetrics_AllFailed_CorrectStats()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(false, 5000, 1000),
            CreateMetrics(false, 6000, 2000),
            CreateMetrics(false, 5500, 3000)
        };

        // Act
        var stats = HealthBufferStats.CreateFromMetrics(metrics);

        // Assert
        Assert.Equal(3, stats.TotalCount);
        Assert.Equal(0, stats.SuccessCount);
        Assert.Equal(3, stats.FailureCount);
        Assert.Equal(0, stats.SuccessRate);
        Assert.Equal(5500, stats.AverageResponseTimeMs);
        Assert.Equal(6000, stats.MaxResponseTimeMs);
        Assert.Equal(5000, stats.MinResponseTimeMs);
        Assert.Equal(3, stats.ConsecutiveFailures);
        Assert.Equal(0, stats.ConsecutiveSuccesses);
    }

    [Fact]
    public void CreateFromMetrics_MixedResults_CorrectStats()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(true, 100, 1000),
            CreateMetrics(true, 150, 2000),
            CreateMetrics(false, 5000, 3000),
            CreateMetrics(false, 6000, 4000),
            CreateMetrics(true, 120, 5000)
        };

        // Act
        var stats = HealthBufferStats.CreateFromMetrics(metrics);

        // Assert
        Assert.Equal(5, stats.TotalCount);
        Assert.Equal(3, stats.SuccessCount);
        Assert.Equal(2, stats.FailureCount);
        Assert.Equal(0.6, stats.SuccessRate, precision: 2);
        Assert.Equal(2274, stats.AverageResponseTimeMs, precision: 0); // (100+150+5000+6000+120)/5
        Assert.Equal(6000, stats.MaxResponseTimeMs);
        Assert.Equal(100, stats.MinResponseTimeMs);
    }

    #endregion

    #region CreateFromMetrics 测试 - 连续计数

    [Fact]
    public void CreateFromMetrics_ConsecutiveSuccesses_CountsCorrectly()
    {
        // Arrange - 最新的3个都是成功
        var metrics = new[]
        {
            CreateMetrics(true, 100, 3000),  // 最新
            CreateMetrics(true, 110, 2000),
            CreateMetrics(true, 120, 1000),
            CreateMetrics(false, 5000, 500), // 更早的失败不计入连续成功
            CreateMetrics(false, 5000, 400)
        };

        // Act
        var stats = HealthBufferStats.CreateFromMetrics(metrics);

        // Assert
        Assert.Equal(3, stats.ConsecutiveSuccesses);
        Assert.Equal(0, stats.ConsecutiveFailures);
        Assert.True(stats.LatestCheckSuccess);
    }

    [Fact]
    public void CreateFromMetrics_ConsecutiveFailures_CountsCorrectly()
    {
        // Arrange - 最新的4个都是失败
        var metrics = new[]
        {
            CreateMetrics(false, 5000, 4000), // 最新
            CreateMetrics(false, 5100, 3000),
            CreateMetrics(false, 5200, 2000),
            CreateMetrics(false, 5300, 1000),
            CreateMetrics(true, 100, 500)     // 更早的成功不计入连续失败
        };

        // Act
        var stats = HealthBufferStats.CreateFromMetrics(metrics);

        // Assert
        Assert.Equal(4, stats.ConsecutiveFailures);
        Assert.Equal(0, stats.ConsecutiveSuccesses);
        Assert.False(stats.LatestCheckSuccess);
    }

    [Fact]
    public void CreateFromMetrics_AlternatingResults_NoConsecutiveCount()
    {
        // Arrange
        var metrics = new[]
        {
            CreateMetrics(true, 100, 5000),
            CreateMetrics(false, 5000, 4000),
            CreateMetrics(true, 110, 3000),
            CreateMetrics(false, 5100, 2000),
            CreateMetrics(true, 120, 1000)
        };

        // Act
        var stats = HealthBufferStats.CreateFromMetrics(metrics);

        // Assert
        Assert.Equal(1, stats.ConsecutiveSuccesses); // 只有最新的1个成功
        Assert.Equal(0, stats.ConsecutiveFailures);
    }

    #endregion

    #region 属性访问器测试

    [Fact]
    public void HasData_WithData_ReturnsTrue()
    {
        // Arrange
        var stats = new HealthBufferStats { TotalCount = 10 };

        // Assert
        Assert.True(stats.HasData);
    }

    [Fact]
    public void HasData_NoData_ReturnsFalse()
    {
        // Arrange
        var stats = new HealthBufferStats { TotalCount = 0 };

        // Assert
        Assert.False(stats.HasData);
    }

    [Fact]
    public void FailureRate_CalculatesCorrectly()
    {
        // Arrange
        var stats = new HealthBufferStats { SuccessRate = 0.8 };

        // Act
        var failureRate = stats.FailureRate;

        // Assert
        Assert.Equal(0.2, failureRate, precision: 2);
    }

    [Fact]
    public void LatestCheckTime_WithValidTimestamp_ReturnsDateTimeOffset()
    {
        // Arrange
        var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var stats = new HealthBufferStats { LatestCheckTimestampMs = timestampMs };

        // Act
        var checkTime = stats.LatestCheckTime;

        // Assert
        Assert.NotNull(checkTime);
        Assert.Equal(timestampMs, checkTime.Value.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void LatestCheckTime_WithZeroTimestamp_ReturnsNull()
    {
        // Arrange
        var stats = new HealthBufferStats { LatestCheckTimestampMs = 0 };

        // Act
        var checkTime = stats.LatestCheckTime;

        // Assert
        Assert.Null(checkTime);
    }

    #endregion

    #region GetHealthDescription 测试

    [Fact]
    public void GetHealthDescription_NoData_ReturnsNoDataMessage()
    {
        // Arrange
        var stats = HealthBufferStats.Empty;

        // Act
        var description = stats.GetHealthDescription();

        // Assert
        Assert.Equal("无检查数据", description);
    }

    [Fact]
    public void GetHealthDescription_WithData_ReturnsFormattedString()
    {
        // Arrange
        var stats = new HealthBufferStats
        {
            TotalCount = 100,
            SuccessRate = 0.95,
            AverageResponseTimeMs = 123.45
        };

        // Act
        var description = stats.GetHealthDescription();

        // Assert
        Assert.Contains("95", description);
        Assert.Contains("123", description);
        Assert.Contains("100", description);
    }

    #endregion

    #region HasSufficientData 测试

    [Fact]
    public void HasSufficientData_DefaultMinimum_ChecksForAtLeastOne()
    {
        // Arrange
        var stats1 = new HealthBufferStats { TotalCount = 0 };
        var stats2 = new HealthBufferStats { TotalCount = 1 };
        var stats3 = new HealthBufferStats { TotalCount = 10 };

        // Assert
        Assert.False(stats1.HasSufficientData());
        Assert.True(stats2.HasSufficientData());
        Assert.True(stats3.HasSufficientData());
    }

    [Fact]
    public void HasSufficientData_CustomMinimum_ChecksCorrectly()
    {
        // Arrange
        var stats = new HealthBufferStats { TotalCount = 5 };

        // Assert
        Assert.True(stats.HasSufficientData(5));
        Assert.False(stats.HasSufficientData(6));
        Assert.True(stats.HasSufficientData(3));
    }

    #endregion

    #region CalculateHealthScore 测试

    [Fact]
    public void CalculateHealthScore_NoData_ReturnsZero()
    {
        // Arrange
        var stats = HealthBufferStats.Empty;

        // Act
        var score = stats.CalculateHealthScore();

        // Assert
        Assert.Equal(0, score);
    }

    [Fact]
    public void CalculateHealthScore_PerfectHealth_Returns100()
    {
        // Arrange
        var stats = new HealthBufferStats
        {
            TotalCount = 100,
            SuccessRate = 1.0,
            AverageResponseTimeMs = 50,
            ConsecutiveFailures = 0
        };

        // Act
        var score = stats.CalculateHealthScore();

        // Assert
        Assert.Equal(100, score);
    }

    [Fact]
    public void CalculateHealthScore_FastResponseNoDeduction_CorrectScore()
    {
        // Arrange - 100ms以内不扣分
        var stats = new HealthBufferStats
        {
            TotalCount = 100,
            SuccessRate = 0.9,
            AverageResponseTimeMs = 80,
            ConsecutiveFailures = 0
        };

        // Act
        var score = stats.CalculateHealthScore();

        // Assert
        Assert.Equal(90, score); // 90% * 100 - 0 (响应快)
    }

    [Fact]
    public void CalculateHealthScore_MediumResponse_Deducts5Points()
    {
        // Arrange - 100-500ms扣5分
        var stats = new HealthBufferStats
        {
            TotalCount = 100,
            SuccessRate = 1.0,
            AverageResponseTimeMs = 300,
            ConsecutiveFailures = 0
        };

        // Act
        var score = stats.CalculateHealthScore();

        // Assert
        Assert.Equal(95, score); // 100 - 5
    }

    [Fact]
    public void CalculateHealthScore_SlowResponse_Deducts15Points()
    {
        // Arrange - 500-1000ms扣15分
        var stats = new HealthBufferStats
        {
            TotalCount = 100,
            SuccessRate = 1.0,
            AverageResponseTimeMs = 800,
            ConsecutiveFailures = 0
        };

        // Act
        var score = stats.CalculateHealthScore();

        // Assert
        Assert.Equal(85, score); // 100 - 15
    }

    [Fact]
    public void CalculateHealthScore_VerySlowResponse_Deducts25Points()
    {
        // Arrange - 超过1s扣25分
        var stats = new HealthBufferStats
        {
            TotalCount = 100,
            SuccessRate = 1.0,
            AverageResponseTimeMs = 1500,
            ConsecutiveFailures = 0
        };

        // Act
        var score = stats.CalculateHealthScore();

        // Assert
        Assert.Equal(75, score); // 100 - 25
    }

    [Fact]
    public void CalculateHealthScore_ConsecutiveFailures_DeductsPoints()
    {
        // Arrange - 每次连续失败扣10分
        var stats = new HealthBufferStats
        {
            TotalCount = 100,
            SuccessRate = 0.8,
            AverageResponseTimeMs = 100,
            ConsecutiveFailures = 3
        };

        // Act
        var score = stats.CalculateHealthScore();

        // Assert
        Assert.Equal(50, score); // 80 - 0 (响应快) - 30 (3次连续失败)
    }

    [Fact]
    public void CalculateHealthScore_MultipleFactors_CombinedDeduction()
    {
        // Arrange - 低成功率 + 慢响应 + 连续失败
        var stats = new HealthBufferStats
        {
            TotalCount = 100,
            SuccessRate = 0.6,
            AverageResponseTimeMs = 1200,
            ConsecutiveFailures = 2
        };

        // Act
        var score = stats.CalculateHealthScore();

        // Assert
        Assert.Equal(15, score); // 60 - 25 (慢响应) - 20 (2次连续失败)
    }

    [Fact]
    public void CalculateHealthScore_NeverBelowZero()
    {
        // Arrange - 分数应该为负但被限制为0
        var stats = new HealthBufferStats
        {
            TotalCount = 100,
            SuccessRate = 0.1,
            AverageResponseTimeMs = 2000,
            ConsecutiveFailures = 10
        };

        // Act
        var score = stats.CalculateHealthScore();

        // Assert
        Assert.Equal(0, score); // 不会低于0
        Assert.True(score >= 0);
    }

    [Fact]
    public void CalculateHealthScore_NeverAbove100()
    {
        // Arrange
        var stats = new HealthBufferStats
        {
            TotalCount = 100,
            SuccessRate = 1.0,
            AverageResponseTimeMs = 10,
            ConsecutiveFailures = 0
        };

        // Act
        var score = stats.CalculateHealthScore();

        // Assert
        Assert.True(score <= 100);
    }

    #endregion

    #region Equals 测试

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var stats1 = new HealthBufferStats
        {
            TotalCount = 100,
            SuccessCount = 80,
            FailureCount = 20,
            SuccessRate = 0.8,
            AverageResponseTimeMs = 150.5,
            LatestCheckTimestampMs = 1000,
            LatestCheckSuccess = true
        };
        var stats2 = new HealthBufferStats
        {
            TotalCount = 100,
            SuccessCount = 80,
            FailureCount = 20,
            SuccessRate = 0.8,
            AverageResponseTimeMs = 150.5,
            LatestCheckTimestampMs = 1000,
            LatestCheckSuccess = true
        };

        // Assert
        Assert.True(stats1.Equals(stats2));
        Assert.True(stats1 == stats2);
        Assert.False(stats1 != stats2);
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var stats1 = new HealthBufferStats { TotalCount = 100 };
        var stats2 = new HealthBufferStats { TotalCount = 200 };

        // Assert
        Assert.False(stats1.Equals(stats2));
        Assert.False(stats1 == stats2);
        Assert.True(stats1 != stats2);
    }

    #endregion

    #region ToString 测试

    [Fact]
    public void ToString_CallsGetHealthDescription()
    {
        // Arrange
        var stats = new HealthBufferStats
        {
            TotalCount = 50,
            SuccessRate = 0.9,
            AverageResponseTimeMs = 100
        };

        // Act
        var result = stats.ToString();

        // Assert
        Assert.Equal(stats.GetHealthDescription(), result);
    }

    #endregion

    #region Helper Methods

    private CompactHealthMetrics CreateMetrics(bool isSuccessful, ushort responseTimeMs, long timestampMs)
    {
        return new CompactHealthMetrics
        {
            Flags = isSuccessful ? HealthCheckFlags.IsSuccessful : HealthCheckFlags.None,
            ResponseTimeMs = responseTimeMs,
            TimestampMs = timestampMs,
            StatusCode = isSuccessful ? (ushort)200 : (ushort)500
        };
    }

    #endregion
}
using Domain.Services;
using Domain.Services.HealthChecking;
using Xunit;

namespace Tests.Unit.Domain.Services.HealthChecking;

/// <summary>
/// CompactHealthMetrics 单元测试
/// </summary>
public class CompactHealthMetricsTests
{
    #region 构造函数测试

    [Fact]
    public void Constructor_DefaultValues_SetsCorrectDefaults()
    {
        // Act
        var metrics = new CompactHealthMetrics();

        // Assert
        Assert.Equal(0, metrics.TimestampMs);
        Assert.Equal(0, metrics.ResponseTimeMs);
        Assert.Equal(0, metrics.StatusCode);
        Assert.Equal(HealthCheckFlags.None, metrics.Flags);
    }

    [Fact]
    public void Constructor_WithValues_SetsAllProperties()
    {
        // Arrange
        var TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ushort responseTime = 150;
        ushort statusCode = 200;
        var flags = HealthCheckFlags.IsSuccessful;

        // Act
        var metrics = new CompactHealthMetrics
        {
            TimestampMs = TimestampMs,
            ResponseTimeMs = responseTime,
            StatusCode = statusCode,
            Flags = flags
        };

        // Assert
        Assert.Equal(TimestampMs, metrics.TimestampMs);
        Assert.Equal(responseTime, metrics.ResponseTimeMs);
        Assert.Equal(statusCode, metrics.StatusCode);
        Assert.Equal(flags, metrics.Flags);
    }

    #endregion

    #region IsSuccessful 测试

    [Fact]
    public void IsSuccess_SuccessFlag_ReturnsTrue()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            Flags = HealthCheckFlags.IsSuccessful
        };

        // Act
        var result = metrics.IsSuccessful;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSuccess_NoSuccessFlag_ReturnsFalse()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            Flags = HealthCheckFlags.IsTimeout
        };

        // Act
        var result = metrics.IsSuccessful;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSuccess_SuccessWithOtherFlags_ReturnsTrue()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            Flags = HealthCheckFlags.IsSuccessful | HealthCheckFlags.Slow
        };

        // Act
        var result = metrics.IsSuccessful;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSuccess_NoneFlag_ReturnsFalse()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            Flags = HealthCheckFlags.None
        };

        // Act
        var result = metrics.IsSuccessful;

        // Assert
        Assert.False(result);
    }

    #endregion

    #region IsTimeout 测试

    [Fact]
    public void IsTimeout_TimeoutFlag_ReturnsTrue()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            Flags = HealthCheckFlags.IsTimeout
        };

        // Act
        var result = metrics.IsTimeout;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTimeout_NoTimeoutFlag_ReturnsFalse()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            Flags = HealthCheckFlags.IsSuccessful
        };

        // Act
        var result = metrics.IsTimeout;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsTimeout_TimeoutWithOtherFlags_ReturnsTrue()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            Flags = HealthCheckFlags.IsTimeout | HealthCheckFlags.IsNetworkError
        };

        // Act
        var result = metrics.IsTimeout;

        // Assert
        Assert.True(result);
    }

    #endregion

    #region IsNetworkError 测试

    [Fact]
    public void IsNetworkError_NetworkErrorFlag_ReturnsTrue()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            Flags = HealthCheckFlags.IsNetworkError
        };

        // Act
        var result = metrics.IsNetworkError;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsNetworkError_NoNetworkErrorFlag_ReturnsFalse()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            Flags = HealthCheckFlags.IsSuccessful
        };

        // Act
        var result = metrics.IsNetworkError;

        // Assert
        Assert.False(result);
    }

    #endregion



    #region 边界值测试

    [Fact]
    public void ResponseTimeMs_MaxValue_StoresCorrectly()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            ResponseTimeMs = ushort.MaxValue // 65535ms
        };

        // Act & Assert
        Assert.Equal(ushort.MaxValue, metrics.ResponseTimeMs);
    }

    [Fact]
    public void StatusCode_MaxValue_StoresCorrectly()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            StatusCode = ushort.MaxValue
        };

        // Act & Assert
        Assert.Equal(ushort.MaxValue, metrics.StatusCode);
    }

    [Fact]
    public void TimestampMs_MaxValue_StoresCorrectly()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            TimestampMs = long.MaxValue
        };

        // Act & Assert
        Assert.Equal(long.MaxValue, metrics.TimestampMs);
    }

    [Fact]
    public void TimestampMs_MinValue_StoresCorrectly()
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            TimestampMs = long.MinValue
        };

        // Act & Assert
        Assert.Equal(long.MinValue, metrics.TimestampMs);
    }

    #endregion

    #region HTTP状态码测试

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void StatusCode_CommonHttpCodes_StoresCorrectly(int statusCode)
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            StatusCode = (ushort)statusCode
        };

        // Act & Assert
        Assert.Equal((ushort)statusCode, metrics.StatusCode);
    }

    #endregion

    #region 响应时间测试

    [Theory]
    [InlineData(0)]      // 极快
    [InlineData(10)]     // 很快
    [InlineData(50)]     // 快
    [InlineData(100)]    // 正常
    [InlineData(500)]    // 慢
    [InlineData(1000)]   // 很慢
    [InlineData(5000)]   // 超慢
    public void ResponseTimeMs_VariousValues_StoresCorrectly(int responseTime)
    {
        // Arrange
        var metrics = new CompactHealthMetrics
        {
            ResponseTimeMs = (ushort)responseTime
        };

        // Act & Assert
        Assert.Equal((ushort)responseTime, metrics.ResponseTimeMs);
    }

    #endregion

    #region 时间戳测试

    [Fact]
    public void TimestampMs_RecentTime_StoresCorrectly()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var metrics = new CompactHealthMetrics
        {
            TimestampMs = now
        };

        // Act & Assert
        Assert.Equal(now, metrics.TimestampMs);
    }

    [Fact]
    public void TimestampMs_PastTime_StoresCorrectly()
    {
        // Arrange
        var past = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();
        var metrics = new CompactHealthMetrics
        {
            TimestampMs = past
        };

        // Act & Assert
        Assert.Equal(past, metrics.TimestampMs);
    }

    [Fact]
    public void TimestampMs_FutureTime_StoresCorrectly()
    {
        // Arrange
        var future = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var metrics = new CompactHealthMetrics
        {
            TimestampMs = future
        };

        // Act & Assert
        Assert.Equal(future, metrics.TimestampMs);
    }

    #endregion

    #region 实际场景测试

    [Fact]
    public void CreateMetrics_SuccessfulHealthCheck_CorrectValues()
    {
        // Arrange & Act
        var metrics = new CompactHealthMetrics
        {
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ResponseTimeMs = 120,
            StatusCode = 200,
            Flags = HealthCheckFlags.IsSuccessful
        };

        // Assert
        Assert.True(metrics.IsSuccessful);
        Assert.False(metrics.IsTimeout);
        Assert.False(metrics.IsNetworkError);
        Assert.Equal(200, metrics.StatusCode);
        Assert.Equal(120, metrics.ResponseTimeMs);
    }

    [Fact]
    public void CreateMetrics_TimeoutHealthCheck_CorrectFlags()
    {
        // Arrange & Act
        var metrics = new CompactHealthMetrics
        {
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ResponseTimeMs = 5000,
            StatusCode = 0,
            Flags = HealthCheckFlags.IsTimeout
        };

        // Assert
        Assert.False(metrics.IsSuccessful);
        Assert.True(metrics.IsTimeout);
        Assert.False(metrics.IsNetworkError);
        Assert.Equal(0, metrics.StatusCode);
    }

    [Fact]
    public void CreateMetrics_NetworkErrorHealthCheck_CorrectFlags()
    {
        // Arrange & Act
        var metrics = new CompactHealthMetrics
        {
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ResponseTimeMs = 0,
            StatusCode = 0,
            Flags = HealthCheckFlags.IsNetworkError
        };

        // Assert
        Assert.False(metrics.IsSuccessful);
        Assert.False(metrics.IsTimeout);
        Assert.True(metrics.IsNetworkError);
    }

    [Fact]
    public void CreateMetrics_SlowButSuccessful_CorrectFlags()
    {
        // Arrange & Act
        var metrics = new CompactHealthMetrics
        {
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ResponseTimeMs = 3000,
            StatusCode = 200,
            Flags = HealthCheckFlags.IsSuccessful | HealthCheckFlags.Slow
        };

        // Assert
        Assert.True(metrics.IsSuccessful);
        Assert.False(metrics.IsTimeout);
        Assert.False(metrics.IsNetworkError);
    }

    #endregion

    #region 结构体大小验证

    [Fact]
    public void StructSize_Is13Bytes()
    {
        // Arrange & Act
        var size = System.Runtime.InteropServices.Marshal.SizeOf<CompactHealthMetrics>();

        // Assert
        Assert.Equal(13, size); // long(8) + ushort(2) + ushort(2) + byte(1) = 13
    }

    #endregion
}
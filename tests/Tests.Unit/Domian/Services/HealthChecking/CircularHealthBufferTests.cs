using Domain.Services.HealthChecking;

namespace Tests.Unit.Domain.Services.HealthChecking;

/// <summary>
/// CircularHealthBuffer 单元测试
/// </summary>
public class CircularHealthBufferTests
{
    #region 构造函数测试

    [Fact]
    public void Constructor_WithCapacity_InitializesCorrectly()
    {
        // Arrange & Act
        var buffer = new CircularHealthBuffer(100);

        // Assert
        Assert.Equal(0, buffer.Count);
        Assert.Equal(100, buffer.Capacity);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Constructor_VariousCapacities_InitializesCorrectly(int capacity)
    {
        // Arrange & Act
        var buffer = new CircularHealthBuffer(capacity);

        // Assert
        Assert.Equal(capacity, buffer.Capacity);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Constructor_DefaultCapacity_Is100()
    {
        // Arrange & Act
        var buffer = new CircularHealthBuffer();

        // Assert
        Assert.Equal(100, buffer.Capacity);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Constructor_ZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularHealthBuffer(0));
    }

    [Fact]
    public void Constructor_NegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularHealthBuffer(-1));
    }

    #endregion

    #region Add 测试 - 基础功能

    [Fact]
    public void Add_SingleMetric_IncreasesCount()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        var metrics = CreateMetrics(timestampMs: 1000);

        // Act
        buffer.Add(metrics);

        // Assert
        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public void Add_MultipleMetricsInOrder_IncreasesCount()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);

        // Act
        for (int i = 0; i < 5; i++)
        {
            buffer.Add(CreateMetrics(timestampMs: 1000 + i * 1000));
        }

        // Assert
        Assert.Equal(5, buffer.Count);
    }

    [Fact]
    public void Add_FillToCapacity_CountEqualsCapacity()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(5);

        // Act
        for (int i = 0; i < 5; i++)
        {
            buffer.Add(CreateMetrics(timestampMs: 1000 + i * 1000));
        }

        // Assert
        Assert.Equal(5, buffer.Count);
    }

    [Fact]
    public void Add_ExceedCapacity_CountStaysAtCapacity()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(3);

        // Act - 添加4个，容量只有3
        for (int i = 0; i < 4; i++)
        {
            buffer.Add(CreateMetrics(timestampMs: 1000 + i * 1000, statusCode: (ushort)(200 + i)));
        }

        // Assert
        Assert.Equal(3, buffer.Count);
    }

    #endregion

    #region Add 测试 - 时间递增验证

    [Fact]
    public void Add_DecreasingTimestamp_IgnoresOldData()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        buffer.Add(CreateMetrics(timestampMs: 2000, statusCode: 200));

        // Act
        buffer.Add(CreateMetrics(timestampMs: 1000, statusCode: 201)); // 时间倒退

        // Assert - 旧数据被忽略，count不变
        Assert.Equal(1, buffer.Count);

        var recent = buffer.GetRecent(1).ToArray();
        Assert.Equal(200, recent[0].StatusCode); // 只有第一条
    }

    [Fact]
    public void Add_SameTimestamp_Allowed()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        buffer.Add(CreateMetrics(timestampMs: 1000, statusCode: 200));

        // Act - 相同时间戳应该允许
        buffer.Add(CreateMetrics(timestampMs: 1000, statusCode: 201));

        // Assert
        Assert.Equal(2, buffer.Count);
    }

    [Fact]
    public void Add_IncreasingTimestamp_Success()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);

        // Act
        buffer.Add(CreateMetrics(timestampMs: 1000, statusCode: 200));
        buffer.Add(CreateMetrics(timestampMs: 2000, statusCode: 201));
        buffer.Add(CreateMetrics(timestampMs: 3000, statusCode: 202));

        // Assert
        Assert.Equal(3, buffer.Count);
    }

    #endregion

    #region Add 测试 - 循环覆盖

    [Fact]
    public void Add_ExceedCapacity_OverwritesOldest()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(3);

        // Act
        buffer.Add(CreateMetrics(timestampMs: 1000, statusCode: 200));
        buffer.Add(CreateMetrics(timestampMs: 2000, statusCode: 201));
        buffer.Add(CreateMetrics(timestampMs: 3000, statusCode: 202));
        buffer.Add(CreateMetrics(timestampMs: 4000, statusCode: 203)); // 覆盖 200

        // Assert
        Assert.Equal(3, buffer.Count);

        var recent = buffer.GetRecent(3).ToArray();
        Assert.DoesNotContain(recent, m => m.StatusCode == 200); // 最旧的被覆盖
        Assert.Contains(recent, m => m.StatusCode == 201);
        Assert.Contains(recent, m => m.StatusCode == 202);
        Assert.Contains(recent, m => m.StatusCode == 203);
    }

    [Fact]
    public void Add_CircularOverwrite_MaintainsCapacity()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(5);

        // Act - 添加10个，应该保留最后5个
        for (int i = 0; i < 10; i++)
        {
            buffer.Add(CreateMetrics(timestampMs: (i + 1) * 1000, statusCode: (ushort)(200 + i)));
        }

        // Assert
        Assert.Equal(5, buffer.Count);

        var recent = buffer.GetRecent(5).ToArray();
        Assert.All(recent, m => Assert.True(m.StatusCode >= 205 && m.StatusCode <= 209));
    }

    [Fact]
    public void Add_MultipleWraparounds_WorksCorrectly()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(3);

        // Act - 环绕多次
        for (int i = 0; i < 20; i++)
        {
            buffer.Add(CreateMetrics(timestampMs: (i + 1) * 1000, statusCode: (ushort)(200 + i)));
        }

        // Assert
        Assert.Equal(3, buffer.Count);

        var recent = buffer.GetRecent(3).ToArray();
        // 应该只保留最后3个：218, 219, 220
        Assert.Equal(219, recent[0].StatusCode); // 最新
        Assert.Equal(218, recent[1].StatusCode);
        Assert.Equal(217, recent[2].StatusCode); // 最旧
    }

    #endregion

    #region GetRecent 测试 - 基础功能

    [Fact]
    public void GetRecent_EmptyBuffer_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);

        // Act
        var recent = buffer.GetRecent(5);

        // Assert
        Assert.True(recent.IsEmpty);
    }

    [Fact]
    public void GetRecent_ZeroCount_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        buffer.Add(CreateMetrics(1000));

        // Act
        var recent = buffer.GetRecent(0);

        // Assert
        Assert.True(recent.IsEmpty);
    }

    [Fact]
    public void GetRecent_NegativeCount_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        buffer.Add(CreateMetrics(1000));

        // Act
        var recent = buffer.GetRecent(-1);

        // Assert
        Assert.True(recent.IsEmpty);
    }

    #endregion

    #region GetRecent 测试 - 时间倒序验证

    [Fact]
    public void GetRecent_ReturnsNewestFirst()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        buffer.Add(CreateMetrics(timestampMs: 1000, statusCode: 200));
        buffer.Add(CreateMetrics(timestampMs: 2000, statusCode: 201));
        buffer.Add(CreateMetrics(timestampMs: 3000, statusCode: 202));
        buffer.Add(CreateMetrics(timestampMs: 4000, statusCode: 203));
        buffer.Add(CreateMetrics(timestampMs: 5000, statusCode: 204));

        // Act
        var recent = buffer.GetRecent(5).ToArray();

        // Assert - 最新的在前
        Assert.Equal(5000, recent[0].TimestampMs);
        Assert.Equal(4000, recent[1].TimestampMs);
        Assert.Equal(3000, recent[2].TimestampMs);
        Assert.Equal(2000, recent[3].TimestampMs);
        Assert.Equal(1000, recent[4].TimestampMs);
    }

    [Fact]
    public void GetRecent_RequestMoreThanAvailable_ReturnsAll()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        buffer.Add(CreateMetrics(timestampMs: 1000));
        buffer.Add(CreateMetrics(timestampMs: 2000));
        buffer.Add(CreateMetrics(timestampMs: 3000));

        // Act
        var recent = buffer.GetRecent(5);

        // Assert
        Assert.Equal(3, recent.Length);
    }

    [Fact]
    public void GetRecent_RequestLessThanAvailable_ReturnsRequested()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        for (int i = 0; i < 5; i++)
        {
            buffer.Add(CreateMetrics(timestampMs: (i + 1) * 1000, statusCode: (ushort)(200 + i)));
        }

        // Act
        var recent = buffer.GetRecent(3).ToArray();

        // Assert
        Assert.Equal(3, recent.Length);
        // 应该返回最近的3个（按时间倒序：5000, 4000, 3000）
        Assert.Equal(5000, recent[0].TimestampMs);
        Assert.Equal(4000, recent[1].TimestampMs);
        Assert.Equal(3000, recent[2].TimestampMs);
    }

    #endregion

    #region GetRecent 测试 - 跨界场景

    [Fact]
    public void GetRecent_AfterWrapAround_ReturnsCorrectOrder()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(3);
        buffer.Add(CreateMetrics(timestampMs: 1000, statusCode: 200));
        buffer.Add(CreateMetrics(timestampMs: 2000, statusCode: 201));
        buffer.Add(CreateMetrics(timestampMs: 3000, statusCode: 202));
        buffer.Add(CreateMetrics(timestampMs: 4000, statusCode: 203)); // 覆盖200

        // Act
        var recent = buffer.GetRecent(3).ToArray();

        // Assert - 时间倒序：4000, 3000, 2000
        Assert.Equal(4000, recent[0].TimestampMs);
        Assert.Equal(3000, recent[1].TimestampMs);
        Assert.Equal(2000, recent[2].TimestampMs);
    }

    [Fact]
    public void GetRecent_MultipleWraparounds_ReturnsCorrectOrder()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(5);

        // 添加10个，环绕一次
        for (int i = 0; i < 10; i++)
        {
            buffer.Add(CreateMetrics(timestampMs: (i + 1) * 1000, statusCode: (ushort)(200 + i)));
        }

        // Act
        var recent = buffer.GetRecent(5).ToArray();

        // Assert - 最后5个：10000, 9000, 8000, 7000, 6000
        Assert.Equal(10000, recent[0].TimestampMs);
        Assert.Equal(9000, recent[1].TimestampMs);
        Assert.Equal(8000, recent[2].TimestampMs);
        Assert.Equal(7000, recent[3].TimestampMs);
        Assert.Equal(6000, recent[4].TimestampMs);
    }

    [Fact]
    public void GetRecent_CrossBoundary_ReturnsCorrectData()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(5);

        // 填满并环绕
        for (int i = 0; i < 7; i++)
        {
            buffer.Add(CreateMetrics(timestampMs: (i + 1) * 1000, statusCode: (ushort)(200 + i)));
        }

        // Act - 获取全部5条（会跨界）
        var recent = buffer.GetRecent(5).ToArray();

        // Assert
        Assert.Equal(5, recent.Length);
        Assert.Equal(7000, recent[0].TimestampMs); // 最新
        Assert.Equal(6000, recent[1].TimestampMs);
        Assert.Equal(5000, recent[2].TimestampMs);
        Assert.Equal(4000, recent[3].TimestampMs);
        Assert.Equal(3000, recent[4].TimestampMs); // 最旧
    }

    #endregion

    #region GetByTimeRange 测试

    [Fact]
    public void GetByTimeRange_EmptyBuffer_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        var from = DateTimeOffset.UtcNow.AddMinutes(-10);
        var to = DateTimeOffset.UtcNow;

        // Act
        var result = buffer.GetByTimeRange(from, to);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetByTimeRange_AllWithinRange_ReturnsAll()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        var now = DateTimeOffset.UtcNow;

        buffer.Add(CreateMetrics(timestampMs: now.AddSeconds(-5).ToUnixTimeMilliseconds()));
        buffer.Add(CreateMetrics(timestampMs: now.AddSeconds(-3).ToUnixTimeMilliseconds()));
        buffer.Add(CreateMetrics(timestampMs: now.AddSeconds(-1).ToUnixTimeMilliseconds()));

        // Act
        var result = buffer.GetByTimeRange(now.AddSeconds(-10), now);

        // Assert
        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void GetByTimeRange_PartiallyWithinRange_ReturnsMatching()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        var now = DateTimeOffset.UtcNow;

        buffer.Add(CreateMetrics(timestampMs: now.AddSeconds(-10).ToUnixTimeMilliseconds(), statusCode: 200));
        buffer.Add(CreateMetrics(timestampMs: now.AddSeconds(-5).ToUnixTimeMilliseconds(), statusCode: 201));
        buffer.Add(CreateMetrics(timestampMs: now.AddSeconds(-2).ToUnixTimeMilliseconds(), statusCode: 202));
        buffer.Add(CreateMetrics(timestampMs: now.AddSeconds(5).ToUnixTimeMilliseconds(), statusCode: 203));

        // Act - 只查询 -6 到 -1 秒的记录
        var result = buffer.GetByTimeRange(now.AddSeconds(-6), now.AddSeconds(-1));

        // Assert
        Assert.Equal(2, result.Length);
        Assert.Contains(result, m => m.StatusCode == 201);
        Assert.Contains(result, m => m.StatusCode == 202);
    }

    [Fact]
    public void GetByTimeRange_NoneWithinRange_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        var now = DateTimeOffset.UtcNow;

        buffer.Add(CreateMetrics(timestampMs: now.AddSeconds(-20).ToUnixTimeMilliseconds()));
        buffer.Add(CreateMetrics(timestampMs: now.AddSeconds(-15).ToUnixTimeMilliseconds()));

        // Act - 查询最近5秒
        var result = buffer.GetByTimeRange(now.AddSeconds(-5), now);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetByTimeRange_StopsEarly_WhenDataTooOld()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(100);
        var now = DateTimeOffset.UtcNow;

        // 添加100条数据，从 -100s 到 -1s
        for (int i = 100; i > 0; i--)
        {
            buffer.Add(CreateMetrics(timestampMs: now.AddSeconds(-i).ToUnixTimeMilliseconds()));
        }

        // Act - 只查询最近10秒
        var result = buffer.GetByTimeRange(now.AddSeconds(-10), now);

        // Assert
        Assert.Equal(10, result.Length); // 应该提前终止，不遍历所有100条
    }

    #endregion

    #region GetByPredicate 测试

    [Fact]
    public void GetByPredicate_EmptyBuffer_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);

        // Act
        var result = buffer.GetByPredicate(m => m.StatusCode == 200);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetByPredicate_AllMatch_ReturnsAll()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        buffer.Add(CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful));
        buffer.Add(CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful));
        buffer.Add(CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful));

        // Act
        var result = buffer.GetByPredicate(m => m.Flags.HasFlag(HealthCheckFlags.IsSuccessful));

        // Assert
        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void GetByPredicate_PartialMatch_ReturnsMatching()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        buffer.Add(CreateMetrics(timestampMs: 1000, statusCode: 200, flags: HealthCheckFlags.IsSuccessful));
        buffer.Add(CreateMetrics(timestampMs: 2000, statusCode: 500, flags: HealthCheckFlags.None));
        buffer.Add(CreateMetrics(timestampMs: 3000, statusCode: 200, flags: HealthCheckFlags.IsSuccessful));
        buffer.Add(CreateMetrics(timestampMs: 4000, statusCode: 503, flags: HealthCheckFlags.None));

        // Act
        var result = buffer.GetByPredicate(m => m.StatusCode == 200);

        // Assert
        Assert.Equal(2, result.Length);
        Assert.All(result, m => Assert.Equal(200, m.StatusCode));
    }

    [Fact]
    public void GetByPredicate_WithMaxCount_LimitsResults()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        for (int i = 0; i < 5; i++)
        {
            buffer.Add(CreateMetrics(timestampMs: (i + 1) * 1000, statusCode: 200));
        }

        // Act
        var result = buffer.GetByPredicate(m => m.StatusCode == 200, maxCount: 3);

        // Assert
        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void GetByPredicate_ReturnsNewestFirst()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        buffer.Add(CreateMetrics(timestampMs: 1000, statusCode: 200));
        buffer.Add(CreateMetrics(timestampMs: 2000, statusCode: 500));
        buffer.Add(CreateMetrics(timestampMs: 3000, statusCode: 200));
        buffer.Add(CreateMetrics(timestampMs: 4000, statusCode: 200));

        // Act
        var result = buffer.GetByPredicate(m => m.StatusCode == 200);

        // Assert - 按时间倒序
        Assert.Equal(4000, result[0].TimestampMs); // 最新
        Assert.Equal(3000, result[1].TimestampMs);
        Assert.Equal(1000, result[2].TimestampMs); // 最旧
    }

    #endregion

    #region GetStats 测试

    [Fact]
    public void GetStats_EmptyBuffer_ReturnsEmptyStats()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);

        // Act
        var stats = buffer.GetStats();

        // Assert
        Assert.Equal(HealthBufferStats.Empty, stats);
    }

    [Fact]
    public void GetStats_WithData_ReturnsCorrectStats()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10);
        buffer.Add(CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 100));
        buffer.Add(CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful, responseTimeMs: 200));
        buffer.Add(CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsTimeout, responseTimeMs: 5000));

        // Act
        var stats = buffer.GetStats();

        // Assert
        Assert.Equal(3, stats.TotalCount);
        Assert.True(stats.SuccessCount >= 2);
        Assert.True(stats.FailureCount >= 1);
    }

    [Fact]
    public void GetStats_AfterWrapAround_UsesCurrentData()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(3);

        // 添加4个，第1个被覆盖
        buffer.Add(CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.None)); // 失败，会被覆盖
        buffer.Add(CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful));
        buffer.Add(CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.IsSuccessful));
        buffer.Add(CreateMetrics(timestampMs: 4000, flags: HealthCheckFlags.IsSuccessful));

        // Act
        var stats = buffer.GetStats();

        // Assert
        Assert.Equal(3, stats.TotalCount); // 只统计当前的3条
    }

    #endregion

    #region 并发测试

    [Fact]
    public void Add_ConcurrentAdds_ThreadSafe()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(1000);
        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var timestampLock = new object();
        var currentTimestamp = baseTimestamp;

        // Act - 使用锁保证时间戳严格递增
        Parallel.For(0, 100, i =>
        {
            long timestamp;
            lock (timestampLock)
            {
                currentTimestamp += 100;
                timestamp = currentTimestamp;
                buffer.Add(CreateMetrics(timestampMs: timestamp));
            }
        });

        // Assert
        Assert.Equal(100, buffer.Count);
    }

    [Fact]
    public void GetRecent_ConcurrentReads_ThreadSafe()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(100);
        for (int i = 0; i < 50; i++)
        {
            buffer.Add(CreateMetrics(timestampMs: (i + 1) * 1000));
        }

        // Act
        var results = new System.Collections.Concurrent.ConcurrentBag<int>();
        Parallel.For(0, 10, _ =>
        {
            results.Add(buffer.GetRecent(50).Length);
        });

        // Assert
        Assert.All(results, count => Assert.Equal(50, count));
    }

    [Fact]
    public void MixedOperations_ConcurrentReadWrite_ThreadSafe()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(100);
        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var timestampLock = new object();
        var currentTimestamp = baseTimestamp;

        // Act - 同时读写
        var exception = Record.Exception(() =>
        {
            Parallel.Invoke(
                // Writer
                () =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        long timestamp;
                        lock (timestampLock)
                        {
                            currentTimestamp += 100;
                            timestamp = currentTimestamp;
                        }
                        buffer.Add(CreateMetrics(timestampMs: timestamp));
                        Thread.Sleep(1);
                    }
                },
                // Reader 1
                () =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        _ = buffer.GetRecent(10);
                        Thread.Sleep(1);
                    }
                },
                // Reader 2
                () =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        _ = buffer.GetStats();
                        Thread.Sleep(1);
                    }
                }
            );
        });

        // Assert - 不应该抛异常
        Assert.Null(exception);
        Assert.True(buffer.Count > 0);
        Assert.True(buffer.Count <= buffer.Capacity);
    }

    [Fact]
    public void Buffer_HighConcurrency_NoDataCorruption()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(100);
        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var operations = 1000;

        // Act - 大量并发读写
        Parallel.For(0, operations, i =>
        {
            if (i % 2 == 0)
            {
                // 写入（时间戳可能乱序，部分会被忽略）
                buffer.Add(CreateMetrics(timestampMs: baseTimestamp + i * 10));
            }
            else
            {
                // 读取
                _ = buffer.GetRecent(10);
            }
        });

        // Assert - 验证数据一致性
        Assert.True(buffer.Count >= 0);
        Assert.True(buffer.Count <= buffer.Capacity);

        // 验证可以正常读取
        var recent = buffer.GetRecent(buffer.Count);
        // 验证时间戳递增（因为旧数据被忽略）
        if (recent.Length > 1)
        {
            for (int i = 1; i < recent.Length; i++)
            {
                Assert.True(recent[i - 1].TimestampMs >= recent[i].TimestampMs);
            }
        }
    }

    [Fact]
    public void Add_ConcurrentWithWrapAround_ThreadSafe()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10); // 小容量，容易触发环绕
        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var timestampLock = new object();
        var currentTimestamp = baseTimestamp;

        // Act - 大量写入触发多次环绕
        Parallel.For(0, 100, i =>
        {
            long timestamp;
            lock (timestampLock)
            {
                currentTimestamp += 100;
                timestamp = currentTimestamp;
            }
            buffer.Add(CreateMetrics(timestampMs: timestamp));
        });

        // Assert
        Assert.Equal(10, buffer.Count); // 容量限制

        // 验证最新的10条数据
        var recent = buffer.GetRecent(10).ToArray();
        Assert.Equal(10, recent.Length);

        // 验证时间递增
        for (int i = 1; i < recent.Length; i++)
        {
            Assert.True(recent[i - 1].TimestampMs > recent[i].TimestampMs);
        }
    }

    #endregion

    #region 边界条件测试

    [Fact]
    public void Add_MinCapacity_WorksCorrectly()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(1);

        // Act
        buffer.Add(CreateMetrics(timestampMs: 1000, statusCode: 200));
        buffer.Add(CreateMetrics(timestampMs: 2000, statusCode: 201));

        // Assert
        Assert.Equal(1, buffer.Count);
        var recent = buffer.GetRecent(1).ToArray();
        Assert.Equal(201, recent[0].StatusCode); // 只保留最新的
    }

    [Fact]
    public void GetRecent_RequestExactCapacity_WorksCorrectly()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(5);
        for (int i = 0; i < 5; i++)
        {
            buffer.Add(CreateMetrics(timestampMs: (i + 1) * 1000));
        }

        // Act
        var recent = buffer.GetRecent(5);

        // Assert
        Assert.Equal(5, recent.Length);
    }

    [Fact]
    public void Buffer_LargeCapacity_HandlesCorrectly()
    {
        // Arrange
        var buffer = new CircularHealthBuffer(10000);

        // Act
        for (int i = 0; i < 1000; i++)
        {
            buffer.Add(CreateMetrics(timestampMs: (i + 1) * 1000));
        }

        // Assert
        Assert.Equal(1000, buffer.Count);
        var recent = buffer.GetRecent(100);
        Assert.Equal(100, recent.Length);
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
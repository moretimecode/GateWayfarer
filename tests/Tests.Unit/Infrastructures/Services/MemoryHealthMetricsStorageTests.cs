using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;
using Infrastructure.Services;
using Xunit;

namespace Tests.Unit.Infrastructure.Services;

/// <summary>
/// MemoryHealthMetricsStorage 单元测试
/// 基于内存的健康指标存储实现
/// </summary>
public class MemoryHealthMetricsStorageTests : IDisposable
{
    private readonly MemoryHealthMetricsStorage _storage;
    private const string DestinationCode1 = "service-1";
    private const string DestinationCode2 = "service-2";
    private const string DestinationCode3 = "service-3";

    public MemoryHealthMetricsStorageTests()
    {
        var destinations = new[] { DestinationCode1, DestinationCode2, DestinationCode3 };
        _storage = new MemoryHealthMetricsStorage(destinations, bufferCapacity: 100);
    }

    #region 构造函数测试

    [Fact]
    public void Constructor_ValidDestinations_CreatesStorage()
    {
        var storage = new MemoryHealthMetricsStorage(
            ["service-1", "service-2"],
            bufferCapacity: 50
        );

        var codes = storage.GetAllDestinationCodes();
        Assert.Equal(2, codes.Length);
        Assert.Contains("service-1", codes);
        Assert.Contains("service-2", codes);

        storage.Dispose();
    }

    [Fact]
    public void Constructor_NullDestinations_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MemoryHealthMetricsStorage(null!, bufferCapacity: 50)
        );
    }

    [Fact]
    public void Constructor_InvalidBufferCapacity_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MemoryHealthMetricsStorage(new[] { "service-1" }, bufferCapacity: 0)
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MemoryHealthMetricsStorage(new[] { "service-1" }, bufferCapacity: -1)
        );
    }

    [Fact]
    public void Constructor_EmptyDestinations_CreatesEmptyStorage()
    {
        var storage = new MemoryHealthMetricsStorage([], bufferCapacity: 50);
        var codes = storage.GetAllDestinationCodes();
        Assert.Empty(codes);
        storage.Dispose();
    }

    [Fact]
    public void Constructor_WhitespaceDestinations_IgnoredDuringInit()
    {
        var storage = new MemoryHealthMetricsStorage(
            ["service-1", "  ", "", "service-2"],
            bufferCapacity: 50
        );

        var codes = storage.GetAllDestinationCodes();
        Assert.Equal(2, codes.Length);
        Assert.Contains("service-1", codes);
        Assert.Contains("service-2", codes);

        storage.Dispose();
    }

    #endregion

    #region StoreMetrics 测试

    [Fact]
    public void StoreMetrics_ValidMetrics_StoresSuccessfully()
    {
        var metrics = CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful);
        _storage.StoreMetrics(DestinationCode1, metrics);

        var stats = _storage.GetStats(DestinationCode1);
        Assert.Equal(1, stats.TotalCount);
    }

    [Fact]
    public void StoreMetrics_MultipleMetrics_StoresAll()
    {
        var metrics1 = CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful);
        var metrics2 = CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful);
        var metrics3 = CreateMetrics(timestampMs: 3000, flags: HealthCheckFlags.None, statusCode: 500);

        _storage.StoreMetrics(DestinationCode1, metrics1);
        _storage.StoreMetrics(DestinationCode1, metrics2);
        _storage.StoreMetrics(DestinationCode1, metrics3);

        var stats = _storage.GetStats(DestinationCode1);
        Assert.Equal(3, stats.TotalCount);
    }

    [Fact]
    public void StoreMetrics_DifferentDestinations_IsolatedStorage()
    {
        var metrics1 = CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful);
        var metrics2 = CreateMetrics(timestampMs: 2000, flags: HealthCheckFlags.IsSuccessful);

        _storage.StoreMetrics(DestinationCode1, metrics1);
        _storage.StoreMetrics(DestinationCode2, metrics2);

        var stats1 = _storage.GetStats(DestinationCode1);
        var stats2 = _storage.GetStats(DestinationCode2);
        Assert.Equal(1, stats1.TotalCount);
        Assert.Equal(1, stats2.TotalCount);
    }

    [Fact]
    public void StoreMetrics_NullOrWhitespaceDestinationCode_IgnoredSilently()
    {
        _storage.StoreMetrics(null!, CreateMetrics(1000));
        _storage.StoreMetrics("", CreateMetrics(1000));
        _storage.StoreMetrics("  ", CreateMetrics(1000));

        var stats = _storage.GetStats(DestinationCode1);
        Assert.Equal(0, stats.TotalCount);
    }

    [Fact]
    public void StoreMetrics_UnregisteredDestination_IgnoredSilently()
    {
        var metrics = CreateMetrics(timestampMs: 1000, flags: HealthCheckFlags.IsSuccessful);
        _storage.StoreMetrics("unregistered-service", metrics);

        var stats = _storage.GetStats("unregistered-service");
        Assert.Equal(HealthBufferStats.Empty, stats);
    }

    [Fact]
    public void StoreMetrics_ExceedsBufferCapacity_CircularOverwrite()
    {
        var storage = new MemoryHealthMetricsStorage(["service"], bufferCapacity: 10);

        for (int i = 0; i < 12; i++)
        {
            storage.StoreMetrics("service", CreateMetrics(timestampMs: (i + 1) * 1000));
        }

        var stats = storage.GetStats("service");
        Assert.Equal(10, stats.TotalCount);

        storage.Dispose();
    }

    #endregion

    #region GetRecentMetrics 测试

    [Fact]
    public void GetRecentMetrics_ValidRequest_ReturnsMetrics()
    {
        var metrics1 = CreateMetrics(timestampMs: 1000);
        var metrics2 = CreateMetrics(timestampMs: 2000);
        var metrics3 = CreateMetrics(timestampMs: 3000);
        _storage.StoreMetrics(DestinationCode1, metrics1);
        _storage.StoreMetrics(DestinationCode1, metrics2);
        _storage.StoreMetrics(DestinationCode1, metrics3);

        var recent = _storage.GetRecentMetrics(DestinationCode1, 2);

        Assert.Equal(2, recent.Length);
    }

    [Fact]
    public void GetRecentMetrics_CountExceedsAvailable_ReturnsAllAvailable()
    {
        _storage.StoreMetrics(DestinationCode1, CreateMetrics(1000));
        _storage.StoreMetrics(DestinationCode1, CreateMetrics(2000));

        var recent = _storage.GetRecentMetrics(DestinationCode1, 100);

        Assert.Equal(2, recent.Length);
    }

    [Fact]
    public void GetRecentMetrics_EmptyStorage_ReturnsEmpty()
    {
        var recent = _storage.GetRecentMetrics(DestinationCode1, 10);

        Assert.Equal(0, recent.Length);
    }

    [Fact]
    public void GetRecentMetrics_NullOrWhitespaceCode_ReturnsEmpty()
    {
        Assert.Equal(0, _storage.GetRecentMetrics(null!, 10).Length);
        Assert.Equal(0, _storage.GetRecentMetrics("", 10).Length);
        Assert.Equal(0, _storage.GetRecentMetrics("  ", 10).Length);
    }

    [Fact]
    public void GetRecentMetrics_InvalidCount_ReturnsEmpty()
    {
        _storage.StoreMetrics(DestinationCode1, CreateMetrics(1000));

        Assert.Equal(0, _storage.GetRecentMetrics(DestinationCode1, 0).Length);
        Assert.Equal(0, _storage.GetRecentMetrics(DestinationCode1, -1).Length);
    }

    [Fact]
    public void GetRecentMetrics_UnregisteredDestination_ReturnsEmpty()
    {
        var recent = _storage.GetRecentMetrics("unregistered", 10);

        Assert.Equal(0, recent.Length);
    }

    #endregion

    #region HasSufficientData 测试

    [Fact]
    public void HasSufficientData_EnoughRecords_ReturnsTrue()
    {
        for (int i = 0; i < 10; i++)
        {
            _storage.StoreMetrics(DestinationCode1, CreateMetrics((i + 1) * 1000));
        }

        Assert.True(_storage.HasSufficientData(DestinationCode1, 5));
        Assert.True(_storage.HasSufficientData(DestinationCode1, 10));
    }

    [Fact]
    public void HasSufficientData_NotEnoughRecords_ReturnsFalse()
    {
        for (int i = 0; i < 5; i++)
        {
            _storage.StoreMetrics(DestinationCode1, CreateMetrics((i + 1) * 1000));
        }

        Assert.False(_storage.HasSufficientData(DestinationCode1, 10));
    }

    [Fact]
    public void HasSufficientData_ExactlyEnough_ReturnsTrue()
    {
        for (int i = 0; i < 5; i++)
        {
            _storage.StoreMetrics(DestinationCode1, CreateMetrics((i + 1) * 1000));
        }

        Assert.True(_storage.HasSufficientData(DestinationCode1, 5));
    }

    [Fact]
    public void HasSufficientData_EmptyStorage_ReturnsFalse()
    {
        Assert.False(_storage.HasSufficientData(DestinationCode1, 1));
    }

    [Fact]
    public void HasSufficientData_InvalidRequiredDataPoints_ReturnsFalse()
    {
        _storage.StoreMetrics(DestinationCode1, CreateMetrics(1000));

        Assert.False(_storage.HasSufficientData(DestinationCode1, 0));
        Assert.False(_storage.HasSufficientData(DestinationCode1, -1));
    }

    [Fact]
    public void HasSufficientData_NullOrWhitespaceCode_ReturnsFalse()
    {
        Assert.False(_storage.HasSufficientData(null!, 10));
        Assert.False(_storage.HasSufficientData("", 10));
        Assert.False(_storage.HasSufficientData("  ", 10));
    }

    #endregion

    #region GetStats 测试

    [Fact]
    public void GetStats_ExistingDestination_ReturnsStats()
    {
        _storage.StoreMetrics(DestinationCode1, CreateMetrics(1000, flags: HealthCheckFlags.IsSuccessful));
        _storage.StoreMetrics(DestinationCode1, CreateMetrics(2000, flags: HealthCheckFlags.IsSuccessful));

        var stats = _storage.GetStats(DestinationCode1);

        Assert.NotEqual(HealthBufferStats.Empty, stats);
        Assert.Equal(2, stats.TotalCount);
    }

    [Fact]
    public void GetStats_EmptyDestination_ReturnsEmpty()
    {
        var stats = _storage.GetStats(DestinationCode1);

        Assert.Equal(HealthBufferStats.Empty, stats);
    }

    [Fact]
    public void GetStats_UnregisteredDestination_ReturnsEmpty()
    {
        var stats = _storage.GetStats("unregistered");

        Assert.Equal(HealthBufferStats.Empty, stats);
    }

    [Fact]
    public void GetStats_NullOrWhitespaceCode_ReturnsEmpty()
    {
        Assert.Equal(HealthBufferStats.Empty, _storage.GetStats(null!));
        Assert.Equal(HealthBufferStats.Empty, _storage.GetStats(""));
        Assert.Equal(HealthBufferStats.Empty, _storage.GetStats("  "));
    }

    #endregion

    #region AddDestination 测试

    [Fact]
    public void AddDestination_ValidCode_AddsSuccessfully()
    {
        var newCode = "new-service";

        _storage.AddDestination(newCode);

        var codes = _storage.GetAllDestinationCodes();
        Assert.Contains(newCode, codes);
    }

    [Fact]
    public void AddDestination_DuplicateCode_DoesNotDuplicate()
    {
        var code = "test-service";
        _storage.AddDestination(code);

        _storage.AddDestination(code);

        var codes = _storage.GetAllDestinationCodes();
        Assert.Single(codes.Where(c => c == code));
    }

    [Fact]
    public void AddDestination_NullOrWhitespace_IgnoredSilently()
    {
        var countBefore = _storage.GetAllDestinationCodes().Length;

        _storage.AddDestination(null!);
        _storage.AddDestination("");
        _storage.AddDestination("  ");

        var countAfter = _storage.GetAllDestinationCodes().Length;
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public void AddDestination_CanStoreMetricsAfterAdd()
    {
        var newCode = "new-service";
        _storage.AddDestination(newCode);

        _storage.StoreMetrics(newCode, CreateMetrics(1000));

        var stats = _storage.GetStats(newCode);
        Assert.Equal(1, stats.TotalCount);
    }

    #endregion

    #region RemoveDestination 测试

    [Fact]
    public void RemoveDestination_ExistingCode_RemovesSuccessfully()
    {
        var code = "to-remove";
        _storage.AddDestination(code);

        _storage.RemoveDestination(code);

        var codes = _storage.GetAllDestinationCodes();
        Assert.DoesNotContain(code, codes);
    }

    [Fact]
    public void RemoveDestination_NonExistentCode_NoError()
    {
        _storage.RemoveDestination("non-existent");
    }

    [Fact]
    public void RemoveDestination_NullOrWhitespace_IgnoredSilently()
    {
        var countBefore = _storage.GetAllDestinationCodes().Length;

        _storage.RemoveDestination(null!);
        _storage.RemoveDestination("");
        _storage.RemoveDestination("  ");

        var countAfter = _storage.GetAllDestinationCodes().Length;
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public void RemoveDestination_DataLost()
    {
        _storage.StoreMetrics(DestinationCode1, CreateMetrics(1000));
        Assert.Equal(1, _storage.GetStats(DestinationCode1).TotalCount);

        _storage.RemoveDestination(DestinationCode1);

        Assert.Equal(HealthBufferStats.Empty, _storage.GetStats(DestinationCode1));
    }

    #endregion

    #region UpdateDestinations 测试

    [Fact]
    public void UpdateDestinations_AddNewCodes_AddsSuccessfully()
    {
        var newCodes = new[] { "service-1", "service-2", "new-service-1", "new-service-2" };

        _storage.UpdateDestinations(newCodes);

        var codes = _storage.GetAllDestinationCodes();
        foreach (var newCode in new[] { "new-service-1", "new-service-2" })
        {
            Assert.Contains(newCode, codes);
        }
    }

    [Fact]
    public void UpdateDestinations_RemoveOldCodes_RemovesSuccessfully()
    {
        var newCodes = new[] { "service-1" };

        _storage.UpdateDestinations(newCodes);

        var codes = _storage.GetAllDestinationCodes();
        Assert.Contains("service-1", codes);
        Assert.DoesNotContain("service-2", codes);
        Assert.DoesNotContain("service-3", codes);
    }

    [Fact]
    public void UpdateDestinations_ReplacesAllCodes()
    {
        var newCodes = new[] { "new-1", "new-2", "new-3" };

        _storage.UpdateDestinations(newCodes);

        var codes = _storage.GetAllDestinationCodes();
        Assert.Equal(3, codes.Length);
        Assert.Contains("new-1", codes);
        Assert.Contains("new-2", codes);
        Assert.Contains("new-3", codes);
        Assert.DoesNotContain("service-1", codes);
    }

    [Fact]
    public void UpdateDestinations_WithWhitespace_IgnoredInUpdate()
    {
        var newCodes = new[] { "service-1", "  ", "", "service-2" };

        _storage.UpdateDestinations(newCodes);

        var codes = _storage.GetAllDestinationCodes();
        Assert.DoesNotContain("", codes);
        Assert.DoesNotContain("  ", codes);
    }

    [Fact]
    public void UpdateDestinations_NullEnumerable_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _storage.UpdateDestinations(null!));
    }

    #endregion

    #region GetAllStats 测试

    [Fact]
    public void GetAllStats_MultipleDestinations_ReturnsAllStats()
    {
        _storage.StoreMetrics(DestinationCode1, CreateMetrics(1000));
        _storage.StoreMetrics(DestinationCode2, CreateMetrics(2000));
        _storage.StoreMetrics(DestinationCode2, CreateMetrics(2100));

        var allStats = _storage.GetAllStats();

        Assert.Equal(3, allStats.Count);
        Assert.Equal(1, allStats[DestinationCode1].TotalCount);
        Assert.Equal(2, allStats[DestinationCode2].TotalCount);
    }

    [Fact]
    public void GetAllStats_EmptyStorage_ReturnsEmptyDict()
    {
        var emptyStorage = new MemoryHealthMetricsStorage(Array.Empty<string>(), bufferCapacity: 100);

        var allStats = emptyStorage.GetAllStats();

        Assert.Empty(allStats);

        emptyStorage.Dispose();
    }

    #endregion

    #region GetMemoryUsage 测试

    [Fact]
    public void GetMemoryUsage_ReturnsValidStats()
    {
        for (int i = 0; i < 5; i++)
        {
            _storage.StoreMetrics(DestinationCode1, CreateMetrics((i + 1) * 1000));
        }
        for (int i = 0; i < 3; i++)
        {
            _storage.StoreMetrics(DestinationCode2, CreateMetrics((i + 1) * 1000));
        }

        var memoryUsage = _storage.GetMemoryUsage();

        Assert.Equal(3, memoryUsage.TotalDestinations);
        Assert.Equal(8, memoryUsage.TotalRecords);
        Assert.Equal(300, memoryUsage.TotalCapacity);
        Assert.True(memoryUsage.MemoryUsageBytes > 0);
    }

    [Fact]
    public void GetMemoryUsage_EmptyStorage_ReturnsZero()
    {
        var memoryUsage = _storage.GetMemoryUsage();

        Assert.Equal(3, memoryUsage.TotalDestinations);
        Assert.Equal(0, memoryUsage.TotalRecords);
        Assert.Equal(0.0, memoryUsage.AverageRecordsPerDestination);
    }

    [Fact]
    public void GetMemoryUsageStats_GetDescription_ReturnsFormattedString()
    {
        var stats = new MemoryUsageStats
        {
            TotalDestinations = 5,
            TotalRecords = 100,
            TotalCapacity = 500,
            MemoryUsageBytes = 1024 * 1024,
            AverageRecordsPerDestination = 20.0
        };

        var description = stats.GetDescription();

        Assert.Contains("1.00MB", description);
        Assert.Contains("目标数: 5", description);
        Assert.Contains("记录数: 100", description);
    }

    [Fact]
    public void GetMemoryUsageStats_GetMemoryUsageMB_ConvertsCorrectly()
    {
        var stats = new MemoryUsageStats
        {
            MemoryUsageBytes = 1024 * 1024
        };

        var mb = stats.GetMemoryUsageMB();

        Assert.Equal(1.0, mb);
    }

    #endregion

    #region GetAllDestinationCodes 测试

    [Fact]
    public void GetAllDestinationCodes_ReturnsAllCodes()
    {
        var codes = _storage.GetAllDestinationCodes();

        Assert.Equal(3, codes.Length);
        Assert.Contains(DestinationCode1, codes);
        Assert.Contains(DestinationCode2, codes);
        Assert.Contains(DestinationCode3, codes);
    }

    [Fact]
    public void GetAllDestinationCodes_AfterRemoval_UpdatedList()
    {
        _storage.RemoveDestination(DestinationCode3);

        var codes = _storage.GetAllDestinationCodes();

        Assert.Equal(2, codes.Length);
        Assert.DoesNotContain(DestinationCode3, codes);
    }

    #endregion

    #region ClearAll 测试

    [Fact]
    public void ClearAll_RemovesAllData()
    {
        _storage.StoreMetrics(DestinationCode1, CreateMetrics(1000));
        _storage.StoreMetrics(DestinationCode2, CreateMetrics(2000));
        Assert.Equal(2, _storage.GetMemoryUsage().TotalRecords);

        _storage.ClearAll();

        Assert.Equal(0, _storage.GetMemoryUsage().TotalRecords);
        Assert.Equal(0, _storage.GetRecentMetrics(DestinationCode1, 10).Length);
        Assert.Equal(0, _storage.GetRecentMetrics(DestinationCode2, 10).Length);
    }

    #endregion

    #region 并发测试

    [Fact]
    public void ConcurrentWrites_MultipleThreads_ThreadSafe()
    {
        var threadCount = 10;
        var operationsPerThread = 100;
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        Parallel.For(0, threadCount, threadIndex =>
        {
            try
            {
                for (int i = 0; i < operationsPerThread; i++)
                {
                    var code = threadIndex % 3 == 0 ? DestinationCode1 :
                              threadIndex % 3 == 1 ? DestinationCode2 : DestinationCode3;
                    var metrics = CreateMetrics(timestampMs: (threadIndex * 10000L + i * 100));
                    _storage.StoreMetrics(code, metrics);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        Assert.Empty(exceptions);
        var memoryUsage = _storage.GetMemoryUsage();
        Assert.True(memoryUsage.TotalRecords > 0);
    }

    [Fact]
    public void ConcurrentReadWrite_MultipleThreads_ThreadSafe()
    {
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var cts = new System.Threading.CancellationTokenSource();

        var writerTask = Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 200 && !cts.Token.IsCancellationRequested; i++)
                {
                    _storage.StoreMetrics(DestinationCode1, CreateMetrics(i * 1000));
                    System.Threading.Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        var readerTasks = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 100 && !cts.Token.IsCancellationRequested; i++)
                {
                    _storage.GetRecentMetrics(DestinationCode1, 10);
                    _storage.GetStats(DestinationCode1);
                    _storage.GetMemoryUsage();
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        Task.WaitAll(new[] { writerTask }.Concat(readerTasks).ToArray());
        cts.Cancel();

        Assert.Empty(exceptions);
    }

    [Fact]
    public void ConcurrentAddRemoveDestinations_MultipleThreads_ThreadSafe()
    {
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        Parallel.For(0, 10, i =>
        {
            try
            {
                _storage.AddDestination($"service-{i}");
                _storage.StoreMetrics($"service-{i}", CreateMetrics(1000));
                _storage.RemoveDestination($"service-{i}");
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        Assert.Empty(exceptions);
    }

    #endregion

    #region 边界情况测试

    [Fact]
    public void LargeDataSet_1000Records_HandlesCorrectly()
    {
        var storage = new MemoryHealthMetricsStorage(
            new[] { "large-service" },
            bufferCapacity: 1000
        );

        for (int i = 0; i < 1000; i++)
        {
            storage.StoreMetrics("large-service", CreateMetrics((i + 1) * 1000));
        }

        var stats = storage.GetStats("large-service");
        Assert.Equal(1000, stats.TotalCount);
        var memoryUsage = storage.GetMemoryUsage();
        Assert.Equal(1000, memoryUsage.TotalRecords);

        storage.Dispose();
    }

    [Fact]
    public void VerySmallBufferCapacity_Works()
    {
        var storage = new MemoryHealthMetricsStorage(
            new[] { "small-buffer" },
            bufferCapacity: 1
        );

        storage.StoreMetrics("small-buffer", CreateMetrics(1000));
        storage.StoreMetrics("small-buffer", CreateMetrics(2000));
        storage.StoreMetrics("small-buffer", CreateMetrics(3000));

        var stats = storage.GetStats("small-buffer");
        Assert.Equal(1, stats.TotalCount);

        storage.Dispose();
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

    public void Dispose()
    {
        _storage?.Dispose();
    }
}
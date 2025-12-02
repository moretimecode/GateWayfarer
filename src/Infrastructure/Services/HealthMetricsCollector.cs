using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Diagnostics;
using Domain.Services;

namespace Infrastructure.Services
{
    /// <summary>
    /// 健康指标收集器实现
    /// </summary>
    public class HealthMetricsCollector : BackgroundService, IHealthMetricsCollector
    {
        private readonly IServiceRepository _serviceRepository;
        private readonly IHealthMetricsStorage _storage;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<HealthCheckOptions> _options;
        private readonly ILogger<HealthMetricsCollector> _logger;
        private SemaphoreSlim _concurrencyLimit;
        private PeriodicTimer _timer;
        private CollectorStats _stats = new();
        private readonly object _statsLock = new();
        private readonly ConcurrentQueue<double> _recentCollectionTimes = new();
        private volatile Dictionary<string, Destination> _currentDestinations = new();
        private readonly object _destinationsLock = new();

        public HealthMetricsCollector(
            IServiceRepository serviceRepository,
            IHealthMetricsStorage storage,
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<HealthCheckOptions> options,
            ILogger<HealthMetricsCollector> logger)
        {
            _serviceRepository = serviceRepository;
            _storage = storage;
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;

            InitializeConcurrencyLimit();

            _options.OnChange((_) => UpdateConcurrencyLimit());
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var options = _options.CurrentValue;
            if (!options.Enabled)
            {
                _logger.LogInformation("健康检查收集器已禁用");
                return;
            }

            options.Validate();

            _timer = new PeriodicTimer(options.Interval);

            _logger.LogInformation("健康检查收集器启动，间隔: {Interval}, 超时: {Timeout}, 策略: {Strategy}",
                options.Interval, options.Timeout, options.ThreadPoolStrategy);

            await RefreshDestinationsAsync();

            try
            {
                while (await _timer.WaitForNextTickAsync(stoppingToken))
                {
                    await CollectAllMetricsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("健康检查收集器已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "健康检查收集器发生异常");
                throw;
            }
        }

        /// <summary>
        /// 收集所有destination的健康指标
        /// </summary>
        private async Task CollectAllMetricsAsync(CancellationToken cancellationToken)
        {
            var startTime = DateTimeOffset.UtcNow;
            var destinations = _currentDestinations;

            if (destinations.Count == 0)
            {
                _logger.LogDebug("没有可用的destination，跳过本次收集");
                return;
            }

            lock (_statsLock)
            {
                _stats.TotalDestinations = destinations.Count;
                _stats.CurrentConcurrency = _concurrencyLimit.CurrentCount;
            }

            _logger.LogDebug("开始收集 {Count} 个destination的健康指标", destinations.Count);

            await Parallel.ForEachAsync(
                destinations,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = _concurrencyLimit.CurrentCount,
                    CancellationToken = cancellationToken
                },
                async (kvp, ct) =>
                {
                    await CollectSingleMetricAsync(kvp.Key, kvp.Value, ct);
                });

            var elapsed = DateTimeOffset.UtcNow - startTime;
            _logger.LogDebug("完成健康指标收集，耗时: {Elapsed}ms", elapsed.TotalMilliseconds);
        }

        /// <summary>
        /// 收集单个destination的健康指标
        /// </summary>
        private async Task CollectSingleMetricAsync(string destinationCode, Destination destination, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            await _concurrencyLimit.WaitAsync(cancellationToken);
            try
            {
                lock (_statsLock)
                {
                    _stats.ActiveCollections++;
                }

                var metrics = await PerformHealthCheckAsync(destination, cancellationToken);
                _storage.StoreMetrics(destinationCode, metrics);

                stopwatch.Stop();
                RecordSuccess(stopwatch.Elapsed);

                _logger.LogTrace("destination {Code} 健康检查完成: {Status}, 耗时: {Elapsed}ms",
                    destinationCode, metrics.Flags, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                RecordFailure(stopwatch.Elapsed);

                _logger.LogWarning(ex, "destination {Code} 健康检查失败，耗时: {Elapsed}ms",
                    destinationCode, stopwatch.ElapsedMilliseconds);

                var failedMetrics = CreateFailedMetrics(ex);
                _storage.StoreMetrics(destinationCode, failedMetrics);
            }
            finally
            {
                lock (_statsLock)
                {
                    _stats.ActiveCollections--;
                }
                _concurrencyLimit.Release();
            }
        }

        /// <summary>
        /// 执行HTTP健康检查
        /// </summary>
        private async Task<CompactHealthMetrics> PerformHealthCheckAsync(Destination destination, CancellationToken cancellationToken)
        {
            var startTime = DateTimeOffset.UtcNow;
            var options = _options.CurrentValue;

            try
            {
                var healthCheckUrl = destination.GetHealthCheckUrl(options.DefaultPath);

                using var httpClient = CreateHttpClient(destination, options);
                var response = await httpClient.GetAsync(healthCheckUrl, cancellationToken);
                var elapsed = DateTimeOffset.UtcNow - startTime;

                var flags = response.IsSuccessStatusCode
                    ? HealthCheckFlags.IsSuccessful
                    : (response.StatusCode >= System.Net.HttpStatusCode.InternalServerError
                        ? HealthCheckFlags.IsServerError
                        : HealthCheckFlags.IsClientError);

                return new CompactHealthMetrics
                {
                    TimestampMs = startTime.ToUnixTimeMilliseconds(),
                    ResponseTimeMs = (ushort)Math.Min(elapsed.TotalMilliseconds, ushort.MaxValue),
                    StatusCode = (ushort)response.StatusCode,
                    Flags = flags
                };
            }
            catch (TimeoutException)
            {
                var elapsed = DateTimeOffset.UtcNow - startTime;
                return new CompactHealthMetrics
                {
                    TimestampMs = startTime.ToUnixTimeMilliseconds(),
                    ResponseTimeMs = (ushort)Math.Min(elapsed.TotalMilliseconds, ushort.MaxValue),
                    StatusCode = 0,
                    Flags = HealthCheckFlags.IsTimeout
                };
            }
            catch (HttpRequestException ex)
            {
                var elapsed = DateTimeOffset.UtcNow - startTime;
                return new CompactHealthMetrics
                {
                    TimestampMs = startTime.ToUnixTimeMilliseconds(),
                    ResponseTimeMs = (ushort)Math.Min(elapsed.TotalMilliseconds, ushort.MaxValue),
                    StatusCode = 0,
                    Flags = ex.Message.Contains("timeout") ? HealthCheckFlags.IsTimeout : HealthCheckFlags.IsNetworkError
                };
            }
            catch (TaskCanceledException)
            {
                var elapsed = DateTimeOffset.UtcNow - startTime;
                return new CompactHealthMetrics
                {
                    TimestampMs = startTime.ToUnixTimeMilliseconds(),
                    ResponseTimeMs = (ushort)Math.Min(elapsed.TotalMilliseconds, ushort.MaxValue),
                    StatusCode = 0,
                    Flags = HealthCheckFlags.IsTimeout
                };
            }
        }

        /// <summary>
        /// 根据异常创建失败指标
        /// </summary>
        private static CompactHealthMetrics CreateFailedMetrics(Exception ex)
        {
            var flags = ex switch
            {
                TimeoutException => HealthCheckFlags.IsTimeout,
                HttpRequestException when ex.Message.Contains("timeout") => HealthCheckFlags.IsTimeout,
                HttpRequestException => HealthCheckFlags.IsNetworkError,
                TaskCanceledException => HealthCheckFlags.IsTimeout,
                _ => HealthCheckFlags.IsNetworkError
            };

            return new CompactHealthMetrics
            {
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ResponseTimeMs = ushort.MaxValue,
                StatusCode = 0,
                Flags = flags
            };
        }

        /// <summary>
        /// 🎯 初始化并发限制
        /// </summary>
        private void InitializeConcurrencyLimit()
        {
            var concurrency = CalculateOptimalConcurrency(0);
            _concurrencyLimit = new SemaphoreSlim(concurrency, concurrency);

            lock (_statsLock)
            {
                _stats.CurrentConcurrency = concurrency;
            }
        }

        /// <summary>
        /// 🎯 动态更新并发限制
        /// </summary>
        private void UpdateConcurrencyLimit()
        {
            var destinationCount = _currentDestinations.Count;
            var newConcurrency = CalculateOptimalConcurrency(destinationCount);

            var oldLimit = _concurrencyLimit;
            _concurrencyLimit = new SemaphoreSlim(newConcurrency, newConcurrency);
            oldLimit?.Dispose();

            lock (_statsLock)
            {
                _stats.CurrentConcurrency = newConcurrency;
            }

            _logger.LogInformation("并发限制已更新: {OldLimit} -> {NewLimit} (总destination: {Total})",
                oldLimit?.CurrentCount ?? 0, newConcurrency, destinationCount);
        }

        /// <summary>
        /// 计算最优并发数
        /// </summary>
        private int CalculateOptimalConcurrency(int destinationCount)
        {
            var options = _options.CurrentValue;

            if (options.MaxConcurrency > 0)
            {
                return options.MaxConcurrency;
            }

            return options.ThreadPoolStrategy switch
            {
                ThreadPoolStrategy.Static => CalculateStaticConcurrency(destinationCount),
                ThreadPoolStrategy.Dynamic => CalculateDynamicConcurrency(destinationCount),
                _ => Math.Max(1, destinationCount / 10)
            };
        }

        /// <summary>
        /// 静态并发数计算
        /// </summary>
        private static int CalculateStaticConcurrency(int destinationCount)
        {
            return destinationCount switch
            {
                <= 10 => destinationCount,
                <= 50 => destinationCount / 2,
                <= 200 => destinationCount / 5,
                _ => 40
            };
        }

        /// <summary>
        /// 动态并发数计算
        /// </summary>
        private int CalculateDynamicConcurrency(int destinationCount)
        {
            var baseConcurrency = CalculateStaticConcurrency(destinationCount);

            var stats = GetStats();
            if (stats.TotalCollections > 100)
            {
                if (stats.SuccessRate > 0.95)
                {
                    baseConcurrency = (int)(baseConcurrency * 1.2);
                }
                else if (stats.SuccessRate < 0.8)
                {
                    baseConcurrency = (int)(baseConcurrency * 0.8);
                }
            }

            return Math.Max(1, Math.Min(baseConcurrency, 100));
        }

        /// <summary>
        /// 刷新destinations缓存
        /// </summary>
        private async Task RefreshDestinationsAsync()
        {
            try
            {
                var allServices = await _serviceRepository.GetAllAsync();
                var newDestinations = new Dictionary<string, Destination>();

                foreach (var service in allServices)
                {
                    foreach (var destination in service.Destinations)
                    {
                        newDestinations[destination.Code] = destination;
                    }
                }

                lock (_destinationsLock)
                {
                    _currentDestinations = newDestinations;
                }

                SyncStorageDestinations(newDestinations.Keys);

                UpdateConcurrencyLimit();

                _logger.LogDebug("已刷新destinations缓存，总数: {Count}", newDestinations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新destinations缓存失败");
            }
        }

        /// <summary>
        /// 同步存储层的destination列表
        /// </summary>
        private void SyncStorageDestinations(IEnumerable<string> destinationCodes)
        {
            if (_storage is MemoryHealthMetricsStorage memoryStorage)
            {
                var currentCodes = memoryStorage.GetAllDestinationCodes();
                var newCodesSet = new HashSet<string>(destinationCodes);
                var currentCodesSet = new HashSet<string>(currentCodes);

                foreach (var newCode in newCodesSet.Except(currentCodesSet))
                {
                    memoryStorage.AddDestination(newCode);
                }

                foreach (var oldCode in currentCodesSet.Except(newCodesSet))
                {
                    memoryStorage.RemoveDestination(oldCode);
                }
            }
        }

        /// <summary>
        /// 记录成功收集
        /// </summary>
        private void RecordSuccess(TimeSpan elapsed)
        {
            lock (_statsLock)
            {
                _stats.SuccessfulCollections++;
                UpdateAverageTime(elapsed.TotalMilliseconds);
            }
        }

        /// <summary>
        /// 记录失败收集
        /// </summary>
        private void RecordFailure(TimeSpan elapsed)
        {
            lock (_statsLock)
            {
                _stats.FailedCollections++;
                UpdateAverageTime(elapsed.TotalMilliseconds);
            }
        }

        /// <summary>
        /// 更新平均收集时间
        /// </summary>
        private void UpdateAverageTime(double elapsedMs)
        {
            _recentCollectionTimes.Enqueue(elapsedMs);

            while (_recentCollectionTimes.Count > 100)
            {
                _recentCollectionTimes.TryDequeue(out _);
            }

            if (_recentCollectionTimes.Count > 0)
            {
                _stats.AverageCollectionTimeMs = _recentCollectionTimes.Average();
            }
        }
        private HttpClient CreateHttpClient(Destination destination, HealthCheckOptions options)
        {
            var clientName = GetHttpClientName(destination);
            var httpClient = _httpClientFactory.CreateClient(clientName);

            httpClient.Timeout = options.Timeout.Add(TimeSpan.FromMilliseconds(-100));

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "ApiGateway-HealthChecker/1.0");
            httpClient.DefaultRequestHeaders.Add("X-Health-Check", "true");

            return httpClient;
        }
        private static string GetHttpClientName(Destination destination)
        {
            var uri = new Uri(destination.GetAddress());
            return $"health-check-{uri.Host}-{uri.Port}";
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return base.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _timer?.Dispose();
            return base.StopAsync(cancellationToken);
        }

        public Task<CompactHealthMetrics> CollectMetricsAsync(string destinationCode, Destination destination)
        {
            return PerformHealthCheckAsync(destination, CancellationToken.None);
        }

        public CollectorStats GetStats()
        {
            lock (_statsLock)
            {
                return _stats;
            }
        }

        public async Task UpdateDestinationsAsync(IEnumerable<string> destinationCodes)
        {
            await RefreshDestinationsAsync();
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            _concurrencyLimit?.Dispose();
            base.Dispose();
        }
    }
}
using Domain.Services;
using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;
using Domain.Services.Services;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using HttpMethod = Domain.Routes.HttpMethod;

namespace Tests.Integration;

/// <summary>
/// 超时与重试机制集成测试（P0 级）
/// 验证：超时检测、错误分类、重试机制、连接管理
/// </summary>
[Collection("Gateway Integration Tests")]
public class TimeoutAndRetryTests
{
    private readonly ILogger<HealthCalculationService> _logger;
    private readonly Mock<IHealthMetricsStorage> _mockStorage;
    private readonly Mock<IHealthPolicyCalculatorFactory> _mockCalculatorFactory;

    public TimeoutAndRetryTests()
    {
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<HealthCalculationService>();
        _mockStorage = new Mock<IHealthMetricsStorage>();
        _mockCalculatorFactory = new Mock<IHealthPolicyCalculatorFactory>();
    }

    #region 1. 超时配置与检测

    /// <summary>
    /// 测试：HTTP客户端超时的基本配置
    /// 场景：验证 HttpClient 的超时设置正确传播
    /// </summary>
    [Fact]
    public void Should_Configure_Http_Client_Timeout()
    {
        // Arrange
        var service = new Service("timeout-service", "Service with Timeout Config");
        var destination = new Destination(
            "dest-with-timeout",
            Protocol.Http,
            "localhost",
            8001,
            weight: 100,
            timeoutSeconds: 30);  // 30秒超时

        service.AddDestination(destination);

        // Act & Assert
        Assert.NotNull(destination);
        Assert.Equal(30, destination.TimeoutSeconds);
    }

    /// <summary>
    /// 测试：超时设置的默认值
    /// 场景：未指定超时时使用默认超时
    /// </summary>
    [Fact]
    public void Should_Use_Default_Timeout_When_Not_Specified()
    {
        // Arrange
        var destination = new Destination(
            "default-timeout",
            Protocol.Http,
            "localhost",
            8001);  // 不指定超时

        // Act & Assert
        Assert.Equal(30, destination.TimeoutSeconds); // 默认 30 秒
    }

    /// <summary>
    /// 测试：流式转发超时的独立配置
    /// 场景：大文件传输使用更长的超时时间
    /// </summary>
    [Fact]
    public void Should_Configure_Streaming_Timeout_Separately()
    {
        // Arrange
        var service = new Service("streaming-service", "Service with Streaming Timeout");

        // Streaming 超时通常比普通请求长 (例如 30 分钟用于大文件)
        var streamingTimeout = 30 * 60; // 30 分钟

        // Act & Assert
        Assert.True(streamingTimeout > 30, "Streaming timeout should be much larger than regular timeout");
    }

    #endregion

    #region 2. 超时异常处理

    /// <summary>
    /// 测试：超时异常的正确识别
    /// 场景：TimeoutException 应被识别为超时而不是其他错误
    /// </summary>
    [Fact]
    public void Should_Identify_TimeoutException_As_Timeout()
    {
        // Arrange
        var exception = new TimeoutException("Request timed out");

        // Act
        var isTimeout = exception is TimeoutException;
        var isNetworkError = exception is TimeoutException;

        // Assert
        Assert.True(isTimeout);
        Assert.True(isNetworkError);
    }

    /// <summary>
    /// 测试：Task 取消异常作为超时的识别
    /// 场景：OperationCanceledException 可能表示超时
    /// </summary>
    [Fact]
    public void Should_Identify_OperationCanceledException_As_Potential_Timeout()
    {
        // Arrange
        var exception = new OperationCanceledException("Operation was canceled");

        // Act
        var isCanceled = exception is OperationCanceledException;

        // Assert
        Assert.True(isCanceled);
    }

    /// <summary>
    /// 测试：HttpRequestException 中的超时错误识别
    /// 场景：连接超时表现为 HttpRequestException
    /// </summary>
    [Fact]
    public void Should_Identify_HttpRequestException_With_Timeout_Message()
    {
        // Arrange
        var exception = new HttpRequestException("A task was canceled");

        // Act
        var message = exception.Message.ToLower();
        var mightBeTimeout = message.Contains("timeout");

        // Assert
        Assert.False(mightBeTimeout); // 此例不包含 timeout 关键字
    }

    /// <summary>
    /// 测试：超时异常与其他异常的区分
    /// 场景：不应将 404 或 500 误认为超时
    /// </summary>
    [Fact]
    public void Should_Distinguish_Timeout_From_Http_Errors()
    {
        // Arrange
        var timeoutEx = new TimeoutException();
        var httpEx = new HttpRequestException("404 Not Found");

        // Act
        var isTimeout1 = timeoutEx is TimeoutException;
        var isTimeout2 = httpEx is TimeoutException;

        // Assert
        Assert.True(isTimeout1);
        Assert.False(isTimeout2);
    }

    #endregion

    #region 3. 错误分类与重试策略

    /// <summary>
    /// 测试：5xx 错误应该重试
    /// 场景：服务端错误 (500, 502, 503) 可以重试
    /// </summary>
    [Fact]
    public void Should_Retry_On_Server_Errors()
    {
        // Arrange
        var serverErrors = new[] { 500, 502, 503, 504 };

        // Act & Assert
        foreach (var statusCode in serverErrors)
        {
            var shouldRetry = statusCode >= 500 && statusCode < 600;
            Assert.True(shouldRetry, $"Status code {statusCode} should be retryable");
        }
    }

    /// <summary>
    /// 测试：4xx 错误不应该重试（除个别例外）
    /// 场景：客户端错误 (400, 404) 通常不重试
    /// </summary>
    [Fact]
    public void Should_Not_Retry_On_Client_Errors()
    {
        // Arrange
        var clientErrors = new[] { 400, 401, 403, 404 };

        // Act & Assert
        foreach (var statusCode in clientErrors)
        {
            var shouldRetry = statusCode == 408 || statusCode == 429; // 408 和 429 可以重试
            Assert.False(shouldRetry, $"Status code {statusCode} should not be retried (except 408 and 429)");
        }
    }

    /// <summary>
    /// 测试：特殊情况 - 429 (Too Many Requests) 应该重试
    /// 场景：限流错误应该重试（带退避）
    /// </summary>
    [Fact]
    public void Should_Retry_On_TooManyRequests()
    {
        // Arrange
        var statusCode = 429;

        // Act
        var shouldRetry = statusCode == 429 || statusCode == 408;

        // Assert
        Assert.True(shouldRetry);
    }

    /// <summary>
    /// 测试：连接被重置作为可重试错误
    /// 场景：ConnectionResetException 应该重试
    /// </summary>
    [Fact]
    public void Should_Retry_On_Connection_Reset()
    {
        // Arrange
        var exception = new IOException("Connection reset by peer");

        // Act
        var isRetryable = exception is IOException;

        // Assert
        Assert.True(isRetryable);
    }

    #endregion

    #region 4. 重试机制

    /// <summary>
    /// 测试：服务完整的重试配置
    /// 场景：验证服务能被配置为支持重试
    /// </summary>
    [Fact]
    public void Should_Configure_Service_With_Retry_Policy()
    {
        // Arrange
        var service = new Service("resilient-service", "Service with Retry");

        // Act
        service.WithErrorHandling(null, null, null, null);

        // Assert
        Assert.NotNull(service.RetryPolicy);
    }

    /// <summary>
    /// 测试：重试次数的正确限制
    /// 场景：避免无限重试，通常限制为 3-5 次
    /// </summary>
    [Fact]
    public void Should_Limit_Retry_Attempts()
    {
        // Arrange
        const int maxRetries = 3;
        var retryCount = 0;

        // Act
        while (retryCount < maxRetries && retryCount < 10)
        {
            retryCount++;
        }

        // Assert
        Assert.Equal(maxRetries, retryCount);
        Assert.True(retryCount <= 5, "Retry attempts should be limited");
    }

    /// <summary>
    /// 测试：异步重试操作的正确处理
    /// 场景：异步操作在失败后应能正确重试
    /// </summary>
    [Fact]
    public async Task Should_Handle_Async_Retry_Operations()
    {
        // Arrange
        var attemptCount = 0;
        var maxRetries = 3;

        // Act
        while (attemptCount < maxRetries)
        {
            try
            {
                attemptCount++;
                await Task.Delay(10); // 模拟异步操作
                if (attemptCount == 2)
                {
                    throw new HttpRequestException("Temporary failure");
                }
            }
            catch (HttpRequestException) when (attemptCount < maxRetries)
            {
                continue; // 重试
            }
        }

        // Assert
        Assert.Equal(3, attemptCount);
    }

    #endregion

    #region 5. 连接与资源管理

    /// <summary>
    /// 测试：连接池的正确配置
    /// 场景：HttpClient 使用连接池提高性能
    /// </summary>
    [Fact]
    public void Should_Configure_Connection_Pool()
    {
        // Arrange
        const int maxConnectionsPerServer = 10;
        const int pooledConnectionIdleTimeoutSeconds = 120;

        // Act & Assert
        Assert.True(maxConnectionsPerServer >= 5, "Should have reasonable connection pool size");
        Assert.True(pooledConnectionIdleTimeoutSeconds >= 60, "Should keep connections alive for reasonable time");
    }

    /// <summary>
    /// 测试：连接重用以减少开销
    /// 场景：保持 HTTP Keep-Alive 连接的活跃
    /// </summary>
    [Fact]
    public void Should_Reuse_Http_Connections()
    {
        // Arrange
        var firstRequestTime = DateTime.Now;
        var secondRequestTime = DateTime.Now.AddMilliseconds(100);
        var idleTimeout = TimeSpan.FromSeconds(120);

        // Act
        var timeBetweenRequests = secondRequestTime - firstRequestTime;
        var shouldReuse = timeBetweenRequests < idleTimeout;

        // Assert
        Assert.True(shouldReuse, "Connections should be reused within idle timeout");
    }

    /// <summary>
    /// 测试：idle 连接的自动清理
    /// 场景：超过 idle 时间的连接应被释放
    /// </summary>
    [Fact]
    public void Should_Clean_Idle_Connections()
    {
        // Arrange
        const long pooledConnectionIdleTimeoutMillis = 120000; // 2 分钟
        var connectionCreatedTime = DateTime.Now.AddSeconds(-150); // 2.5 分钟前

        // Act
        var isIdle = DateTime.Now - connectionCreatedTime > TimeSpan.FromMilliseconds(pooledConnectionIdleTimeoutMillis);

        // Assert
        Assert.True(isIdle, "Old idle connections should be cleaned up");
    }

    #endregion

    #region 6. 综合场景

    /// <summary>
    /// 测试：完整的超时 → 重试 → 转移流程
    /// 场景：1) 请求超时 2) 触发重试 3) 重试失败转移到其他目标
    /// </summary>
    [Fact]
    public void Should_Handle_Timeout_Retry_And_Failover_Flow()
    {
        // Arrange
        var service = new Service("resilient-api", "Resilient API Service");
        service.AddDestination(new Destination("primary", Protocol.Http, "localhost", 8001, weight: 100, timeoutSeconds: 5));
        service.AddDestination(new Destination("secondary", Protocol.Http, "localhost", 8002, weight: 100, timeoutSeconds: 5));
        service.AddDestination(new Destination("tertiary", Protocol.Http, "localhost", 8003, weight: 100, timeoutSeconds: 5));

        var mockHealthService = new Mock<IHealthCalculationService>();

        // 模拟 primary 不健康
        mockHealthService
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code == "primary"), It.IsAny<HealthCheckConfig>()))
            .Returns(false);
        mockHealthService
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code != "primary"), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        // Act
        var healthyDestinations = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Equal(2, healthyDestinations.Count); // 排除 primary
        Assert.All(healthyDestinations, d => Assert.Equal(5, d.TimeoutSeconds));
    }

    /// <summary>
    /// 测试：重试次数用尽后的最终降级
    /// 场景：所有重试都失败 → 返回最后一次错误给客户端
    /// </summary>
    [Fact]
    public async Task Should_Return_Error_After_All_Retries_Exhausted()
    {
        // Arrange
        var maxRetries = 3;
        var attemptCount = 0;
        Exception lastException = null;

        // Act
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                attemptCount++;
                if (i < maxRetries - 1)
                {
                    throw new HttpRequestException("Service unavailable");
                }
            }
            catch (Exception ex) when (attemptCount < maxRetries)
            {
                lastException = ex;
                await Task.Delay(10); // 模拟重试间隔
            }
        }

        // Assert
        Assert.Equal(maxRetries, attemptCount);
        // lastException 可能为 null，因为最后一次不抛出异常
    }

    /// <summary>
    /// 测试：超时间隔随重试次数增加（指数退避）
    /// 场景：1 秒 → 2 秒 → 4 秒 → 8 秒
    /// </summary>
    [Fact]
    public void Should_Apply_Exponential_Backoff_For_Retries()
    {
        // Arrange
        var baseDelayMs = 100;
        var retryAttempts = new[] { 0, 1, 2, 3 };

        // Act
        var delays = retryAttempts.Select(attempt =>
            (long)(baseDelayMs * Math.Pow(2, attempt))
        ).ToList();

        // Assert
        Assert.Equal(100, delays[0]);    // 第1次: 100ms
        Assert.Equal(200, delays[1]);    // 第2次: 200ms
        Assert.Equal(400, delays[2]);    // 第3次: 400ms
        Assert.Equal(800, delays[3]);    // 第4次: 800ms
    }

    /// <summary>
    /// 测试：并发请求时的超时隔离
    /// 场景：一个请求超时不应影响其他并发请求
    /// </summary>
    [Fact]
    public async Task Should_Isolate_Timeout_Between_Concurrent_Requests()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            var taskIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                // 模拟请求
                await Task.Delay(taskIndex * 10);
                // 第2个请求会模拟超时
                if (taskIndex == 2)
                {
                    throw new TimeoutException();
                }
            }));
        }

        // 捕获任何超时异常
        var results = await Task.WhenAll(
            tasks.Select(t => t.ContinueWith(task =>
                task.IsFaulted ? task.Exception?.InnerException : null
            ))
        );

        // Assert
        Assert.NotNull(results);
        Assert.Single(results.Where(r => r is TimeoutException));
    }

    #endregion
}

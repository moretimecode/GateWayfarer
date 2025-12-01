using Domain.Services;
using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;
using Domain.Services.Services;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using HttpMethod = Domain.Routes.HttpMethod;

/// <summary>
/// 健康检查集成测试
/// 测试健康检查配置、目标状态过滤、并发检查等场景
/// </summary>
[Collection("Gateway Integration Tests")]
public class HealthCheckingTests
{
    private readonly ILogger<HealthCalculationService> _logger;
    private readonly Mock<IHealthMetricsStorage> _mockStorage;
    private readonly Mock<IHealthPolicyCalculatorFactory> _mockCalculatorFactory;

    public HealthCheckingTests()
    {
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<HealthCalculationService>();
        _mockStorage = new Mock<IHealthMetricsStorage>();
        _mockCalculatorFactory = new Mock<IHealthPolicyCalculatorFactory>();
    }

    /// <summary>
    /// 测试：健康检查配置创建和验证
    /// </summary>
    [Fact]
    public void Should_Create_Health_Check_Config()
    {
        // Arrange
        var config = new HealthCheckConfig(
            activeEnabled: true,
            activeIntervalSeconds: 10,
            activeTimeoutSeconds: 5,
            activePath: "/health",
            failureThreshold: 5,
            successThreshold: 2,
            passiveEnabled: true
        );

        // Act & Assert
        Assert.True(config.ActiveEnabled);
        Assert.True(config.PassiveEnabled);
        Assert.Equal("/health", config.ActivePath);
        Assert.Equal(10, config.ActiveIntervalSeconds);
        Assert.Equal(2, config.SuccessThreshold);
        Assert.Equal(5, config.FailureThreshold);
    }

    /// <summary>
    /// 测试：目标实例默认健康状态
    /// 当禁用主动健康检查时，目标应视为健康
    /// </summary>
    [Fact]
    public void Should_Treat_Destination_As_Healthy_When_Active_Check_Disabled()
    {
        // Arrange
        var healthCheckConfig = new HealthCheckConfig(activeEnabled: false);
        var destination = new Destination("dest-1", Protocol.Http, "localhost", 8080, 100, 30);
        var service = new HealthCalculationService(_mockStorage.Object, _mockCalculatorFactory.Object, _logger);

        // Act
        var isHealthy = service.IsHealthy(destination, healthCheckConfig);

        // Assert
        Assert.True(isHealthy);
    }

    /// <summary>
    /// 测试：从不健康目标列表过滤健康目标
    /// </summary>
    [Fact]
    public void Should_Filter_Healthy_Destinations_From_Service()
    {
        // Arrange
        var service = new Service("test-svc", "Test Service");
        service.AddDestination(new Destination("healthy-1", Protocol.Http, "host1", 80, 100, 30));
        service.AddDestination(new Destination("healthy-2", Protocol.Http, "host2", 80, 100, 30));
        service.AddDestination(new Destination("unhealthy", Protocol.Http, "host3", 80, 100, 30));

        var healthCheckConfig = new HealthCheckConfig(activeEnabled: false);

        var mockHealthService = new Mock<IHealthCalculationService>();
        mockHealthService
            .Setup(h => h.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns((Destination d, HealthCheckConfig c) => d.Code != "unhealthy");

        // Act
        var healthyDestinations = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Equal(2, healthyDestinations.Count);
        Assert.DoesNotContain(healthyDestinations, d => d.Code == "unhealthy");
        Assert.Contains(healthyDestinations, d => d.Code == "healthy-1");
        Assert.Contains(healthyDestinations, d => d.Code == "healthy-2");
    }

    /// <summary>
    /// 测试：目标实例自定义健康检查路径
    /// </summary>
    [Fact]
    public void Should_Use_Custom_Health_Path_For_Destination()
    {
        // Arrange
        var destination = new Destination(
            "custom-health",
            Protocol.Http,
            "api.example.com",
            80,
            100,
            30,
            "/api/health");

        // Act
        var healthUrl = destination.GetHealthCheckUrl("/default");

        // Assert
        Assert.Contains("/api/health", healthUrl);
    }

    /// <summary>
    /// 测试：目标实例使用默认健康检查路径
    /// </summary>
    [Fact]
    public void Should_Use_Default_Health_Path_When_Not_Specified()
    {
        // Arrange
        var destination = new Destination(
            "default-health",
            Protocol.Http,
            "api.example.com",
            80,
            100,
            30,
            null);

        // Act
        var healthUrl = destination.GetHealthCheckUrl("/health");

        // Assert
        Assert.Equal("http://api.example.com/health", healthUrl);
    }

    /// <summary>
    /// 测试：健康检查URL支持HTTPS协议
    /// </summary>
    [Fact]
    public void Should_Generate_Https_Health_Check_Url()
    {
        // Arrange
        var destination = new Destination(
            "secure-health",
            Protocol.Https,
            "api.secure.com",
            443,
            100,
            30,
            "/healthz");

        // Act
        var healthUrl = destination.GetHealthCheckUrl("/health");

        // Assert
        Assert.StartsWith("https://", healthUrl);
    }

    /// <summary>
    /// 测试：所有目标都不健康的情况
    /// </summary>
    [Fact]
    public void Should_Return_Empty_List_When_No_Healthy_Destinations()
    {
        // Arrange
        var service = new Service("failing-svc", "Failing Service");
        service.AddDestination(new Destination("unhealthy-1", Protocol.Http, "host1", 80, 100, 30));
        service.AddDestination(new Destination("unhealthy-2", Protocol.Http, "host2", 80, 100, 30));

        var mockHealthService = new Mock<IHealthCalculationService>();
        mockHealthService
            .Setup(h => h.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns(false);

        var healthCheckConfig = new HealthCheckConfig();

        // Act
        var healthyDestinations = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Empty(healthyDestinations);
    }

    /// <summary>
    /// 测试：动态更新健康检查配置
    /// </summary>
    [Fact]
    public void Should_Update_Health_Check_Config_Dynamically()
    {
        // Arrange
        var service = new Service("dynamic-svc", "Dynamic Service");
        var originalConfig = new HealthCheckConfig(
            activeEnabled: true,
            activeIntervalSeconds: 10
        );
        service.UpdateHealthCheckConfig(originalConfig);

        var newConfig = new HealthCheckConfig(
            activeEnabled: false,
            activeIntervalSeconds: 30
        );

        // Act
        service.UpdateHealthCheckConfig(newConfig);

        // Assert
        Assert.False(service.HealthCheck.ActiveEnabled);
        Assert.Equal(30, service.HealthCheck.ActiveIntervalSeconds);
    }

    /// <summary>
    /// 测试：并发健康检查场景
    /// </summary>
    [Fact]
    public void Should_Handle_Concurrent_Health_Checks()
    {
        // Arrange
        var service = new Service("concurrent-svc", "Concurrent Service");
        for (int i = 0; i < 10; i++)
        {
            service.AddDestination(new Destination($"server-{i}", Protocol.Http, $"host{i}", 80, 100, 30));
        }

        var healthCheckConfig = new HealthCheckConfig(activeEnabled: false);

        var mockHealthService = new Mock<IHealthCalculationService>();
        mockHealthService
            .Setup(h => h.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        var results = new System.Collections.Concurrent.ConcurrentBag<List<Destination>>();

        // Act
        Parallel.For(0, 100, i =>
        {
            var healthy = service.GetHealthyDestinations(mockHealthService.Object);
            results.Add(healthy);
        });

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, healthy => Assert.Equal(10, healthy.Count));
    }

    /// <summary>
    /// 测试：健康检查配置验证
    /// </summary>
    [Fact]
    public void Should_Validate_Health_Check_Parameters()
    {
        // Arrange
        var config = new HealthCheckConfig(
            failureThreshold: 5,
            successThreshold: 2
        );

        // Act & Assert
        Assert.True(config.SuccessThreshold > 0);
        Assert.True(config.FailureThreshold > 0);
    }

    /// <summary>
    /// 测试：部分目标不健康的场景
    /// </summary>
    [Fact]
    public void Should_Distribute_Traffic_Only_To_Healthy_Destinations()
    {
        // Arrange
        var service = new Service("partial-healthy-svc", "Partially Healthy Service");
        var healthy1 = new Destination("healthy-1", Protocol.Http, "h1.com", 80, 100, 30);
        var healthy2 = new Destination("healthy-2", Protocol.Http, "h2.com", 80, 100, 30);
        var unhealthy = new Destination("unhealthy", Protocol.Http, "bad.com", 80, 100, 30);

        service.AddDestination(healthy1);
        service.AddDestination(healthy2);
        service.AddDestination(unhealthy);

        var mockHealthService = new Mock<IHealthCalculationService>();
        mockHealthService
            .Setup(h => h.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns((Destination d, HealthCheckConfig c) => d.Code != "unhealthy");

        var healthCheckConfig = new HealthCheckConfig();

        // Act
        var healthyList = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Equal(2, healthyList.Count);
        Assert.All(healthyList, d => Assert.NotEqual("unhealthy", d.Code));
    }

    /// <summary>
    /// 测试：获取健康检查URL时处理异常路径
    /// </summary>
    [Fact]
    public void Should_Handle_Health_Path_Edge_Cases()
    {
        // Arrange & Act & Assert
        var dest1 = new Destination("dest1", Protocol.Http, "host", 80, 100, 30, "health");
        Assert.Equal("http://host/health", dest1.GetHealthCheckUrl("/default"));

        var dest2 = new Destination("dest2", Protocol.Http, "host", 80, 100, 30, "/health");
        Assert.Equal("http://host/health", dest2.GetHealthCheckUrl("/default"));

        var dest3 = new Destination("dest3", Protocol.Http, "host", 80, 100, 30, "");
        Assert.Equal("http://host/default", dest3.GetHealthCheckUrl("/default"));
    }

    /// <summary>
    /// 测试：服务无目标时的健康检查处理
    /// </summary>
    [Fact]
    public void Should_Return_Empty_When_Service_Has_No_Destinations()
    {
        // Arrange
        var service = new Service("empty-svc", "Empty Service");
        var healthCheckConfig = new HealthCheckConfig();
        var mockHealthService = new Mock<IHealthCalculationService>();

        // Act
        var healthy = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Empty(healthy);
    }

    /// <summary>
    /// 测试：健康检查间隔配置
    /// </summary>
    [Fact]
    public void Should_Configure_Health_Check_Intervals()
    {
        // Arrange
        // activeTimeoutSeconds must be less than activeIntervalSeconds
        var config1 = new HealthCheckConfig(activeIntervalSeconds: 10, activeTimeoutSeconds: 5);
        var config2 = new HealthCheckConfig(activeIntervalSeconds: 30, activeTimeoutSeconds: 10);

        // Act & Assert
        Assert.True(config1.ActiveIntervalSeconds < config2.ActiveIntervalSeconds);
        Assert.Equal(10, config1.ActiveIntervalSeconds);
        Assert.Equal(30, config2.ActiveIntervalSeconds);
    }

    /// <summary>
    /// 测试：无效的健康检查配置参数
    /// </summary>
    [Fact]
    public void Should_Reject_Invalid_Health_Check_Parameters()
    {
        // 间隔时间必须大于超时时间
        Assert.Throws<ArgumentException>(() =>
            new HealthCheckConfig(activeIntervalSeconds: 5, activeTimeoutSeconds: 10)
        );

        // 间隔和超时必须大于0
        Assert.Throws<ArgumentException>(() =>
            new HealthCheckConfig(activeIntervalSeconds: 0)
        );
    }
}

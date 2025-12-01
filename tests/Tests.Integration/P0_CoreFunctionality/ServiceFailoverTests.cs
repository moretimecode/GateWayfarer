using Domain.Routes;
using Domain.Routes.Configuration;
using Domain.Routes.Contexts;
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
/// 故障转移与降级集成测试（P0 级）
/// 验证：健康检查系统、目标选择过滤、自动转移、恢复检测
/// </summary>
[Collection("Gateway Integration Tests")]
public class ServiceFailoverTests
{
    private readonly GatewayTestFixture _fixture;
    private readonly ILogger<HealthCalculationService> _logger;
    private readonly Mock<IHealthMetricsStorage> _mockStorage;
    private readonly Mock<IHealthPolicyCalculatorFactory> _mockCalculatorFactory;

    public ServiceFailoverTests(GatewayTestFixture fixture)
    {
        _fixture = fixture;
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<HealthCalculationService>();
        _mockStorage = new Mock<IHealthMetricsStorage>();
        _mockCalculatorFactory = new Mock<IHealthPolicyCalculatorFactory>();
    }

    #region 1. 基础故障转移

    /// <summary>
    /// 测试：单个目标宕机时自动转移到备用目标
    /// 场景：3个目标 → 1个宕机 → 自动转移
    /// </summary>
    [Fact]
    public void Should_Failover_When_Primary_Destination_Fails()
    {
        // Arrange
        var service = new Service("api-service", "API Service");
        service.AddDestination(new Destination("primary", Protocol.Http, "localhost", 8001, weight: 100));
        service.AddDestination(new Destination("backup-1", Protocol.Http, "localhost", 8002, weight: 50));
        service.AddDestination(new Destination("backup-2", Protocol.Http, "localhost", 8003, weight: 50));

        var healthCalcService = new HealthCalculationService(_mockStorage.Object, _mockCalculatorFactory.Object, _logger);
        var healthCheckConfig = new HealthCheckConfig();

        // 模拟 primary 不健康，其他健康
        var mockHealthService = new Mock<IHealthCalculationService>();
        mockHealthService
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code == "primary"), It.IsAny<HealthCheckConfig>()))
            .Returns(false);
        mockHealthService
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code != "primary"), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        // Act
        var healthyDestinations = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Equal(2, healthyDestinations.Count);
        Assert.DoesNotContain(healthyDestinations, d => d.Code == "primary");
        Assert.Contains(healthyDestinations, d => d.Code == "backup-1");
        Assert.Contains(healthyDestinations, d => d.Code == "backup-2");
    }

    /// <summary>
    /// 测试：所有目标都不健康时的降级处理
    /// 场景：所有目标 → 都宕机 → 返回空列表
    /// </summary>
    [Fact]
    public void Should_Return_Empty_When_All_Destinations_Unhealthy()
    {
        // Arrange
        var service = new Service("failing-service", "Failing Service");
        service.AddDestination(new Destination("dest-1", Protocol.Http, "localhost", 8001));
        service.AddDestination(new Destination("dest-2", Protocol.Http, "localhost", 8002));
        service.AddDestination(new Destination("dest-3", Protocol.Http, "localhost", 8003));

        var mockHealthService = new Mock<IHealthCalculationService>();
        mockHealthService
            .Setup(h => h.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns(false);

        // Act
        var healthyDestinations = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Empty(healthyDestinations);
    }

    /// <summary>
    /// 测试：单个健康目标的正常选择
    /// 场景：多个目标中只有1个健康 → 选择该目标
    /// </summary>
    [Fact]
    public void Should_Select_Single_Healthy_Destination()
    {
        // Arrange
        var service = new Service("degraded-service", "Degraded Service");
        service.AddDestination(new Destination("unhealthy-1", Protocol.Http, "localhost", 8001));
        service.AddDestination(new Destination("healthy", Protocol.Http, "localhost", 8002));
        service.AddDestination(new Destination("unhealthy-2", Protocol.Http, "localhost", 8003));

        var mockHealthService = new Mock<IHealthCalculationService>();
        mockHealthService
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code == "healthy"), It.IsAny<HealthCheckConfig>()))
            .Returns(true);
        mockHealthService
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code != "healthy"), It.IsAny<HealthCheckConfig>()))
            .Returns(false);

        // Act
        var healthyDestinations = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Single(healthyDestinations);
        Assert.Equal("healthy", healthyDestinations[0].Code);
    }

    #endregion

    #region 2. 健康状态跟踪

    /// <summary>
    /// 测试：健康检查配置的完整生命周期
    /// 场景：创建 → 配置 → 验证
    /// </summary>
    [Fact]
    public void Should_Create_And_Validate_Health_Check_Config()
    {
        // Arrange
        var config = new HealthCheckConfig(
            activeEnabled: true,
            activeIntervalSeconds: 15,
            activeTimeoutSeconds: 10,
            activePath: "/health",
            failureThreshold: 3,
            successThreshold: 2,
            passiveEnabled: true
        );

        // Act & Assert
        Assert.True(config.ActiveEnabled);
        Assert.True(config.PassiveEnabled);
        Assert.Equal(15, config.ActiveIntervalSeconds);
        Assert.Equal(10, config.ActiveTimeoutSeconds);
        Assert.Equal("/health", config.ActivePath);
        Assert.Equal(3, config.FailureThreshold);
        Assert.Equal(2, config.SuccessThreshold);
    }

    /// <summary>
    /// 测试：目标健康检查URL生成
    /// 场景：测试自定义健康检查端点和默认路径
    /// </summary>
    [Fact]
    public void Should_Generate_Correct_Health_Check_Urls()
    {
        // Arrange
        var destWithCustomPath = new Destination(
            "custom-health",
            Protocol.Http,
            "api.example.com",
            80,
            100,
            30,
            "/api/health/status");

        var destWithoutCustomPath = new Destination(
            "default-health",
            Protocol.Http,
            "api.example.com",
            80,
            100,
            30,
            null);

        // Act
        var customUrl = destWithCustomPath.GetHealthCheckUrl("/health");
        var defaultUrl = destWithoutCustomPath.GetHealthCheckUrl("/health");

        // Assert
        Assert.Contains("/api/health/status", customUrl);
        Assert.Contains("/health", defaultUrl);
        Assert.StartsWith("http://", customUrl);
    }

    /// <summary>
    /// 测试：HTTPS 协议的健康检查URL生成
    /// 场景：确保 HTTPS 目标生成正确的健康检查URL
    /// </summary>
    [Fact]
    public void Should_Generate_Https_Health_Check_Url()
    {
        // Arrange
        var destination = new Destination(
            "secure-api",
            Protocol.Https,
            "secure.api.com",
            443,
            100,
            30,
            "/healthz");

        // Act
        var healthUrl = destination.GetHealthCheckUrl("/health");

        // Assert
        Assert.StartsWith("https://", healthUrl);
        Assert.Contains("secure.api.com", healthUrl);
    }

    #endregion

    #region 3. 目标选择与负载均衡

    /// <summary>
    /// 测试：健康目标过滤后仍保留权重信息
    /// 场景：过滤后的目标权重应该保持不变用于负载均衡
    /// </summary>
    [Fact]
    public void Should_Preserve_Weights_After_Healthy_Filtering()
    {
        // Arrange
        var service = new Service("weighted-service", "Weighted Service");
        service.AddDestination(new Destination("high-weight", Protocol.Http, "host1", 8001, weight: 100));
        service.AddDestination(new Destination("low-weight", Protocol.Http, "host2", 8002, weight: 25));
        service.AddDestination(new Destination("unhealthy", Protocol.Http, "host3", 8003, weight: 50));

        var mockHealthService = new Mock<IHealthCalculationService>();
        mockHealthService
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code == "unhealthy"), It.IsAny<HealthCheckConfig>()))
            .Returns(false);
        mockHealthService
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code != "unhealthy"), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        // Act
        var healthyDestinations = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Equal(2, healthyDestinations.Count);
        var highWeight = healthyDestinations.FirstOrDefault(d => d.Code == "high-weight");
        var lowWeight = healthyDestinations.FirstOrDefault(d => d.Code == "low-weight");

        Assert.NotNull(highWeight);
        Assert.NotNull(lowWeight);
        Assert.Equal(100, highWeight.Weight);
        Assert.Equal(25, lowWeight.Weight);
    }

    /// <summary>
    /// 测试：多个健康目标的正确返回
    /// 场景：多个目标都健康 → 返回所有目标
    /// </summary>
    [Fact]
    public void Should_Return_All_Healthy_Destinations_When_All_Healthy()
    {
        // Arrange
        var service = new Service("healthy-service", "All Healthy Service");
        service.AddDestination(new Destination("server-1", Protocol.Http, "host1", 8001));
        service.AddDestination(new Destination("server-2", Protocol.Http, "host2", 8002));
        service.AddDestination(new Destination("server-3", Protocol.Http, "host3", 8003));

        var mockHealthService = new Mock<IHealthCalculationService>();
        mockHealthService
            .Setup(h => h.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        // Act
        var healthyDestinations = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Equal(3, healthyDestinations.Count);
    }

    #endregion

    #region 4. 恢复与再入选

    /// <summary>
    /// 测试：不健康目标恢复后的重新入选
    /// 场景：目标恢复健康 → 重新加入负载均衡池
    /// </summary>
    [Fact]
    public void Should_Re_Select_Destination_After_Recovery()
    {
        // Arrange
        var service = new Service("recovering-service", "Service with Recovery");
        service.AddDestination(new Destination("recovered", Protocol.Http, "localhost", 8001));
        service.AddDestination(new Destination("stable", Protocol.Http, "localhost", 8002));

        // 第一次：目标不健康
        var mockHealthService1 = new Mock<IHealthCalculationService>();
        mockHealthService1
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code == "recovered"), It.IsAny<HealthCheckConfig>()))
            .Returns(false);
        mockHealthService1
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code == "stable"), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        var healthyBefore = service.GetHealthyDestinations(mockHealthService1.Object);

        // 第二次：目标已恢复
        var mockHealthService2 = new Mock<IHealthCalculationService>();
        mockHealthService2
            .Setup(h => h.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        var healthyAfter = service.GetHealthyDestinations(mockHealthService2.Object);

        // Assert
        Assert.Single(healthyBefore);
        Assert.Equal(2, healthyAfter.Count);
        Assert.Contains(healthyAfter, d => d.Code == "recovered");
    }

    /// <summary>
    /// 测试：部分故障场景下的逐步恢复
    /// 场景：从 3 个目标宕 2 个 → 逐步恢复 → 最终全部恢复
    /// </summary>
    [Fact]
    public void Should_Handle_Partial_Recovery_Scenario()
    {
        // Arrange
        var service = new Service("partial-recovery", "Partial Recovery Service");
        service.AddDestination(new Destination("dest-1", Protocol.Http, "host1", 8001));
        service.AddDestination(new Destination("dest-2", Protocol.Http, "host2", 8002));
        service.AddDestination(new Destination("dest-3", Protocol.Http, "host3", 8003));

        // 阶段 1: 只有 dest-1 健康
        var mockHealth1 = new Mock<IHealthCalculationService>();
        mockHealth1
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code == "dest-1"), It.IsAny<HealthCheckConfig>()))
            .Returns(true);
        mockHealth1
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code != "dest-1"), It.IsAny<HealthCheckConfig>()))
            .Returns(false);

        var healthy1 = service.GetHealthyDestinations(mockHealth1.Object);

        // 阶段 2: dest-1 和 dest-2 健康
        var mockHealth2 = new Mock<IHealthCalculationService>();
        mockHealth2
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code == "dest-3"), It.IsAny<HealthCheckConfig>()))
            .Returns(false);
        mockHealth2
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code != "dest-3"), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        var healthy2 = service.GetHealthyDestinations(mockHealth2.Object);

        // 阶段 3: 全部健康
        var mockHealth3 = new Mock<IHealthCalculationService>();
        mockHealth3
            .Setup(h => h.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        var healthy3 = service.GetHealthyDestinations(mockHealth3.Object);

        // Assert
        Assert.Single(healthy1);
        Assert.Equal(2, healthy2.Count);
        Assert.Equal(3, healthy3.Count);
    }

    #endregion

    #region 5. 健康检查禁用场景

    /// <summary>
    /// 测试：禁用主动健康检查时所有目标视为健康
    /// 场景：activeEnabled=false → 所有目标都视为健康
    /// </summary>
    [Fact]
    public void Should_Treat_All_As_Healthy_When_Check_Disabled()
    {
        // Arrange
        var destination = new Destination("any-destination", Protocol.Http, "localhost", 8001);
        var healthCheckConfig = new HealthCheckConfig(activeEnabled: false);
        var healthService = new HealthCalculationService(_mockStorage.Object, _mockCalculatorFactory.Object, _logger);

        // Act
        var isHealthy = healthService.IsHealthy(destination, healthCheckConfig);

        // Assert
        Assert.True(isHealthy);
    }

    /// <summary>
    /// 测试：被禁用的目标自动排除
    /// 场景：服务的目标列表应该过滤掉所有不健康的目标
    /// </summary>
    [Fact]
    public void Should_Exclude_Disabled_Destinations()
    {
        // Arrange
        var service = new Service("selective-service", "Service with Multiple Destinations");
        service.AddDestination(new Destination("enabled-1", Protocol.Http, "host1", 8001));
        service.AddDestination(new Destination("dest-2", Protocol.Http, "host2", 8002));
        service.AddDestination(new Destination("enabled-2", Protocol.Http, "host3", 8003));

        var mockHealthService = new Mock<IHealthCalculationService>();
        // 只有 enabled-1 和 enabled-2 健康
        mockHealthService
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code == "dest-2"), It.IsAny<HealthCheckConfig>()))
            .Returns(false);
        mockHealthService
            .Setup(h => h.IsHealthy(It.Is<Destination>(d => d.Code != "dest-2"), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        // Act - 获取所有目标
        var allDestinations = service.Destinations;
        var healthyDestinations = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Equal(3, allDestinations.Count);
        Assert.Equal(2, healthyDestinations.Count);
        Assert.DoesNotContain(healthyDestinations, d => d.Code == "dest-2");
    }

    #endregion

    #region 6. 错误条件处理

    /// <summary>
    /// 测试：空目标列表的安全处理
    /// 场景：服务没有配置目标 → GetHealthyDestinations 返回空列表
    /// </summary>
    [Fact]
    public void Should_Handle_Empty_Destination_List()
    {
        // Arrange
        var service = new Service("empty-service", "Service with No Destinations");

        var mockHealthService = new Mock<IHealthCalculationService>();
        mockHealthService
            .Setup(h => h.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        // Act
        var healthyDestinations = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Empty(healthyDestinations);
    }

    /// <summary>
    /// 测试：各种协议的健康检查支持
    /// 场景：HTTP 和 HTTPS 目标都能正确处理
    /// </summary>
    [Fact]
    public void Should_Support_Both_Http_And_Https_Protocols()
    {
        // Arrange
        var service = new Service("mixed-protocol-service", "Mixed Protocol Service");
        service.AddDestination(new Destination("http-dest", Protocol.Http, "api.local", 80));
        service.AddDestination(new Destination("https-dest", Protocol.Https, "api.secure", 443));

        var mockHealthService = new Mock<IHealthCalculationService>();
        mockHealthService
            .Setup(h => h.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        // Act
        var healthyDestinations = service.GetHealthyDestinations(mockHealthService.Object);

        // Assert
        Assert.Equal(2, healthyDestinations.Count);
        Assert.Contains(healthyDestinations, d => d.Protocol == Protocol.Http);
        Assert.Contains(healthyDestinations, d => d.Protocol == Protocol.Https);
    }

    #endregion
}

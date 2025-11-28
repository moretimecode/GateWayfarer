using Domain.Services;
using Domain.Services.Configuration;
using Domain.Services.Services;
using Moq;
using Xunit;

namespace Tests.Unit.Domain.Services;

/// <summary>
/// Service 聚合根单元测试
/// </summary>
public class ServiceTests
{
    #region 构造函数测试

    [Fact]
    public void Constructor_ValidParameters_CreatesService()
    {
        // Arrange & Act
        var service = new Service(
            code: "user-service",
            name: "User Service",
            healthCheck: null,
            destinations: null,
            protocolConfig: null
        );

        // Assert
        Assert.Equal("user-service", service.Code);
        Assert.Equal("User Service", service.Name);
        Assert.Empty(service.Destinations);
        Assert.NotNull(service.HealthCheck); // ✅ 默认会创建 HealthCheckConfig
        Assert.NotNull(service.ProtocolConfig); // ✅ 默认会创建 ServiceProtocolConfig
    }

    [Fact]
    public void Constructor_WithHealthCheck_SetsHealthCheck()
    {
        // Arrange
        var healthCheck = new HealthCheckConfig(
            activeEnabled: true,
            activeIntervalSeconds: 10,
            activeTimeoutSeconds: 5,
            activePath: "/health",
            failureThreshold: 3,
            successThreshold: 2
        );

        // Act
        var service = new Service(
            code: "user-service",
            name: "User Service",
            healthCheck: healthCheck,
            destinations: null,
            protocolConfig: null
        );

        // Assert
        Assert.NotNull(service.HealthCheck);
        Assert.Equal("/health", service.HealthCheck.ActivePath);
        Assert.Equal(10, service.HealthCheck.ActiveIntervalSeconds);
    }

    [Fact]
    public void Constructor_WithDestinations_AddsDestinations()
    {
        // Arrange
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "localhost", 5001),
            CreateDestination("dest-2", "localhost", 5002)
        };

        // Act
        var service = new Service(
            code: "user-service",
            name: "User Service",
            healthCheck: null,
            destinations: destinations,
            protocolConfig: null
        );

        // Assert
        Assert.Equal(2, service.Destinations.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidCode_ThrowsArgumentException(string? invalidCode)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Service(invalidCode!, "Service Name", null, null, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Service("service-code", invalidName!, null, null, null));
    }

    #endregion

    #region AddDestination 测试

    [Fact]
    public void AddDestination_ValidDestination_Adds()
    {
        // Arrange
        var service = CreateService();
        var destination = CreateDestination("dest-1", "localhost", 5001);

        // Act
        service.AddDestination(destination);

        // Assert
        Assert.Single(service.Destinations);
        Assert.Equal("dest-1", service.Destinations.First().Code);
    }

    [Fact]
    public void AddDestination_MultipleDestinations_AddsAll()
    {
        // Arrange
        var service = CreateService();
        var dest1 = CreateDestination("dest-1", "localhost", 5001);
        var dest2 = CreateDestination("dest-2", "localhost", 5002);
        var dest3 = CreateDestination("dest-3", "localhost", 5003);

        // Act
        service.AddDestination(dest1);
        service.AddDestination(dest2);
        service.AddDestination(dest3);

        // Assert
        Assert.Equal(3, service.Destinations.Count);
    }

    [Fact]
    public void AddDestination_DuplicateCode_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var destination1 = CreateDestination("dest-1", "localhost", 5001);
        var destination2 = CreateDestination("dest-1", "localhost", 5002);
        service.AddDestination(destination1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            service.AddDestination(destination2));
    }

    [Fact]
    public void AddDestination_NullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            service.AddDestination(null!));
    }

    #endregion

    #region RemoveDestination 测试

    [Fact]
    public void RemoveDestination_ExistingDestination_Removes()
    {
        // Arrange
        var service = CreateService();
        var destination = CreateDestination("dest-1", "localhost", 5001);
        service.AddDestination(destination);

        // Act
        var result = service.RemoveDestination("dest-1");

        // Assert
        Assert.True(result);
        Assert.Empty(service.Destinations);
    }

    [Fact]
    public void RemoveDestination_NonExistentDestination_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var destination = CreateDestination("dest-1", "localhost", 5001);
        service.AddDestination(destination);

        // Act
        var result = service.RemoveDestination("non-existent");

        // Assert
        Assert.False(result);
        Assert.Single(service.Destinations);
    }

    [Fact]
    public void RemoveDestination_EmptyService_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.RemoveDestination("dest-1");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region UpdateDestinations 测试（批量更新）

    [Fact]
    public void UpdateDestinations_ValidList_ReplacesAll()
    {
        // Arrange
        var service = CreateService();
        service.AddDestination(CreateDestination("dest-1", "localhost", 5001));
        service.AddDestination(CreateDestination("dest-2", "localhost", 5002));

        var newDestinations = new List<Destination>
        {
            CreateDestination("dest-3", "localhost", 5003),
            CreateDestination("dest-4", "localhost", 5004)
        };

        // Act
        service.UpdateDestinations(newDestinations);

        // Assert
        Assert.Equal(2, service.Destinations.Count);
        Assert.Contains(service.Destinations, d => d.Code == "dest-3");
        Assert.Contains(service.Destinations, d => d.Code == "dest-4");
        Assert.DoesNotContain(service.Destinations, d => d.Code == "dest-1");
    }

    [Fact]
    public void UpdateDestinations_DuplicateCodes_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "localhost", 5001),
            CreateDestination("dest-1", "localhost", 5002) // 重复
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            service.UpdateDestinations(destinations));
    }

    [Fact]
    public void UpdateDestinations_NullList_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            service.UpdateDestinations(null!));
    }

    #endregion

    #region GetHealthyDestinations 测试

    [Fact]
    public void GetHealthyDestinations_AllHealthy_ReturnsAll()
    {
        // Arrange
        var service = CreateService();
        var dest1 = CreateDestination("dest-1", "localhost", 5001);
        var dest2 = CreateDestination("dest-2", "localhost", 5002);
        var dest3 = CreateDestination("dest-3", "localhost", 5003);

        service.AddDestination(dest1);
        service.AddDestination(dest2);
        service.AddDestination(dest3);

        var healthCalculationService = new Mock<IHealthCalculationService>();
        healthCalculationService
            .Setup(x => x.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        // Act
        var result = service.GetHealthyDestinations(healthCalculationService.Object);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void GetHealthyDestinations_SomeUnhealthy_ReturnsHealthyOnly()
    {
        // Arrange
        var service = CreateService();
        var dest1 = CreateDestination("dest-1", "localhost", 5001);
        var dest2 = CreateDestination("dest-2", "localhost", 5002);
        var dest3 = CreateDestination("dest-3", "localhost", 5003);

        service.AddDestination(dest1);
        service.AddDestination(dest2);
        service.AddDestination(dest3);

        var healthCalculationService = new Mock<IHealthCalculationService>();
        healthCalculationService
            .Setup(x => x.IsHealthy(dest1, It.IsAny<HealthCheckConfig>()))
            .Returns(true);
        healthCalculationService
            .Setup(x => x.IsHealthy(dest2, It.IsAny<HealthCheckConfig>()))
            .Returns(false);
        healthCalculationService
            .Setup(x => x.IsHealthy(dest3, It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        // Act
        var result = service.GetHealthyDestinations(healthCalculationService.Object);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, d => d.Code == "dest-1");
        Assert.Contains(result, d => d.Code == "dest-3");
        Assert.DoesNotContain(result, d => d.Code == "dest-2");
    }

    [Fact]
    public void GetHealthyDestinations_AllUnhealthy_ReturnsEmpty()
    {
        // Arrange
        var service = CreateService();
        var dest1 = CreateDestination("dest-1", "localhost", 5001);
        var dest2 = CreateDestination("dest-2", "localhost", 5002);

        service.AddDestination(dest1);
        service.AddDestination(dest2);

        var healthCalculationService = new Mock<IHealthCalculationService>();
        healthCalculationService
            .Setup(x => x.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns(false);

        // Act
        var result = service.GetHealthyDestinations(healthCalculationService.Object);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetHealthyDestinations_NoDestinations_ReturnsEmpty()
    {
        // Arrange
        var service = CreateService();
        var healthCalculationService = new Mock<IHealthCalculationService>();

        // Act
        var result = service.GetHealthyDestinations(healthCalculationService.Object);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetHealthyDestinations_NullHealthService_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            service.GetHealthyDestinations(null!));
    }

    #endregion

    #region UpdateHealthCheckConfig 测试

    [Fact]
    public void UpdateHealthCheckConfig_ValidConfig_Updates()
    {
        // Arrange
        var service = CreateService();
        var healthCheck = new HealthCheckConfig(
            activeEnabled: true,
            activeIntervalSeconds: 10,
            activeTimeoutSeconds: 5,
            activePath: "/custom-health",
            failureThreshold: 5,
            successThreshold: 3
        );

        // Act
        service.UpdateHealthCheckConfig(healthCheck);

        // Assert
        Assert.NotNull(service.HealthCheck);
        Assert.Equal("/custom-health", service.HealthCheck.ActivePath);
        Assert.Equal(10, service.HealthCheck.ActiveIntervalSeconds);
        Assert.Equal(5, service.HealthCheck.FailureThreshold);
    }

    [Fact]
    public void UpdateHealthCheckConfig_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            service.UpdateHealthCheckConfig(null!));
    }

    #endregion

    #region UpdateProtocolConfig 测试

    [Fact]
    public void UpdateProtocolConfig_ValidConfig_Updates()
    {
        // Arrange
        var service = CreateService();
        var protocolConfig = ServiceProtocolConfig.CreateHttp11Only();

        // Act
        service.UpdateProtocolConfig(protocolConfig);

        // Assert
        Assert.NotNull(service.ProtocolConfig);
        Assert.Contains(ProtocolType.Http11, service.ProtocolConfig.SupportedProtocols);
    }

    [Fact]
    public void UpdateProtocolConfig_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            service.UpdateProtocolConfig(null!));
    }

    #endregion

    #region GetDestination 测试

    [Fact]
    public void GetDestination_ExistingDestination_ReturnsDestination()
    {
        // Arrange
        var service = CreateService();
        var destination = CreateDestination("dest-1", "localhost", 5001);
        service.AddDestination(destination);

        // Act
        var result = service.GetDestination("dest-1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dest-1", result.Code);
    }

    [Fact]
    public void GetDestination_NonExistentDestination_ReturnsNull()
    {
        // Arrange
        var service = CreateService();
        var destination = CreateDestination("dest-1", "localhost", 5001);
        service.AddDestination(destination);

        // Act
        var result = service.GetDestination("non-existent");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region HasDestinations 测试

    [Fact]
    public void HasDestinations_WithDestinations_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        service.AddDestination(CreateDestination("dest-1", "localhost", 5001));

        // Act
        var result = service.HasDestinations();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasDestinations_EmptyService_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.HasDestinations();

        // Assert
        Assert.False(result);
    }

    #endregion

    #region WithErrorHandling 测试

    [Fact]
    public void WithErrorHandling_SetsAllConfigs()
    {
        // Arrange
        var service = CreateService();
        var circuitBreaker = CircuitBreakerConfig.Create();
        var retryPolicy = RetryPolicy.Create();
        var fallback = FallbackStrategy.None();
        var timeout = TimeoutConfig.Create();

        // Act
        var result = service.WithErrorHandling(circuitBreaker, retryPolicy, fallback, timeout);

        // Assert
        Assert.Same(service, result); // 返回自身支持链式调用
        Assert.NotNull(service.CircuitBreaker);
        Assert.NotNull(service.RetryPolicy);
        Assert.NotNull(service.FallbackStrategy);
        Assert.NotNull(service.TimeoutConfig);
    }

    [Fact]
    public void WithErrorHandling_NullParameters_UsesDefaults()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.WithErrorHandling();

        // Assert
        Assert.NotNull(service.CircuitBreaker);
        Assert.NotNull(service.RetryPolicy);
        Assert.NotNull(service.FallbackStrategy);
        Assert.NotNull(service.TimeoutConfig);
    }

    #endregion

    #region ValidateErrorHandlingConfig 测试

    [Fact]
    public void ValidateErrorHandlingConfig_AllConfigsSet_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        service.WithErrorHandling();

        // Act & Assert
        service.ValidateErrorHandlingConfig(); // 不应抛出异常
    }

    [Fact]
    public void ValidateErrorHandlingConfig_MissingConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        // 不调用 WithErrorHandling

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            service.ValidateErrorHandlingConfig());
    }

    #endregion

    #region Helper Methods

    private Service CreateService(string code = "test-service", string name = "Test Service")
    {
        return new Service(code, name, null, null, null);
    }

    private Destination CreateDestination(string code, string host, int port)
    {
        return new Destination(
            code: code,
            protocol: Protocol.Http,
            host: host,
            port: port,
            weight: 100,
            timeoutSeconds: 30,
            healthPath: null,
            metadata: null
        );
    }

    #endregion
}
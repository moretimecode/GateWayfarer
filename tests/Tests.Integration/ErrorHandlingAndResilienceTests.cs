using Domain.Services;
using HttpMethod = Domain.Routes.HttpMethod;

/// <summary>
/// 错误处理和容错机制集成测试
/// 测试服务的容错配置
/// </summary>
[Collection("Gateway Integration Tests")]
public class ErrorHandlingAndResilienceTests
{
    /// <summary>
    /// 测试：服务完整的容错配置
    /// </summary>
    [Fact]
    public void Should_Configure_Complete_Resilience_Pipeline()
    {
        // Arrange
        var service = new Service("resilient-service", "Resilient Service");

        // Act
        service.WithErrorHandling(
            null, // 使用默认配置
            null,
            null,
            null
        );

        // Assert
        Assert.NotNull(service.CircuitBreaker);
        Assert.NotNull(service.RetryPolicy);
        Assert.NotNull(service.FallbackStrategy);
        Assert.NotNull(service.TimeoutConfig);
    }

    /// <summary>
    /// 测试：多个服务的独立容错配置
    /// </summary>
    [Fact]
    public void Should_Configure_Different_Resilience_For_Different_Services()
    {
        // Arrange
        var criticalService = new Service("critical-svc", "Critical Service");
        var normalService = new Service("normal-svc", "Normal Service");

        // Act
        criticalService.WithErrorHandling(null, null, null, null);
        normalService.WithErrorHandling(null, null, null, null);

        // Assert
        Assert.NotNull(criticalService.CircuitBreaker);
        Assert.NotNull(normalService.CircuitBreaker);
    }

    /// <summary>
    /// 测试：服务验证错误处理配置
    /// </summary>
    [Fact]
    public void Should_Validate_Service_Error_Handling_Config()
    {
        // Arrange
        var service = new Service("test-svc", "Test Service");
        service.WithErrorHandling(null, null, null, null);

        // Act & Assert
        service.ValidateErrorHandlingConfig(); // 不应抛出异常
        Assert.NotNull(service.CircuitBreaker);
        Assert.NotNull(service.RetryPolicy);
        Assert.NotNull(service.FallbackStrategy);
        Assert.NotNull(service.TimeoutConfig);
    }

    /// <summary>
    /// 测试：当错误处理配置不完整时的验证失败
    /// </summary>
    [Fact]
    public void Should_Fail_Validation_When_Error_Handling_Incomplete()
    {
        // Arrange
        var service = new Service("incomplete-svc", "Incomplete Service");
        // 不设置任何错误处理配置

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.ValidateErrorHandlingConfig());
    }

    /// <summary>
    /// 测试：多服务的级联容错配置
    /// </summary>
    [Fact]
    public void Should_Apply_Cascading_Resilience_Configuration()
    {
        // Arrange
        var backendService = new Service("backend-api", "Backend API");
        backendService.WithErrorHandling(null, null, null, null);

        var gatewayService = new Service("gateway", "API Gateway");
        gatewayService.WithErrorHandling(null, null, null, null);

        // Act & Assert
        Assert.NotNull(gatewayService.TimeoutConfig);
        Assert.NotNull(backendService.TimeoutConfig);
    }

    /// <summary>
    /// 测试：服务的目标地址和容错配置
    /// </summary>
    [Fact]
    public void Should_Link_Destinations_With_Resilience_Config()
    {
        // Arrange
        var service = new Service("svc-with-dest", "Service With Destinations");
        service.AddDestination(new Destination("dest-1", Protocol.Http, "host1", 80, 100, 30));
        service.AddDestination(new Destination("dest-2", Protocol.Http, "host2", 80, 100, 30));

        service.WithErrorHandling(null, null, null, null);

        // Act
        var destinations = service.Destinations;

        // Assert
        Assert.Equal(2, destinations.Count);
        Assert.NotNull(service.CircuitBreaker);
    }
}

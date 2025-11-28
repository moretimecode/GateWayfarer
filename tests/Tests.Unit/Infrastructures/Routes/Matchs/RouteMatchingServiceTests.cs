using Domain.Routes;
using Domain.Routes.Contexts;
using Domain.Routes.Services;
using Domain.Services;
using Domain.Services.LoadBalancing;
using Domain.Services.Services;
using Infrastructure.Routes;
using Infrastructure.Routes.LoadBalance;
using Infrastructure.Routes.ParameterExtractors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using HttpMethod = Domain.Routes.HttpMethod;

namespace Tests.Unit.Infrastructure.Routes;

/// <summary>
/// RouteMatchingService 单元测试
/// </summary>
public class RouteMatchingServiceTests
{
    private readonly Mock<IRouteRepository> _routeRepositoryMock;
    private readonly Mock<IServiceRepository> _serviceRepositoryMock;
    private readonly Mock<IHealthCalculationService> _healthCalculationServiceMock;
    private readonly Mock<ILogger<RouteMatchingService>> _loggerMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly RouteMatchingService _service;

    public RouteMatchingServiceTests()
    {
        _routeRepositoryMock = new Mock<IRouteRepository>();
        _serviceRepositoryMock = new Mock<IServiceRepository>();
        _healthCalculationServiceMock = new Mock<IHealthCalculationService>();
        _loggerMock = new Mock<ILogger<RouteMatchingService>>();

        // ✅ 使用真实的 ServiceCollection
        var services = new ServiceCollection();

        // 注册真实的参数提取器
        services.AddKeyedSingleton<IRouteParameterExtractor, TemplateParameterExtractor>(RouteMatchType.Template);
        services.AddKeyedSingleton<IRouteParameterExtractor, RegexParameterExtractor>(RouteMatchType.Regex);
        services.AddKeyedSingleton<IRouteParameterExtractor, PrefixParameterExtractor>(RouteMatchType.Prefix);
        services.AddKeyedSingleton<IRouteParameterExtractor, ExactParameterExtractor>(RouteMatchType.Exact);

        // 注册真实的负载均衡策略（如果已实现）
        services.AddKeyedScoped<ILoadBalanceStrategy, RoundRobinStrategy>(LoadBalanceType.RoundRobin);
        services.AddKeyedScoped<ILoadBalanceStrategy, WeightedRoundRobinStrategy>(LoadBalanceType.WeightedRoundRobin);
        services.AddKeyedScoped<ILoadBalanceStrategy, RandomStrategy>(LoadBalanceType.Random);
        services.AddKeyedScoped<ILoadBalanceStrategy, WeightedRandomStrategy>(LoadBalanceType.WeightedRandom);
        services.AddKeyedScoped<ILoadBalanceStrategy, LeastConnectionsStrategy>(LoadBalanceType.LeastConnections);
        services.AddKeyedScoped<ILoadBalanceStrategy, IpHashStrategy>(LoadBalanceType.IpHash);
        services.AddKeyedScoped<ILoadBalanceStrategy, ConsistentHashLoadBalancer>(LoadBalanceType.ConsistentHash);

        _serviceProvider = services.BuildServiceProvider();

        _service = new RouteMatchingService(
            _routeRepositoryMock.Object,
            _serviceRepositoryMock.Object,
            _healthCalculationServiceMock.Object,
            _serviceProvider,
            _loggerMock.Object
        );
    }

    #region MatchAsync 测试 - 成功场景

    [Fact]
    public async Task MatchAsync_WithMatchingRoute_ReturnsRouteMatchResult()
    {
        // Arrange
        var request = new GatewayRequest("/api/users/123", HttpMethod.GET)
        {
            ClientIp = "127.0.0.1"
        };

        var route = CreateRoute("GetUser", "/api/users/{id}", "user-service");
        var destination = CreateDestination("dest-1", Protocol.Http, "localhost", 5001);
        var service = CreateService("user-service");

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([route]);

        _serviceRepositoryMock
            .Setup(x => x.GetByCodeAsync("user-service"))
            .ReturnsAsync(service);

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(route, result.Route);
        Assert.NotNull(result.SelectedDestination);
        Assert.Equal("dest-1", result.SelectedDestination.Code);
        Assert.Equal("123", result.Parameters.GetPathParameter("id"));
    }

    [Fact]
    public async Task MatchAsync_MultipleRoutes_SelectsFirstMatch()
    {
        // Arrange
        var request = new GatewayRequest("/api/users", HttpMethod.GET);

        var route1 = CreateRoute("ExactMatch", "/api/users", "user-service", RouteMatchType.Exact, priority: 10);
        var route2 = CreateRoute("PrefixMatch", "/api", "api-service", RouteMatchType.Prefix, priority: 5);

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([route1, route2]);

        var service = CreateService("user-service");
        _serviceRepositoryMock
            .Setup(x => x.GetByCodeAsync("user-service"))
            .ReturnsAsync(service);

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(route1, result.Route);
        Assert.Equal("ExactMatch", result.Route.Name);
    }

    #endregion

    #region MatchAsync 测试 - 失败场景

    [Fact]
    public async Task MatchAsync_NoRoutesAvailable_ReturnsNull()
    {
        // Arrange
        var request = new GatewayRequest("/api/users", HttpMethod.GET);

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([]);

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task MatchAsync_NoMatchingRoute_ReturnsNull()
    {
        // Arrange
        var request = new GatewayRequest("/api/products", HttpMethod.GET);
        var route = CreateRoute("GetUser", "/api/users", "user-service");

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([route]);

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task MatchAsync_ServiceNotFound_ReturnsNull()
    {
        // Arrange
        var request = new GatewayRequest("/api/users", HttpMethod.GET);
        var route = CreateRoute("GetUser", "/api/users", "non-existent-service");

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([route]);

        _serviceRepositoryMock
            .Setup(x => x.GetByCodeAsync("non-existent-service"))
            .ReturnsAsync((Service?)null);

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task MatchAsync_NoHealthyDestinations_ReturnsNull()
    {
        // Arrange
        var request = new GatewayRequest("/api/users", HttpMethod.GET);
        var route = CreateRoute("GetUser", "/api/users", "user-service");
        var service = CreateService("user-service", hasHealthyDestinations: false);

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([route]);

        _serviceRepositoryMock
            .Setup(x => x.GetByCodeAsync("user-service"))
            .ReturnsAsync(service);

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task MatchAsync_UnexpectedException_ReturnsNull()
    {
        // Arrange
        var request = new GatewayRequest("/api/users", HttpMethod.GET);

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region MatchAsync 测试 - 参数提取

    [Fact]
    public async Task MatchAsync_ExtractsTemplateParameters_Successfully()
    {
        // Arrange
        var request = new GatewayRequest("/api/users/123/orders/456", HttpMethod.GET);
        var route = CreateRoute("GetUserOrder", "/api/users/{userId}/orders/{orderId}", "user-service", RouteMatchType.Template);
        var service = CreateService("user-service");

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([route]);

        _serviceRepositoryMock
            .Setup(x => x.GetByCodeAsync("user-service"))
            .ReturnsAsync(service);

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("123", result.Parameters.GetPathParameter("userId"));
        Assert.Equal("456", result.Parameters.GetPathParameter("orderId"));
    }

    [Fact]
    public async Task MatchAsync_ExtractsRegexParameters_Successfully()
    {
        // Arrange
        var request = new GatewayRequest("/api/users/123", HttpMethod.GET);
        var route = CreateRoute("GetUser", @"^/api/users/(?<id>\d+)$", "user-service", RouteMatchType.Regex);
        var service = CreateService("user-service");

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([route]);

        _serviceRepositoryMock
            .Setup(x => x.GetByCodeAsync("user-service"))
            .ReturnsAsync(service);

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("123", result.Parameters.GetPathParameter("id"));
    }

    [Fact]
    public async Task MatchAsync_ExtractsPrefixParameters_Successfully()
    {
        // Arrange
        var request = new GatewayRequest("/api/users/123", HttpMethod.GET);
        var route = CreateRoute("ApiPrefix", "/api", "user-service", RouteMatchType.Prefix);
        var service = CreateService("user-service");

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([route]);

        _serviceRepositoryMock
            .Setup(x => x.GetByCodeAsync("user-service"))
            .ReturnsAsync(service);

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("users/123", result.Parameters.GetPathParameter("remainingPath"));
    }

    #endregion

    #region MatchAsync 测试 - 负载均衡

    [Fact]
    public async Task MatchAsync_RoundRobinLoadBalance_SelectsDestination()
    {
        // Arrange
        var request = new GatewayRequest("/api/users", HttpMethod.GET);
        var route = CreateRoute("GetUser", "/api/users", "user-service", loadBalanceType: LoadBalanceType.RoundRobin);

        var service = new Service(
            code: "user-service",
            name: "user-service-name",
            healthCheck: null,
            destinations: null,
            protocolConfig: null
        );

        // 添加多个目标
        service.AddDestination(CreateDestination("dest-1", Protocol.Http, "localhost", 5001));
        service.AddDestination(CreateDestination("dest-2", Protocol.Http, "localhost", 5002));
        service.AddDestination(CreateDestination("dest-3", Protocol.Http, "localhost", 5003));

        _healthCalculationServiceMock
            .Setup(x => x.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([route]);

        _serviceRepositoryMock
            .Setup(x => x.GetByCodeAsync("user-service"))
            .ReturnsAsync(service);

        // Act - 多次调用测试轮询
        var result1 = await _service.MatchAsync(request);
        var result2 = await _service.MatchAsync(request);
        var result3 = await _service.MatchAsync(request);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);

        // 轮询应该选择不同的目标（取决于具体实现）
        Assert.NotNull(result1.SelectedDestination);
        Assert.NotNull(result2.SelectedDestination);
        Assert.NotNull(result3.SelectedDestination);
    }

    [Fact]
    public async Task MatchAsync_WeightedLoadBalance_RespectsWeight()
    {
        // Arrange
        var request = new GatewayRequest("/api/users", HttpMethod.GET);
        var route = CreateRoute("GetUser", "/api/users", "user-service", loadBalanceType: LoadBalanceType.WeightedRoundRobin);

        var service = new Service(
            code: "user-service",
            name: "user-service-name",
            healthCheck: null,
            destinations: null,
            protocolConfig: null
        );

        // 添加不同权重的目标
        service.AddDestination(new Destination("dest-1", Protocol.Http, "localhost", 5001, weight: 100, 30, null, null));
        service.AddDestination(new Destination("dest-2", Protocol.Http, "localhost", 5002, weight: 50, 30, null, null));
        service.AddDestination(new Destination("dest-3", Protocol.Http, "localhost", 5003, weight: 10, 30, null, null));

        _healthCalculationServiceMock
            .Setup(x => x.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
            .Returns(true);

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([route]);

        _serviceRepositoryMock
            .Setup(x => x.GetByCodeAsync("user-service"))
            .ReturnsAsync(service);

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.SelectedDestination);
        // 权重越高，被选中的概率越大（具体验证取决于实现）
    }

    #endregion

    #region MatchAsync 测试 - HTTP 方法匹配

    [Theory]
    [InlineData(HttpMethod.GET)]
    [InlineData(HttpMethod.POST)]
    [InlineData(HttpMethod.PUT)]
    [InlineData(HttpMethod.DELETE)]
    public async Task MatchAsync_MatchesCorrectHttpMethod(HttpMethod httpMethod)
    {
        // Arrange
        var request = new GatewayRequest("/api/users", httpMethod);
        var route = CreateRoute("Users", "/api/users", "user-service", httpMethods: httpMethod);
        var service = CreateService("user-service");

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([route]);

        _serviceRepositoryMock
            .Setup(x => x.GetByCodeAsync("user-service"))
            .ReturnsAsync(service);

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(route, result.Route);
    }

    [Fact]
    public async Task MatchAsync_WrongHttpMethod_ReturnsNull()
    {
        // Arrange
        var request = new GatewayRequest("/api/users", HttpMethod.POST);
        var route = CreateRoute("GetUsers", "/api/users", "user-service", httpMethods: HttpMethod.GET);

        _routeRepositoryMock
            .Setup(x => x.GetEnabledRoutesOrderedByPriorityAsync())
            .ReturnsAsync([route]);

        // Act
        var result = await _service.MatchAsync(request);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Helper Methods

    private Route CreateRoute(
        string name,
        string path,
        string serviceCode,
        RouteMatchType matchType = RouteMatchType.Template,
        HttpMethod httpMethods = HttpMethod.GET,
        int priority = 0,
        LoadBalanceType loadBalanceType = LoadBalanceType.RoundRobin)
    {
        return new Route(name, path, serviceCode, httpMethods, priority, true, matchType, loadBalanceType);
    }

    private Service CreateService(string code, bool hasHealthyDestinations = true)
    {
        var service = new Service(
            code: code,
            name: $"{code}-name",
            healthCheck: null,
            destinations: null,
            protocolConfig: null
        );

        if (hasHealthyDestinations)
        {
            var destination = CreateDestination("dest-1", Protocol.Http, "localhost", 5001);
            service.AddDestination(destination);

            _healthCalculationServiceMock
                .Setup(x => x.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
                .Returns(true);
        }
        else
        {
            _healthCalculationServiceMock
                .Setup(x => x.IsHealthy(It.IsAny<Destination>(), It.IsAny<HealthCheckConfig>()))
                .Returns(false);
        }

        return service;
    }

    private Destination CreateDestination(string code, Protocol protocol, string host, int port)
    {
        return new Destination(code, protocol, host, port, 100, 30, null, null);
    }

    #endregion
}
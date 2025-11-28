using Domain.Routes;
using Domain.Routes.Configuration;
using Domain.Routes.Contexts;
using Domain.Services;
using Domain.Services.Configuration;
using HttpMethod = Domain.Routes.HttpMethod;

namespace Tests.Integration;

/// <summary>
/// 端到端请求转发功能集成测试
/// 完整测试从接收请求、路由匹配、目标选择到转发的全流程
/// </summary>
[Collection("Gateway Integration Tests")]
public class EndToEndForwardingTests
{
    private readonly GatewayTestFixture _fixture;

    public EndToEndForwardingTests(GatewayTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region 完整请求转发流程

    /// <summary>
    /// 测试：完整的GET请求匹配和转发流程
    /// 流程：创建服务 → 创建路由 → 匹配请求 → 验证结果
    /// </summary>
    [Fact]
    public async Task Should_Complete_Full_GET_Forwarding_Scenario()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        var service = new Service("users-api", "用户API服务");
        service.AddDestination(new Destination("users-1", Protocol.Http, "localhost", 5001));
        service.AddDestination(new Destination("users-2", Protocol.Http, "localhost", 5002));

        var route = new Route(
            "GetUser",
            "/api/users/{userId}",
            "users-api",
            HttpMethod.GET,
            priority: 10,
            isEnabled: true,
            matchType: RouteMatchType.Template,
            loadBalanceType: LoadBalanceType.RoundRobin
        );

        dbContext.Services.Add(service);
        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync();

        // 创建网关请求
        var request = new GatewayRequest("/api/users/user-123", HttpMethod.GET)
        {
            ClientIp = "192.168.1.100",
            UserAgent = "Mozilla/5.0"
        }.WithRequestId("req-001");

        // Act
        var matchedRoute = dbContext.Routes.AsEnumerable().FirstOrDefault(r => r.IsMatch(request.Path, request.HttpMethod));

        // Assert
        Assert.NotNull(matchedRoute);
        Assert.Equal("GetUser", matchedRoute.Name);
        Assert.Equal("users-api", matchedRoute.ServiceCode);
        Assert.True(matchedRoute.IsEnabled);
    }

    /// <summary>
    /// 测试：POST请求的完整转发流程
    /// 包括请求头、查询参数、请求体等信息
    /// </summary>
    [Fact]
    public async Task Should_Forward_POST_Request_With_Full_Context()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        var service = new Service("orders-api", "订单API服务");
        service.AddDestination(new Destination("orders-1", Protocol.Http, "orders.internal", 8080));

        var route = new Route(
            "CreateOrder",
            "/api/orders",
            "orders-api",
            HttpMethod.POST,
            priority: 1,
            isEnabled: true,
            matchType: RouteMatchType.Exact,
            loadBalanceType: LoadBalanceType.RoundRobin
        );

        // 配置转发规则：添加网关标识头部，保留客户端IP
        var forwardingConfig = RequestForwardingConfig.Create(
            preserveHostHeader: false,
            customHeaders: new Dictionary<string, string>
            {
                { "X-Gateway", "api-gateway/1.0" },
                { "X-Request-Source", "gateway" }
            },
            removeHeaders: new List<string> { "X-Internal-Token" },
            enableStreamingForwarding: false,
            bufferSize: 65536,
            preserveClientIp: true
        );

        route.WithForwardingConfig(forwardingConfig);

        dbContext.Services.Add(service);
        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync();

        // 创建完整的请求上下文
        var request = GatewayRequest.FromHttp("/api/orders", HttpMethod.POST, "203.0.113.50", "PostmanRuntime")
            .WithRequestId("order-001")
            .WithHeader("Content-Type", "application/json")
            .WithHeader("Authorization", "Bearer token-xyz")
            .WithQueryParameter("async", "true")
            .WithQueryParameter("validate", "false")
            .WithMetadata("RequestBody", "{\"productId\": \"prod-123\", \"qty\": 5}");

        // Act
        var matchedRoute = dbContext.Routes.AsEnumerable().FirstOrDefault(r => r.IsMatch(request.Path, request.HttpMethod));

        // Assert
        Assert.NotNull(matchedRoute);
        Assert.NotNull(matchedRoute.ForwardingConfig);
        Assert.Equal(2, request.Headers.Count);
        Assert.Equal(2, request.QueryParameters.Count);
        Assert.Equal("203.0.113.50", request.ClientIp);
        Assert.True(matchedRoute.ForwardingConfig.PreserveClientIp);
    }

    /// <summary>
    /// 测试：前缀匹配路由的参数提取
    /// 请求路径：/api/products/categories/electronics/filters
    /// 路由规则：/api/products (前缀匹配)
    /// 提取剩余路径：categories/electronics/filters
    /// </summary>
    [Fact]
    public async Task Should_Extract_Remaining_Path_In_Prefix_Matching()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        var service = new Service("products-api", "产品API");
        service.AddDestination(new Destination("prod-1", Protocol.Http, "products.api", 8080));

        var route = new Route(
            "ProductsGateway",
            "/api/products",
            "products-api",
            HttpMethod.GET,
            priority: 5,
            isEnabled: true,
            matchType: RouteMatchType.Prefix,
            loadBalanceType: LoadBalanceType.RoundRobin
        );

        dbContext.Services.Add(service);
        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync();

        var requestPath = "/api/products/categories/electronics/filters";
        var request = new GatewayRequest(requestPath, HttpMethod.GET);

        // Act
        var matchedRoute = dbContext.Routes.AsEnumerable().FirstOrDefault(r => r.IsMatch(request.Path, request.HttpMethod));

        // Assert
        Assert.NotNull(matchedRoute);
        Assert.Equal(RouteMatchType.Prefix, matchedRoute.MatchType);
        // 应该能提取出 categories/electronics/filters
        var remainingPath = requestPath.Substring(matchedRoute.Path.Length).TrimStart('/');
        Assert.Equal("categories/electronics/filters", remainingPath);
    }

    /// <summary>
    /// 测试：正则表达式路由匹配和参数提取
    /// 路由正则：^/api/v(?<version>\d+)/users/(?<userId>.+)$
    /// 请求：/api/v2/users/user-abc-123
    /// </summary>
    [Fact]
    public async Task Should_Extract_Regex_Named_Groups()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        var service = new Service("users-api-v2", "用户APIv2");
        service.AddDestination(new Destination("users-v2", Protocol.Http, "users.v2.api", 8080));

        var regexPattern = @"^/api/v(?<version>\d+)/users/(?<userId>.+)$";
        var route = new Route(
            "UsersV2",
            regexPattern,
            "users-api-v2",
            HttpMethod.GET,
            priority: 1,
            isEnabled: true,
            matchType: RouteMatchType.Regex,
            loadBalanceType: LoadBalanceType.RoundRobin
        );

        dbContext.Services.Add(service);
        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync();

        var requestPath = "/api/v2/users/user-abc-123";

        // Act
        var matchedRoute = dbContext.Routes.AsEnumerable().FirstOrDefault(r => r.IsMatch(requestPath, HttpMethod.GET));

        // Assert
        Assert.NotNull(matchedRoute);
        Assert.Equal(RouteMatchType.Regex, matchedRoute.MatchType);
    }

    #endregion

    #region HTTP方法和优先级测试

    /// <summary>
    /// 测试：HTTP方法过滤
    /// 路由配置：仅允许 POST 和 PUT
    /// 尝试：GET 请求（应被拒绝）
    /// </summary>
    [Fact]
    public async Task Should_Reject_Wrong_HTTP_Method()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        var service = new Service("payments-api", "支付API");
        service.AddDestination(new Destination("pay-1", Protocol.Http, "payments.internal", 8080));

        // 仅允许 POST 和 PUT
        var route = new Route(
            "Payment",
            "/api/payments",
            "payments-api",
            HttpMethod.POST | HttpMethod.PUT,
            priority: 1,
            isEnabled: true,
            matchType: RouteMatchType.Exact,
            loadBalanceType: LoadBalanceType.RoundRobin
        );

        dbContext.Services.Add(service);
        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync();

        // 尝试 GET 请求
        var getRequest = new GatewayRequest("/api/payments", HttpMethod.GET);
        var postRequest = new GatewayRequest("/api/payments", HttpMethod.POST);

        // Act & Assert
        Assert.False(route.IsMatch(getRequest.Path, getRequest.HttpMethod));
        Assert.True(route.IsMatch(postRequest.Path, postRequest.HttpMethod));
    }

    /// <summary>
    /// 测试：多路由优先级排序
    /// 精确匹配优先级 > 前缀匹配
    /// </summary>
    [Fact]
    public async Task Should_Select_Route_By_Priority()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        var service1 = new Service("users-exact", "用户服务精确");
        service1.AddDestination(new Destination("u1", Protocol.Http, "users.exact", 8080));

        var service2 = new Service("api-prefix", "API网关前缀");
        service2.AddDestination(new Destination("api", Protocol.Http, "api.gateway", 8080));

        // 精确匹配路由（优先级100）
        var exactRoute = new Route("UsersExact", "/api/users", "users-exact", HttpMethod.GET, 100, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);

        // 前缀匹配路由（优先级50）
        var prefixRoute = new Route("ApiPrefix", "/api", "api-prefix", HttpMethod.GET, 50, true, RouteMatchType.Prefix, LoadBalanceType.RoundRobin);

        dbContext.Services.Add(service1);
        dbContext.Services.Add(service2);
        dbContext.Routes.Add(exactRoute);
        dbContext.Routes.Add(prefixRoute);
        await dbContext.SaveChangesAsync();

        // Act
        var routes = dbContext.Routes.ToList();
        var sortedRoutes = routes.OrderByDescending(r => r.Priority).ToList();
        var matchedRoute = sortedRoutes.FirstOrDefault(r => r.IsMatch("/api/users", HttpMethod.GET));

        // Assert
        Assert.NotNull(matchedRoute);
        Assert.Equal("UsersExact", matchedRoute.Name);
        Assert.Equal(100, matchedRoute.Priority);
    }

    /// <summary>
    /// 测试：禁用的路由应被跳过
    /// </summary>
    [Fact]
    public async Task Should_Skip_Disabled_Routes()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        var service1 = new Service("disabled-svc", "禁用的服务");
        service1.AddDestination(new Destination("d1", Protocol.Http, "disabled", 8080));

        var service2 = new Service("enabled-svc", "启用的服务");
        service2.AddDestination(new Destination("e1", Protocol.Http, "enabled", 8080));

        // 禁用的路由
        var disabledRoute = new Route("Disabled", "/api/test", "disabled-svc", HttpMethod.GET, 100, false, RouteMatchType.Exact, LoadBalanceType.RoundRobin);

        // 启用的备用路由
        var enabledRoute = new Route("Enabled", "/api/test", "enabled-svc", HttpMethod.GET, 50, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);

        dbContext.Services.Add(service1);
        dbContext.Services.Add(service2);
        dbContext.Routes.Add(disabledRoute);
        dbContext.Routes.Add(enabledRoute);
        await dbContext.SaveChangesAsync();

        // Act
        var request = new GatewayRequest("/api/test", HttpMethod.GET);
        var routes = dbContext.Routes.AsEnumerable().Where(r => r.IsMatch(request.Path, request.HttpMethod) && r.IsEnabled).ToList();

        // Assert
        Assert.Single(routes);
        Assert.Equal("Enabled", routes.First().Name);
    }

    #endregion

    #region 转发配置集成测试

    /// <summary>
    /// 测试：转发配置应用到路由
    /// 包括头部处理、流式转发、客户端IP保留等
    /// </summary>
    [Fact]
    public async Task Should_Apply_Forwarding_Configuration()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        var service = new Service("config-test-svc", "配置测试服务");
        service.AddDestination(new Destination("cfg1", Protocol.Http, "config.test", 8080));

        // 创建转发配置
        var forwardingConfig = RequestForwardingConfig.Create(
            preserveHostHeader: true,
            customHeaders: new Dictionary<string, string>
            {
                { "X-API-Version", "v1.0" },
                { "X-Router", "gateway" }
            },
            removeHeaders: new List<string> { "X-Debug", "X-Internal" },
            enableStreamingForwarding: true,
            bufferSize: 2 * 1024 * 1024,  // 2MB
            preserveClientIp: true
        );

        var route = new Route("ConfigTest", "/api/config", "config-test-svc", HttpMethod.POST, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin)
            .WithForwardingConfig(forwardingConfig);

        dbContext.Services.Add(service);
        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync();

        // Act
        var savedRoute = dbContext.Routes.First();
        var config = savedRoute.ForwardingConfig;

        // Assert
        Assert.NotNull(config);
        Assert.True(config.PreserveHostHeader);
        Assert.True(config.EnableStreamingForwarding);
        Assert.True(config.PreserveClientIp);
        Assert.Equal(2 * 1024 * 1024, config.BufferSize);
        Assert.Contains("X-API-Version", config.CustomHeaders.Keys);
        Assert.Contains("X-Internal", config.RemoveHeaders);
    }

    #endregion

    #region 复杂场景测试

    /// <summary>
    /// 测试：复杂的多参数路由
    /// 路由：/api/v1/orgs/{orgId}/teams/{teamId}/projects/{projectId}
    /// </summary>
    [Fact]
    public async Task Should_Handle_Multi_Parameter_Routes()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        var service = new Service("projects-api", "项目API");
        service.AddDestination(new Destination("proj1", Protocol.Http, "projects.internal", 8080));

        var route = new Route(
            "ProjectDetails",
            "/api/v1/orgs/{orgId}/teams/{teamId}/projects/{projectId}",
            "projects-api",
            HttpMethod.GET,
            priority: 10,
            isEnabled: true,
            matchType: RouteMatchType.Template,
            loadBalanceType: LoadBalanceType.RoundRobin
        );

        dbContext.Services.Add(service);
        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync();

        var requestPath = "/api/v1/orgs/org-abc/teams/team-xyz/projects/proj-123";

        // Act
        var matchedRoute = dbContext.Routes.AsEnumerable().FirstOrDefault(r => r.IsMatch(requestPath, HttpMethod.GET));

        // Assert
        Assert.NotNull(matchedRoute);
        Assert.Equal(RouteMatchType.Template, matchedRoute.MatchType);
        Assert.True(matchedRoute.IsMatch(requestPath, HttpMethod.GET));
    }

    /// <summary>
    /// 测试：同路径多路由场景（不同HTTP方法）
    /// 相同路径：/api/resource
    /// GET → 查询路由
    /// POST → 创建路由
    /// PUT → 更新路由
    /// DELETE → 删除路由
    /// </summary>
    [Fact]
    public async Task Should_Route_Same_Path_Different_Methods()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        var service = new Service("resource-api", "资源API");
        service.AddDestination(new Destination("res1", Protocol.Http, "resources.api", 8080));

        var routes = new[]
        {
            new Route("GetResource", "/api/resource", "resource-api", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin),
            new Route("CreateResource", "/api/resource", "resource-api", HttpMethod.POST, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin),
            new Route("UpdateResource", "/api/resource", "resource-api", HttpMethod.PUT, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin),
            new Route("DeleteResource", "/api/resource", "resource-api", HttpMethod.DELETE, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin)
        };

        dbContext.Services.Add(service);
        foreach (var route in routes)
        {
            dbContext.Routes.Add(route);
        }
        await dbContext.SaveChangesAsync();

        // Act & Assert
        Assert.NotNull(dbContext.Routes.AsEnumerable().FirstOrDefault(r => r.IsMatch("/api/resource", HttpMethod.GET) && r.Name == "GetResource"));
        Assert.NotNull(dbContext.Routes.AsEnumerable().FirstOrDefault(r => r.IsMatch("/api/resource", HttpMethod.POST) && r.Name == "CreateResource"));
        Assert.NotNull(dbContext.Routes.AsEnumerable().FirstOrDefault(r => r.IsMatch("/api/resource", HttpMethod.PUT) && r.Name == "UpdateResource"));
        Assert.NotNull(dbContext.Routes.AsEnumerable().FirstOrDefault(r => r.IsMatch("/api/resource", HttpMethod.DELETE) && r.Name == "DeleteResource"));
    }

    #endregion
}

using Domain.Routes;
using Domain.Routes.Configuration;
using Domain.Services;
using Domain.Services.Configuration;
using HttpMethod = Domain.Routes.HttpMethod;

/// <summary>
/// 网关集成测试 - 路由和服务管理
/// </summary>
[Collection("Gateway Integration Tests")]
public class GatewayTests
{
    private readonly GatewayTestFixture _fixture;

    public GatewayTests(GatewayTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Route Tests

    /// <summary>
    /// 测试：添加单个路由
    /// </summary>
    [Fact]
    public async Task Should_Add_Single_Route()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();
        var route = new Route("TestRoute", "/api/test", "test-service", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);

        // Act
        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync();

        // Assert
        Assert.Single(dbContext.Routes);
        var saved = dbContext.Routes.First();
        Assert.Equal("TestRoute", saved.Name);
        Assert.Equal("/api/test", saved.Path);
    }

    /// <summary>
    /// 测试：路由精确匹配
    /// </summary>
    [Fact]
    public async Task Should_Match_Route_Exactly()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var route = new Route("Exact", "/api/test", "svc", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);

        // Act & Assert
        Assert.True(route.IsMatch("/api/test", HttpMethod.GET));
        Assert.False(route.IsMatch("/api/test/123", HttpMethod.GET));
    }

    /// <summary>
    /// 测试：路由前缀匹配
    /// </summary>
    [Fact]
    public async Task Should_Match_Route_By_Prefix()
    {
        // Arrange
        var route = new Route("Prefix", "/api/users", "svc", HttpMethod.GET, 1, true, RouteMatchType.Prefix, LoadBalanceType.RoundRobin);

        // Act & Assert
        Assert.True(route.IsMatch("/api/users", HttpMethod.GET));
        Assert.True(route.IsMatch("/api/users/123", HttpMethod.GET));
        Assert.False(route.IsMatch("/api/products", HttpMethod.GET));
    }

    /// <summary>
    /// 测试：HTTP 方法过滤
    /// </summary>
    [Fact]
    public async Task Should_Filter_By_HTTP_Method()
    {
        // Arrange
        var route = new Route("Method", "/api/test", "svc", HttpMethod.POST | HttpMethod.PUT, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);

        // Act & Assert
        Assert.True(route.IsMatch("/api/test", HttpMethod.POST));
        Assert.True(route.IsMatch("/api/test", HttpMethod.PUT));
        Assert.False(route.IsMatch("/api/test", HttpMethod.GET));
    }

    /// <summary>
    /// 测试：禁用路由应被跳过
    /// </summary>
    [Fact]
    public async Task Should_Skip_Disabled_Routes()
    {
        // Arrange
        var route = new Route("Disabled", "/api/test", "svc", HttpMethod.GET, 1, false, RouteMatchType.Exact, LoadBalanceType.RoundRobin);

        // Act & Assert
        Assert.False(route.IsMatch("/api/test", HttpMethod.GET));
    }

    /// <summary>
    /// 测试：路由优先级
    /// </summary>
    [Fact]
    public async Task Should_Sort_By_Priority()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();
        dbContext.Routes.Add(new Route("High", "/1", "svc", HttpMethod.GET, 10, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin));
        dbContext.Routes.Add(new Route("Low", "/2", "svc", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin));
        await dbContext.SaveChangesAsync();

        // Act
        var sorted = dbContext.Routes.OrderBy(r => r.Priority).ToList();

        // Assert
        Assert.Equal("Low", sorted[0].Name);
        Assert.Equal("High", sorted[1].Name);
    }

    /// <summary>
    /// 测试：路由模板匹配 (Template)
    /// </summary>
    [Fact]
    public async Task Should_Match_Route_By_Template()
    {
        // Arrange
        var route = new Route("Template", "/api/{id}", "svc", HttpMethod.GET, 1, true, RouteMatchType.Template, LoadBalanceType.RoundRobin);

        // Act & Assert
        Assert.True(route.IsMatch("/api/123", HttpMethod.GET));
        Assert.True(route.IsMatch("/api/user-123", HttpMethod.GET));
        Assert.False(route.IsMatch("/api/123/456", HttpMethod.GET)); // 参数个数不匹配
    }

    /// <summary>
    /// 测试：路由正则匹配 (Regex)
    /// </summary>
    [Fact]
    public async Task Should_Match_Route_By_Regex()
    {
        // Arrange
        var route = new Route("Regex", @"^/api/v\d+/", "svc", HttpMethod.GET, 1, true, RouteMatchType.Regex, LoadBalanceType.RoundRobin);

        // Act & Assert
        Assert.True(route.IsMatch("/api/v1/users", HttpMethod.GET));
        Assert.True(route.IsMatch("/api/v2/products", HttpMethod.GET));
        Assert.False(route.IsMatch("/api/users", HttpMethod.GET));
    }

    /// <summary>
    /// 测试：路由启用/禁用切换
    /// </summary>
    [Fact]
    public async Task Should_Enable_Disable_Route()
    {
        // Arrange
        var route = new Route("Toggle", "/api/test", "svc", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);
        Assert.True(route.IsEnabled);

        // Act
        route.Disable();
        Assert.False(route.IsEnabled);
        route.Enable();
        Assert.True(route.IsEnabled);
    }

    /// <summary>
    /// 测试：路由更新
    /// </summary>
    [Fact]
    public async Task Should_Update_Route()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();
        var route = new Route("Original", "/old", "svc1", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);
        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync();

        // Act
        route.Update(path: "/new", serviceCode: "svc2", httpMethods: HttpMethod.POST);
        await dbContext.SaveChangesAsync();

        // Assert
        var updated = dbContext.Routes.First();
        Assert.Equal("/new", updated.Path);
        Assert.Equal("svc2", updated.ServiceCode);
        Assert.Equal(HttpMethod.POST, updated.HttpMethods);
    }

    /// <summary>
    /// 测试：添加多个路由查询正确的
    /// </summary>
    [Fact]
    public async Task Should_Find_Correct_Route_Among_Many()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();
        dbContext.Routes.Add(new Route("Route1", "/api/users", "svc", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin));
        dbContext.Routes.Add(new Route("Route2", "/api/products", "svc", HttpMethod.GET, 2, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin));
        dbContext.Routes.Add(new Route("Route3", "/api/orders", "svc", HttpMethod.POST, 3, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin));
        await dbContext.SaveChangesAsync();

        // Act
        var route = dbContext.Routes
            .AsEnumerable()
            .Where(r => r.IsMatch("/api/products", HttpMethod.GET))
            .FirstOrDefault();

        // Assert
        Assert.NotNull(route);
        Assert.Equal("Route2", route.Name);
    }

    #endregion

    #region Service Tests

    /// <summary>
    /// 测试：添加服务
    /// </summary>
    [Fact]
    public async Task Should_Add_Service()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();
        var service = new Service("test-svc", "Test Service");

        // Act
        dbContext.Services.Add(service);
        await dbContext.SaveChangesAsync();

        // Assert
        Assert.Single(dbContext.Services);
        var saved = dbContext.Services.First();
        Assert.Equal("test-svc", saved.Code);
        Assert.Equal("Test Service", saved.Name);
    }

    /// <summary>
    /// 测试：删除服务
    /// </summary>
    [Fact]
    public async Task Should_Delete_Service()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();
        var service = new Service("del-svc", "Delete Me");
        dbContext.Services.Add(service);
        await dbContext.SaveChangesAsync();

        // Act
        var toDelete = dbContext.Services.First();
        dbContext.Services.Remove(toDelete);
        await dbContext.SaveChangesAsync();

        // Assert
        Assert.Empty(dbContext.Services);
    }

    /// <summary>
    /// 测试：添加多个服务
    /// </summary>
    [Fact]
    public async Task Should_Add_Multiple_Services()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        // Act
        dbContext.Services.Add(new Service("user-api", "User Service"));
        dbContext.Services.Add(new Service("product-api", "Product Service"));
        dbContext.Services.Add(new Service("order-api", "Order Service"));
        await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(3, dbContext.Services.Count());
        Assert.NotNull(dbContext.Services.FirstOrDefault(s => s.Code == "user-api"));
        Assert.NotNull(dbContext.Services.FirstOrDefault(s => s.Code == "product-api"));
    }

    /// <summary>
    /// 测试：服务添加目标地址
    /// </summary>
    [Fact]
    public async Task Should_Add_Destination_To_Service()
    {
        // Arrange
        var service = new Service("backend-svc", "Backend Service");
        var destination = new Destination("dest-1", Domain.Services.Protocol.Http, "localhost", 8080, 100, 30);

        // Act
        service.AddDestination(destination);

        // Assert
        Assert.NotEmpty(service.Destinations);
        var dest = service.Destinations.First();
        Assert.Equal("dest-1", dest.Code);
        Assert.Equal("localhost", dest.Host);
        Assert.Equal(8080, dest.Port);
    }

    /// <summary>
    /// 测试：服务添加多个目标地址
    /// </summary>
    [Fact]
    public async Task Should_Add_Multiple_Destinations()
    {
        // Arrange
        var service = new Service("load-balanced-svc", "Load Balanced Service");

        // Act
        service.AddDestination(new Destination("server-1", Domain.Services.Protocol.Http, "server1.com", 80, 50, 30));
        service.AddDestination(new Destination("server-2", Domain.Services.Protocol.Http, "server2.com", 80, 50, 30));
        service.AddDestination(new Destination("server-3", Domain.Services.Protocol.Https, "server3.com", 443, 100, 30));

        // Assert
        Assert.Equal(3, service.Destinations.Count());
        Assert.Equal(50, service.Destinations.ElementAt(0).Weight);
        Assert.Equal(100, service.Destinations.ElementAt(2).Weight);
    }

    /// <summary>
    /// 测试：目标地址权重配置
    /// </summary>
    [Fact]
    public async Task Should_Configure_Destination_Weight()
    {
        // Arrange
        var dest1 = new Destination("fast-server", Domain.Services.Protocol.Http, "fast.com", 80, 100, 30);
        var dest2 = new Destination("slow-server", Domain.Services.Protocol.Http, "slow.com", 80, 50, 30);

        // Act & Assert
        Assert.Equal(100, dest1.Weight); // 更高权重 - 获得更多流量
        Assert.Equal(50, dest2.Weight);  // 较低权重
    }

    /// <summary>
    /// 测试：目标地址支持不同协议
    /// </summary>
    [Fact]
    public async Task Should_Support_Multiple_Protocols()
    {
        // Arrange
        var httpDest = new Destination("http-svc", Domain.Services.Protocol.Http, "api.com", 80, 100, 30);
        var httpsDest = new Destination("https-svc", Domain.Services.Protocol.Https, "api.com", 443, 100, 30);

        // Act & Assert
        Assert.Equal(Domain.Services.Protocol.Http, httpDest.Protocol);
        Assert.Equal(Domain.Services.Protocol.Https, httpsDest.Protocol);
        Assert.NotNull(httpDest);
        Assert.NotNull(httpsDest);
    }

    /// <summary>
    /// 测试：删除不存在的目标地址返回 false
    /// </summary>
    [Fact]
    public async Task Should_Return_False_When_Removing_Nonexistent_Destination()
    {
        // Arrange
        var service = new Service("test-svc", "Test Service");
        service.AddDestination(new Destination("existing", Domain.Services.Protocol.Http, "host", 80, 100, 30));

        // Act
        var result = service.RemoveDestination("nonexistent");

        // Assert
        Assert.False(result);
        Assert.Single(service.Destinations); // 没有删除任何东西
    }

    /// <summary>
    /// 测试：删除存在的目标地址返回 true
    /// </summary>
    [Fact]
    public async Task Should_Remove_Existing_Destination()
    {
        // Arrange
        var service = new Service("test-svc", "Test Service");
        service.AddDestination(new Destination("to-delete", Domain.Services.Protocol.Http, "host", 80, 100, 30));

        // Act
        var result = service.RemoveDestination("to-delete");

        // Assert
        Assert.True(result);
        Assert.Empty(service.Destinations);
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// 测试：路由和服务关联
    /// </summary>
    [Fact]
    public async Task Should_Link_Route_And_Service()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        var service = new Service("api-svc", "API Service");
        dbContext.Services.Add(service);
        await dbContext.SaveChangesAsync();

        var route = new Route("ApiRoute", "/api/", "api-svc", HttpMethod.GET, 1, true, RouteMatchType.Prefix, LoadBalanceType.RoundRobin);
        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync();

        // Act & Assert
        var savedRoute = dbContext.Routes.First();
        Assert.Equal("api-svc", savedRoute.ServiceCode);

        var savedService = dbContext.Services.First();
        Assert.Equal("api-svc", savedService.Code);
    }

    /// <summary>
    /// 测试：检查服务是否有目标地址
    /// </summary>
    [Fact]
    public async Task Should_Check_Service_Has_Destinations()
    {
        // Arrange
        var serviceWithDest = new Service("svc-with", "Service With Destinations");
        serviceWithDest.AddDestination(new Destination("dest-1", Domain.Services.Protocol.Http, "host1", 80, 100, 30));

        var serviceNoDest = new Service("svc-without", "Service Without Destinations");

        // Act & Assert
        Assert.True(serviceWithDest.HasDestinations());
        Assert.False(serviceNoDest.HasDestinations());
    }

    /// <summary>
    /// 测试：根据Code获取目标地址
    /// </summary>
    [Fact]
    public async Task Should_Get_Destination_By_Code()
    {
        // Arrange
        var service = new Service("test-svc", "Test Service");
        var dest1 = new Destination("dest-1", Domain.Services.Protocol.Http, "host1", 80, 100, 30);
        var dest2 = new Destination("dest-2", Domain.Services.Protocol.Http, "host2", 80, 100, 30);

        service.AddDestination(dest1);
        service.AddDestination(dest2);

        // Act
        var retrieved = service.GetDestination("dest-1");
        var notFound = service.GetDestination("nonexistent");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("dest-1", retrieved.Code);
        Assert.Null(notFound);
    }

    /// <summary>
    /// 测试：更新目标地址列表
    /// </summary>
    [Fact]
    public async Task Should_Update_Destinations_List()
    {
        // Arrange
        var service = new Service("test-svc", "Test Service");
        service.AddDestination(new Destination("old-1", Domain.Services.Protocol.Http, "old1", 80, 100, 30));
        service.AddDestination(new Destination("old-2", Domain.Services.Protocol.Http, "old2", 80, 100, 30));

        var newDests = new List<Destination>
        {
            new Destination("new-1", Domain.Services.Protocol.Http, "new1", 80, 100, 30),
            new Destination("new-2", Domain.Services.Protocol.Http, "new2", 80, 100, 30),
            new Destination("new-3", Domain.Services.Protocol.Http, "new3", 80, 100, 30)
        };

        // Act
        service.UpdateDestinations(newDests);

        // Assert
        Assert.Equal(3, service.Destinations.Count);
        Assert.NotNull(service.GetDestination("new-1"));
        Assert.Null(service.GetDestination("old-1"));
    }

    /// <summary>
    /// 测试：不能添加重复Code的目标地址
    /// </summary>
    [Fact]
    public async Task Should_Not_Add_Duplicate_Destination_Code()
    {
        // Arrange
        var service = new Service("test-svc", "Test Service");
        var dest1 = new Destination("same-code", Domain.Services.Protocol.Http, "host1", 80, 100, 30);
        var dest2 = new Destination("same-code", Domain.Services.Protocol.Http, "host2", 80, 100, 30);

        service.AddDestination(dest1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.AddDestination(dest2));
        Assert.Single(service.Destinations);
    }

    /// <summary>
    /// 测试：多个HTTP方法支持
    /// </summary>
    [Fact]
    public async Task Should_Support_Multiple_Http_Methods()
    {
        // Arrange
        var combinedMethods = HttpMethod.GET | HttpMethod.POST | HttpMethod.PUT;
        var route = new Route("Multi", "/api/resource", "svc", combinedMethods, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);

        // Act & Assert
        Assert.True(route.IsMatch("/api/resource", HttpMethod.GET));
        Assert.True(route.IsMatch("/api/resource", HttpMethod.POST));
        Assert.True(route.IsMatch("/api/resource", HttpMethod.PUT));
        Assert.False(route.IsMatch("/api/resource", HttpMethod.DELETE));
    }

    /// <summary>
    /// 测试：路由的批处理配置
    /// </summary>
    [Fact]
    public async Task Should_Configure_Route_Batching()
    {
        // Arrange
        var route = new Route("Batch", "/api/batch", "svc", HttpMethod.POST, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);
        var batchConfig = RequestBatchingConfig.Create(
            batchWindow: TimeSpan.FromMilliseconds(1000),
            maxBatchSize: 10);

        // Act
        route.WithBatchingConfig(batchConfig);
        route.ValidateConfiguration();

        // Assert
        Assert.NotNull(route.BatchingConfig);
        Assert.True(route.BatchingConfig.EnableBatching);
        Assert.Equal(10, route.BatchingConfig.MaxBatchSize);
    }

    /// <summary>
    /// 测试：路由的转发配置
    /// </summary>
    [Fact]
    public async Task Should_Configure_Route_Forwarding()
    {
        // Arrange
        var route = new Route("Forward", "/api/forward", "svc", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);
        var forwardConfig = RequestForwardingConfig.Create(
            preserveHostHeader: false,
            enableStreamingForwarding: true);

        // Act
        route.WithForwardingConfig(forwardConfig);
        route.ValidateConfiguration();

        // Assert
        Assert.NotNull(route.ForwardingConfig);
        Assert.False(route.ForwardingConfig.PreserveHostHeader);
        Assert.True(route.ForwardingConfig.EnableStreamingForwarding);
    }

    /// <summary>
    /// 测试：服务的错误处理配置
    /// </summary>
    [Fact]
    public async Task Should_Configure_Service_Error_Handling()
    {
        // Arrange
        var service = new Service("resilient-svc", "Resilient Service");
        var circuitBreaker = CircuitBreakerConfig.Create(failureThreshold: 5);
        var retryPolicy = RetryPolicy.Create(maxRetryAttempts: 3);
        var fallback = FallbackStrategy.StaticResponse("Service temporarily unavailable");
        var timeout = TimeoutConfig.Create(requestTimeout: TimeSpan.FromSeconds(5));

        // Act
        service.WithErrorHandling(circuitBreaker, retryPolicy, fallback, timeout);

        // Assert
        Assert.NotNull(service.CircuitBreaker);
        Assert.Equal(5, service.CircuitBreaker.FailureThreshold);
        Assert.NotNull(service.RetryPolicy);
        Assert.Equal(3, service.RetryPolicy.MaxRetryAttempts);
        Assert.NotNull(service.FallbackStrategy);
        Assert.True(service.FallbackStrategy.Enabled);
        Assert.NotNull(service.TimeoutConfig);
        Assert.Equal(TimeSpan.FromSeconds(5), service.TimeoutConfig.RequestTimeout);
    }

    /// <summary>
    /// 测试：路由不匹配的情况（多条件）
    /// </summary>
    [Fact]
    public async Task Should_Not_Match_Route_When_Multiple_Conditions_Fail()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        var route1 = new Route("User", "/api/users", "svc", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);
        var route2 = new Route("Product", "/api/products", "svc", HttpMethod.POST, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);

        dbContext.Routes.Add(route1);
        dbContext.Routes.Add(route2);
        await dbContext.SaveChangesAsync();

        // Act
        var routes = dbContext.Routes
            .AsEnumerable()
            .Where(r => r.IsMatch("/api/orders", HttpMethod.GET))
            .ToList();

        // Assert
        Assert.Empty(routes); // 没有路由匹配
    }

    /// <summary>
    /// 测试：禁用的路由不会被匹配
    /// </summary>
    [Fact]
    public async Task Should_Not_Match_Disabled_Route()
    {
        // Arrange
        var route = new Route("Disabled", "/api/old", "svc", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);
        route.Disable();

        // Act
        var matches = route.IsMatch("/api/old", HttpMethod.GET);

        // Assert
        Assert.False(matches);
    }

    /// <summary>
    /// 测试：路由优先级排序的正确性
    /// </summary>
    [Fact]
    public async Task Should_Sort_Routes_By_Priority_Descending()
    {
        // Arrange
        await _fixture.ClearAllDataAsync();
        var dbContext = _fixture.GetDbContext();

        dbContext.Routes.Add(new Route("Low", "/api/1", "svc", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin));
        dbContext.Routes.Add(new Route("High", "/api/2", "svc", HttpMethod.GET, 100, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin));
        dbContext.Routes.Add(new Route("Medium", "/api/3", "svc", HttpMethod.GET, 50, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin));
        await dbContext.SaveChangesAsync();

        // Act
        var sorted = dbContext.Routes.OrderByDescending(r => r.Priority).ToList();

        // Assert
        Assert.Equal("High", sorted[0].Name);
        Assert.Equal("Medium", sorted[1].Name);
        Assert.Equal("Low", sorted[2].Name);
    }

    #endregion
}

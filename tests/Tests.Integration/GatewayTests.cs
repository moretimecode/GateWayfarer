using Domain.Routes;
using Domain.Services;
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

    #endregion
}

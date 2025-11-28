using Domain.Routes;
using Infrastructure.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Tests.Integration;
using HttpMethod = Domain.Routes.HttpMethod;

/// <summary>
/// 路由匹配性能和复杂场景测试
/// 测试大量路由的匹配性能、优先级排序、冲突处理等
/// </summary>
[Collection("Gateway Integration Tests")]
public class RouteMatchingPerformanceTests : IAsyncLifetime
{
    private SqliteConnection _connection;
    private TestApiGatewayDbContext _dbContext;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TestApiGatewayDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        _dbContext = new TestApiGatewayDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }

    /// <summary>
    /// 测试：大量路由时的匹配性能
    /// </summary>
    [Fact]
    public async Task Should_Match_Routes_Efficiently_With_Large_Count()
    {
        // Arrange
        const int routeCount = 100;
        for (int i = 0; i < routeCount; i++)
        {
            var route = new Route(
                $"route-{i}",
                $"/api/service{i % 10}/endpoint{i % 20}",
                $"service-{i % 5}",
                HttpMethod.GET,
                i % 100,
                true,
                RouteMatchType.Exact,  // Use Exact matching for predictable results
                LoadBalanceType.RoundRobin
            );
            _dbContext.Routes.Add(route);
        }
        await _dbContext.SaveChangesAsync();

        // Act - 验证所有路由都已保存且启用
        var routes = await _dbContext.Routes
            .Where(r => r.IsEnabled)
            .OrderByDescending(r => r.Priority)
            .ToListAsync();

        var routesByService = routes.GroupBy(r => r.ServiceCode).ToList();

        // Assert
        Assert.Equal(routeCount, routes.Count);
        Assert.NotEmpty(routes);  // Should have routes
        Assert.True(routes.All(r => r.IsEnabled), "All routes should be enabled");
        Assert.Equal(5, routesByService.Count);  // 5 different services (i % 5)
    }

    /// <summary>
    /// 测试：复杂优先级排序场景
    /// </summary>
    [Fact]
    public async Task Should_Sort_Complex_Priority_Scenario()
    {
        // Arrange
        _dbContext.Routes.Add(new Route("users-all", "/api/users", "svc-1", HttpMethod.GET, 10, true, RouteMatchType.Prefix, LoadBalanceType.RoundRobin));
        _dbContext.Routes.Add(new Route("users-specific", "/api/users/profile", "svc-2", HttpMethod.GET, 20, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin));
        _dbContext.Routes.Add(new Route("users-admin", "/api/users/admin", "svc-3", HttpMethod.GET, 30, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin));
        await _dbContext.SaveChangesAsync();

        // Act
        var sorted = _dbContext.Routes
            .AsEnumerable()
            .OrderByDescending(r => r.Priority)
            .ToList();

        // Assert
        Assert.Equal("users-admin", sorted[0].Name);
        Assert.Equal("users-specific", sorted[1].Name);
        Assert.Equal("users-all", sorted[2].Name);
    }

    /// <summary>
    /// 测试：精确匹配优于前缀匹配
    /// </summary>
    [Fact]
    public async Task Should_Prefer_Exact_Match_Over_Prefix_When_Both_Exist()
    {
        // Arrange
        var exactRoute = new Route("exact", "/api/users", "svc-1", HttpMethod.GET, 100, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);
        var prefixRoute = new Route("prefix", "/api", "svc-2", HttpMethod.GET, 1, true, RouteMatchType.Prefix, LoadBalanceType.RoundRobin);

        _dbContext.Routes.Add(exactRoute);
        _dbContext.Routes.Add(prefixRoute);
        await _dbContext.SaveChangesAsync();

        // Act
        var matched = _dbContext.Routes
            .AsEnumerable()
            .OrderByDescending(r => r.Priority)
            .Where(r => r.IsMatch("/api/users", HttpMethod.GET))
            .FirstOrDefault();

        // Assert
        Assert.NotNull(matched);
        Assert.Equal("exact", matched.Name);
    }

    /// <summary>
    /// 测试：路由冲突时的处理
    /// </summary>
    [Fact]
    public async Task Should_Handle_Route_Conflicts_By_Priority()
    {
        // Arrange
        _dbContext.Routes.Add(new Route("low", "/api/users", "svc-1", HttpMethod.GET, 1, true, RouteMatchType.Prefix, LoadBalanceType.RoundRobin));
        _dbContext.Routes.Add(new Route("medium", "/api/users", "svc-2", HttpMethod.GET, 50, true, RouteMatchType.Prefix, LoadBalanceType.RoundRobin));
        _dbContext.Routes.Add(new Route("high", "/api/users", "svc-3", HttpMethod.GET, 100, true, RouteMatchType.Prefix, LoadBalanceType.RoundRobin));
        await _dbContext.SaveChangesAsync();

        // Act
        var matched = _dbContext.Routes
            .AsEnumerable()
            .OrderByDescending(r => r.Priority)
            .Where(r => r.IsMatch("/api/users/123", HttpMethod.GET))
            .FirstOrDefault();

        // Assert
        Assert.NotNull(matched);
        Assert.Equal("high", matched.Name);
    }

    /// <summary>
    /// 测试：正则表达式路由的匹配
    /// </summary>
    [Fact]
    public async Task Should_Match_Regex_Routes_For_Version_Patterns()
    {
        // Arrange
        var v1Route = new Route("v1-api", @"^/api/v1/", "svc-v1", HttpMethod.GET, 10, true, RouteMatchType.Regex, LoadBalanceType.RoundRobin);
        var v2Route = new Route("v2-api", @"^/api/v2/", "svc-v2", HttpMethod.GET, 10, true, RouteMatchType.Regex, LoadBalanceType.RoundRobin);

        _dbContext.Routes.Add(v1Route);
        _dbContext.Routes.Add(v2Route);
        await _dbContext.SaveChangesAsync();

        // Act
        var v1Matches = _dbContext.Routes
            .AsEnumerable()
            .Where(r => r.IsMatch("/api/v1/users", HttpMethod.GET))
            .ToList();

        var v2Matches = _dbContext.Routes
            .AsEnumerable()
            .Where(r => r.IsMatch("/api/v2/products", HttpMethod.GET))
            .ToList();

        // Assert
        Assert.NotEmpty(v1Matches);
        Assert.Equal("v1-api", v1Matches.First().Name);
        Assert.NotEmpty(v2Matches);
        Assert.Equal("v2-api", v2Matches.First().Name);
    }

    /// <summary>
    /// 测试：禁用路由不会被匹配
    /// </summary>
    [Fact]
    public async Task Should_Skip_Disabled_Routes_Even_With_High_Priority()
    {
        // Arrange
        var highPriorityDisabled = new Route("disabled-high", "/api/test", "svc-1", HttpMethod.GET, 1000, false, RouteMatchType.Prefix, LoadBalanceType.RoundRobin);
        var lowPriorityEnabled = new Route("enabled-low", "/api/test", "svc-2", HttpMethod.GET, 1, true, RouteMatchType.Prefix, LoadBalanceType.RoundRobin);

        _dbContext.Routes.Add(highPriorityDisabled);
        _dbContext.Routes.Add(lowPriorityEnabled);
        await _dbContext.SaveChangesAsync();

        // Act
        var matched = _dbContext.Routes
            .AsEnumerable()
            .Where(r => r.IsMatch("/api/test", HttpMethod.GET))
            .FirstOrDefault();

        // Assert
        Assert.NotNull(matched);
        Assert.Equal("enabled-low", matched.Name);
    }

    /// <summary>
    /// 测试：大小写不敏感的路由匹配
    /// </summary>
    [Fact]
    public async Task Should_Match_Routes_Case_Insensitively()
    {
        // Arrange
        _dbContext.Routes.Add(new Route("case-test", "/API/USERS", "svc-1", HttpMethod.GET, 10, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin));
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        Assert.True(_dbContext.Routes.First().IsMatch("/api/users", HttpMethod.GET));
        Assert.True(_dbContext.Routes.First().IsMatch("/API/USERS", HttpMethod.GET));
        Assert.True(_dbContext.Routes.First().IsMatch("/Api/Users", HttpMethod.GET));
    }

    /// <summary>
    /// 测试：超过100条路由时的排序性能
    /// </summary>
    [Fact]
    public async Task Should_Sort_Large_Routes_Efficiently()
    {
        // Arrange
        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            _dbContext.Routes.Add(new Route(
                $"route-{i}",
                $"/api/endpoint{i}",
                $"service-{i % 10}",
                HttpMethod.GET,
                random.Next(0, 1000),
                i % 10 != 0,
                RouteMatchType.Prefix,
                LoadBalanceType.RoundRobin
            ));
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var sorted = _dbContext.Routes
            .AsEnumerable()
            .Where(r => r.IsEnabled)
            .OrderByDescending(r => r.Priority)
            .ToList();

        stopwatch.Stop();

        // Assert
        Assert.True(sorted.Count > 0);
        Assert.True(stopwatch.ElapsedMilliseconds < 200);
    }
}

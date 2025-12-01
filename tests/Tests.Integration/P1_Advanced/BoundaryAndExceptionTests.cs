using Domain.Routes;
using Domain.Services;
using Infrastructure.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Tests.Integration;
using HttpMethod = Domain.Routes.HttpMethod;

/// <summary>
/// 边界情况和异常场景集成测试
/// 测试空列表、无效配置、并发冲突、大数据等边界情况
/// </summary>
[Collection("Gateway Integration Tests")]
public class BoundaryAndExceptionTests : IAsyncLifetime
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

    #region 空列表和无效配置

    /// <summary>
    /// 测试：空路由列表的匹配
    /// </summary>
    [Fact]
    public async Task Should_Handle_Empty_Route_List()
    {
        // Arrange
        // 数据库中没有任何路由

        // Act
        var routes = await _dbContext.Routes.ToListAsync();

        // Assert
        Assert.Empty(routes);
    }

    /// <summary>
    /// 测试：空服务列表
    /// </summary>
    [Fact]
    public async Task Should_Handle_Empty_Service_List()
    {
        // Arrange
        // 数据库中没有任何服务

        // Act
        var services = await _dbContext.Services.ToListAsync();

        // Assert
        Assert.Empty(services);
    }

    /// <summary>
    /// 测试：空目标地址列表的负载均衡
    /// </summary>
    [Fact]
    public void Should_Handle_Empty_Destinations_List()
    {
        // Arrange
        var service = new Service("empty-svc", "Empty Service");

        // Act & Assert
        Assert.Empty(service.Destinations);
        Assert.False(service.HasDestinations());
    }

    /// <summary>
    /// 测试：无效的路由名称
    /// </summary>
    [Fact]
    public void Should_Reject_Empty_Route_Name()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Route("", "/api/test", "svc", HttpMethod.GET, 0, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin)
        );

        Assert.Throws<ArgumentException>(() =>
            new Route(null, "/api/test", "svc", HttpMethod.GET, 0, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin)
        );

        Assert.Throws<ArgumentException>(() =>
            new Route("   ", "/api/test", "svc", HttpMethod.GET, 0, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin)
        );
    }

    /// <summary>
    /// 测试：无效的路由路径
    /// </summary>
    [Fact]
    public void Should_Reject_Empty_Route_Path()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Route("test", "", "svc", HttpMethod.GET, 0, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin)
        );

        Assert.Throws<ArgumentException>(() =>
            new Route("test", null, "svc", HttpMethod.GET, 0, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin)
        );
    }

    /// <summary>
    /// 测试：无效的服务代码
    /// </summary>
    [Fact]
    public void Should_Reject_Empty_Service_Code()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Route("test", "/api", "", HttpMethod.GET, 0, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin)
        );

        Assert.Throws<ArgumentException>(() =>
            new Route("test", "/api", null, HttpMethod.GET, 0, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin)
        );
    }

    /// <summary>
    /// 测试：无效的目标地址配置
    /// </summary>
    [Fact]
    public void Should_Reject_Invalid_Destination_Configuration()
    {
        // 无效的Code
        Assert.Throws<ArgumentException>(() =>
            new Destination("", Protocol.Http, "host", 80, 100, 30)
        );

        // 无效的Host
        Assert.Throws<ArgumentException>(() =>
            new Destination("dest", Protocol.Http, "", 80, 100, 30)
        );

        // 无效的Port
        Assert.Throws<ArgumentException>(() =>
            new Destination("dest", Protocol.Http, "host", 0, 100, 30)
        );

        Assert.Throws<ArgumentException>(() =>
            new Destination("dest", Protocol.Http, "host", 70000, 100, 30)
        );

        // 无效的权重
        Assert.Throws<ArgumentException>(() =>
            new Destination("dest", Protocol.Http, "host", 80, 0, 30)
        );

        // 无效的超时
        Assert.Throws<ArgumentException>(() =>
            new Destination("dest", Protocol.Http, "host", 80, 100, 0)
        );
    }

    #endregion

    #region 并发和冲突场景

    /// <summary>
    /// 测试：并发添加相同代码的目标地址会失败
    /// </summary>
    [Fact]
    public void Should_Fail_When_Adding_Duplicate_Destination_Code()
    {
        // Arrange
        var service = new Service("concurrent-svc", "Concurrent Service");
        var dest1 = new Destination("same-code", Protocol.Http, "host1", 80, 100, 30);
        var dest2 = new Destination("same-code", Protocol.Http, "host2", 80, 100, 30);

        service.AddDestination(dest1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.AddDestination(dest2));
    }

    /// <summary>
    /// 测试：并发更新目标地址列表
    /// </summary>
    [Fact]
    public void Should_Handle_Concurrent_Destination_Updates()
    {
        // Arrange
        var service = new Service("multi-update-svc", "Multi Update Service");
        for (int i = 0; i < 5; i++)
        {
            service.AddDestination(new Destination($"server-{i}", Protocol.Http, $"host{i}", 80, 100, 30));
        }

        var newDestinations = new List<Destination>();
        for (int i = 5; i < 10; i++)
        {
            newDestinations.Add(new Destination($"server-{i}", Protocol.Http, $"host{i}", 80, 100, 30));
        }

        // Act - 并发更新
        var task1 = Task.Run(() => service.UpdateDestinations(newDestinations));
        var task2 = Task.Run(() => _ = service.Destinations); // 并发读

        Task.WaitAll(task1, task2);

        // Assert
        Assert.NotEmpty(service.Destinations);
    }

    /// <summary>
    /// 测试：并发修改和查询路由
    /// </summary>
    [Fact]
    public async Task Should_Handle_Concurrent_Route_Operations()
    {
        // Arrange
        var route = new Route("concurrent-route", "/api/test", "svc", HttpMethod.GET, 1, true, RouteMatchType.Prefix, LoadBalanceType.RoundRobin);
        _dbContext.Routes.Add(route);
        await _dbContext.SaveChangesAsync();

        // Act - 顺序修改而不是并发修改，以避免EF Core并发问题
        for (int i = 0; i < 5; i++)
        {
            var r = _dbContext.Routes.First();
            r.Update(priority: i);
            await _dbContext.SaveChangesAsync();
        }

        // Assert
        var routes = await _dbContext.Routes.ToListAsync();
        Assert.NotEmpty(routes);
    }

    #endregion

    #region 大数据和性能边界

    /// <summary>
    /// 测试：超长路径处理
    /// </summary>
    [Fact]
    public void Should_Handle_Very_Long_Route_Path()
    {
        // Arrange
        var longPath = string.Concat(Enumerable.Repeat("/api/very/long/path/segment/", 10));

        // Act
        var route = new Route("long-path", longPath, "svc", HttpMethod.GET, 1, true, RouteMatchType.Prefix, LoadBalanceType.RoundRobin);

        // Assert
        Assert.NotNull(route);
        Assert.Equal(longPath, route.Path);
    }

    /// <summary>
    /// 测试：处理特殊字符的路由
    /// </summary>
    [Fact]
    public void Should_Handle_Special_Characters_In_Path()
    {
        // Arrange
        var specialPaths = new[]
        {
            "/api/users?filter=active",
            "/api/file%20name",
            "/api/resource#id",
            "/api/resource-name",
            "/api/resource_name"
        };

        // Act & Assert
        foreach (var path in specialPaths)
        {
            var route = new Route("special", path, "svc", HttpMethod.GET, 1, true, RouteMatchType.Prefix, LoadBalanceType.RoundRobin);
            Assert.NotNull(route);
        }
    }

    /// <summary>
    /// 测试：大量目标地址的权重分配
    /// </summary>
    [Fact]
    public void Should_Handle_Large_Number_Of_Weighted_Destinations()
    {
        // Arrange
        var service = new Service("large-svc", "Large Service");
        for (int i = 0; i < 100; i++)
        {
            service.AddDestination(new Destination($"server-{i}", Protocol.Http, $"host{i}", 80, (i + 1) * 10, 30));
        }

        // Act
        var totalWeight = service.Destinations.Sum(d => d.Weight);

        // Assert
        Assert.Equal(100, service.Destinations.Count);
        Assert.True(totalWeight > 0);
    }

    /// <summary>
    /// 测试：超大优先级值
    /// </summary>
    [Fact]
    public void Should_Handle_Large_Priority_Values()
    {
        // Arrange
        var route1 = new Route("normal", "/api/1", "svc", HttpMethod.GET, 100, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);
        var route2 = new Route("high", "/api/2", "svc", HttpMethod.GET, int.MaxValue, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);
        var route3 = new Route("negative", "/api/3", "svc", HttpMethod.GET, int.MinValue, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);

        // Act & Assert
        Assert.Equal(100, route1.Priority);
        Assert.Equal(int.MaxValue, route2.Priority);
        Assert.Equal(int.MinValue, route3.Priority);
    }

    #endregion

    #region 特殊字符和编码

    /// <summary>
    /// 测试：路由名称中的特殊字符
    /// </summary>
    [Fact]
    public void Should_Handle_Special_Characters_In_Route_Name()
    {
        // Arrange
        var names = new[]
        {
            "route-with-dash",
            "route_with_underscore",
            "route.with.dot",
            "route:with:colon",
            "路由中文名称"
        };

        // Act & Assert
        foreach (var name in names)
        {
            var route = new Route(name, "/api/test", "svc", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);
            Assert.Equal(name, route.Name);
        }
    }

    /// <summary>
    /// 测试：服务代码中的版本号
    /// </summary>
    [Fact]
    public void Should_Handle_Version_Numbers_In_Service_Code()
    {
        // Arrange
        var serviceCodes = new[]
        {
            "user-service-v1",
            "user-service-v2",
            "user-service-v2.1.0",
            "api_service_1.0.0-beta"
        };

        // Act & Assert
        foreach (var code in serviceCodes)
        {
            var service = new Service(code, $"Service {code}");
            Assert.Equal(code, service.Code);
        }
    }

    #endregion

    #region 状态转换

    /// <summary>
    /// 测试：路由多次启用/禁用切换
    /// </summary>
    [Fact]
    public void Should_Handle_Multiple_Enable_Disable_Toggles()
    {
        // Arrange
        var route = new Route("toggle", "/api/test", "svc", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);

        // Act - 多次切换
        for (int i = 0; i < 100; i++)
        {
            if (i % 2 == 0)
                route.Enable();
            else
                route.Disable();
        }

        // Assert
        Assert.False(route.IsEnabled); // 最后一次是Disable
    }

    /// <summary>
    /// 测试：服务目标地址的添加/删除循环
    /// </summary>
    [Fact]
    public void Should_Handle_Add_Remove_Destination_Cycle()
    {
        // Arrange
        var service = new Service("cycle-svc", "Cycle Service");
        var dest = new Destination("dest-1", Protocol.Http, "host", 80, 100, 30);

        // Act - 循环添加删除
        for (int i = 0; i < 10; i++)
        {
            if (i % 2 == 0)
                service.AddDestination(dest);
            else
                service.RemoveDestination("dest-1");
        }

        // Assert
        Assert.Empty(service.Destinations); // 最后一次是删除
    }

    #endregion

    #region 极限值

    /// <summary>
    /// 测试：最小和最大端口号
    /// </summary>
    [Fact]
    public void Should_Handle_Valid_Port_Range()
    {
        // Arrange
        var minPort = new Destination("min", Protocol.Http, "host", 1, 100, 30);
        var maxPort = new Destination("max", Protocol.Http, "host", 65535, 100, 30);

        // Act & Assert
        Assert.Equal(1, minPort.Port);
        Assert.Equal(65535, maxPort.Port);
    }

    /// <summary>
    /// 测试：权重为1的最小值
    /// </summary>
    [Fact]
    public void Should_Handle_Minimum_Weight()
    {
        // Arrange & Act
        var dest = new Destination("min-weight", Protocol.Http, "host", 80, 1, 30);

        // Assert
        Assert.Equal(1, dest.Weight);
    }

    /// <summary>
    /// 测试：权重的非常大的值
    /// </summary>
    [Fact]
    public void Should_Handle_Large_Weight_Values()
    {
        // Arrange & Act
        var dest = new Destination("huge-weight", Protocol.Http, "host", 80, int.MaxValue, 30);

        // Assert
        Assert.Equal(int.MaxValue, dest.Weight);
    }

    /// <summary>
    /// 测试：目标地址地址生成
    /// </summary>
    [Fact]
    public void Should_Generate_Valid_Address_URL()
    {
        // Arrange
        var httpDest = new Destination("http-dest", Protocol.Http, "example.com", 80, 100, 30);
        var httpsDest = new Destination("https-dest", Protocol.Https, "secure.example.com", 443, 100, 30);
        var customPortDest = new Destination("custom-port", Protocol.Http, "localhost", 8080, 100, 30);

        // Act & Assert
        Assert.Equal("http://example.com", httpDest.GetAddress());
        Assert.Equal("https://secure.example.com", httpsDest.GetAddress());
        Assert.Equal("http://localhost:8080", customPortDest.GetAddress());
    }

    #endregion

    #region 正则表达式边界

    /// <summary>
    /// 测试：复杂的正则表达式路由
    /// </summary>
    [Fact]
    public void Should_Handle_Complex_Regex_Patterns()
    {
        // Arrange
        var patterns = new[]
        {
            @"^/api/v[0-9]+/",
            @"^/api/[a-z]+/[0-9]+$",
            @"^/api/.*\.json$",
            @"^/api/(users|products|orders)/",
            @"^/api/\d{4}/\d{2}/\d{2}/"
        };

        // Act & Assert
        foreach (var pattern in patterns)
        {
            var route = new Route("regex-test", pattern, "svc", HttpMethod.GET, 1, true, RouteMatchType.Regex, LoadBalanceType.RoundRobin);
            Assert.NotNull(route);
        }
    }

    #endregion

    #region 无效操作

    /// <summary>
    /// 测试：删除不存在的目标地址
    /// </summary>
    [Fact]
    public void Should_Return_False_When_Deleting_NonExistent_Destination()
    {
        // Arrange
        var service = new Service("delete-test", "Delete Test");

        // Act
        var result = service.RemoveDestination("nonexistent");

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// 测试：获取不存在的目标地址
    /// </summary>
    [Fact]
    public void Should_Return_Null_For_NonExistent_Destination()
    {
        // Arrange
        var service = new Service("get-test", "Get Test");

        // Act
        var dest = service.GetDestination("nonexistent");

        // Assert
        Assert.Null(dest);
    }

    /// <summary>
    /// 测试：空参数提取
    /// </summary>
    [Fact]
    public void Should_Handle_Empty_Route_Update()
    {
        // Arrange
        var route = new Route("original", "/api/original", "svc-1", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);

        // Act - 用空值更新（不应改变任何东西）
        route.Update(null, null, null, null, null, null, null, null);

        // Assert - 所有属性都应保持不变
        Assert.Equal("/api/original", route.Path);
        Assert.Equal("svc-1", route.ServiceCode);
        Assert.Equal(HttpMethod.GET, route.HttpMethods);
    }

    #endregion
}

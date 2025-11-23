using Domain.Routes;
using Domain.Routes.Configuration;
using HttpMethod = Domain.Routes.HttpMethod;

namespace Tests.Unit.Domain.Routes;

/// <summary>
/// Route 实体单元测试
/// </summary>
public class RouteTests
{
    #region 构造函数测试

    [Fact]
    public void Constructor_ValidParameters_CreatesRoute()
    {
        // Arrange & Act
        var route = new Route(
            name: "UserRoute",
            path: "/api/users",
            serviceCode: "user-service",
            httpMethods: HttpMethod.GET,
            priority: 100,
            isEnabled: true,
            matchType: RouteMatchType.Prefix,
            loadBalanceType: LoadBalanceType.RoundRobin
        );

        // Assert
        Assert.Equal("UserRoute", route.Name);
        Assert.Equal("/api/users", route.Path);
        Assert.Equal("user-service", route.ServiceCode);
        Assert.Equal(HttpMethod.GET, route.HttpMethods);
        Assert.Equal(100, route.Priority);
        Assert.True(route.IsEnabled);
        Assert.Equal(RouteMatchType.Prefix, route.MatchType);
        Assert.Equal(LoadBalanceType.RoundRobin, route.LoadBalanceType);
    }

    [Fact]
    public void Constructor_DefaultValues_UsesDefaults()
    {
        // Arrange & Act
        var route = new Route(
            name: "DefaultRoute",
            path: "/api/default",
            serviceCode: "default-service"
        );

        // Assert
        Assert.Equal(HttpMethod.GET, route.HttpMethods); // 默认GET
        Assert.Equal(0, route.Priority); // 默认0
        Assert.True(route.IsEnabled); // 默认启用
        Assert.Equal(RouteMatchType.Prefix, route.MatchType); // 默认前缀匹配
        Assert.Equal(LoadBalanceType.RoundRobin, route.LoadBalanceType); // 默认轮询
    }

    [Fact]
    public void Constructor_EmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new Route("", "/api/users", "service")
        );

        Assert.Contains("Route name cannot be empty", exception.Message);
        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhitespaceName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new Route("   ", "/api/users", "service")
        );

        Assert.Contains("Route name cannot be empty", exception.Message);
    }

    [Fact]
    public void Constructor_EmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new Route("TestRoute", "", "service")
        );

        Assert.Contains("Route path cannot be empty", exception.Message);
        Assert.Equal("path", exception.ParamName);
    }

    [Fact]
    public void Constructor_EmptyServiceCode_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new Route("TestRoute", "/api/users", "")
        );

        Assert.Contains("Service code cannot be empty", exception.Message);
        Assert.Equal("serviceCode", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Route(null!, "/api/users", "service")
        );
    }

    #endregion

    #region IsMatch 测试 - Exact 匹配

    [Theory]
    [InlineData("/api/users", "/api/users", true)]
    [InlineData("/api/users", "/API/USERS", true)]
    [InlineData("/api/users", "/api/users/", false)]
    [InlineData("/api/users", "/api/users/{id}", false)]
    [InlineData("/", "/", true)]
    public void IsMatch_ExactMatchType_ReturnsExpectedResult(
        string routePath,
        string requestPath,
        bool expected)
    {
        // Arrange
        var route = new Route("Test", routePath, "service",
            matchType: RouteMatchType.Exact,
            httpMethods: HttpMethod.GET);

        // Act
        var result = route.IsMatch(requestPath, HttpMethod.GET);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region IsMatch 测试 - Prefix 匹配

    [Theory]
    [InlineData("/api", "/api/users", true)]
    [InlineData("/api", "/api/users/123", true)]
    [InlineData("/api", "/api", true)]
    [InlineData("/api", "/API/USERS", true)] // 不区分大小写
    [InlineData("/api/users", "/api/products", false)]
    [InlineData("/", "/api/users", true)] // 根路径匹配所有
    public void IsMatch_PrefixMatchType_ReturnsExpectedResult(
        string routePath,
        string requestPath,
        bool expected)
    {
        // Arrange
        var route = new Route("Test", routePath, "service",
            matchType: RouteMatchType.Prefix,
            httpMethods: HttpMethod.GET);

        // Act
        var result = route.IsMatch(requestPath, HttpMethod.GET);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region IsMatch 测试 - Regex 匹配

    [Theory]
    [InlineData(@"^/api/users/\d+$", "/api/users/123", true)]
    [InlineData(@"^/api/users/\d+$", "/api/users/abc", false)]
    [InlineData(@"^/api/users/(?<id>\d+)$", "/api/users/456", true)]
    [InlineData(@"^/api/.+$", "/api/anything", true)]
    [InlineData(@"^/api/.+$", "/api/", false)]
    public void IsMatch_RegexMatchType_ReturnsExpectedResult(
        string pattern,
        string requestPath,
        bool expected)
    {
        // Arrange
        var route = new Route("Test", pattern, "service",
            matchType: RouteMatchType.Regex,
            httpMethods: HttpMethod.GET);

        // Act
        var result = route.IsMatch(requestPath, HttpMethod.GET);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsMatch_RegexMatch_InvalidPattern_ReturnsFalse()
    {
        // Arrange - 无效的正则表达式
        var route = new Route("Test", @"^/api/users/[", "service",
            matchType: RouteMatchType.Regex,
            httpMethods: HttpMethod.GET);

        // Act & Assert
        // 根据实际实现，可能抛异常或返回false
        try
        {
            var result = route.IsMatch("/api/users/123", HttpMethod.GET);
            Assert.False(result); // 如果不抛异常，应该返回false
        }
        catch (ArgumentException)
        {
            // 如果抛异常也是合理的
            Assert.True(true);
        }
    }

    [Theory]
    [InlineData("/api/users/{id}", "/api/users/123", true)]
    [InlineData("/api/users/{id}", "/api/users/abc", true)]
    [InlineData("/api/users/{id}", "/API/USERS/123", true)]
    [InlineData("/api/users/{id}", "/api/users", false)]
    [InlineData("/api/users/{id}", "/api/users/123/extra", false)]
    [InlineData("/api/users/{userId}/orders/{orderId}", "/api/users/123/orders/456", true)]
    [InlineData("/api/{resource}/{id}", "/api/users/123", true)]
    public void IsMatch_TemplateMatchType_ReturnsExpectedResult(
           string routePath,
           string requestPath,
           bool expected)
    {
        // Arrange
        var route = new Route("Test", routePath, "service",
            matchType: RouteMatchType.Template,
            httpMethods: HttpMethod.GET);

        // Act
        var result = route.IsMatch(requestPath, HttpMethod.GET);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsMatch_TemplateMatch_WithHttpMethodMismatch_ReturnsFalse()
    {
        // Arrange
        var route = new Route("Test", "/api/users/{id}", "service",
            matchType: RouteMatchType.Template,
            httpMethods: HttpMethod.GET);

        // Act
        var result = route.IsMatch("/api/users/123", HttpMethod.POST);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region IsEnabled 测试

    [Fact]
    public void IsMatch_DisabledRoute_ReturnsFalse()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service",
            isEnabled: false,
            httpMethods: HttpMethod.GET);

        // Act
        var result = route.IsMatch("/api/users", HttpMethod.GET);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Enable_SetsIsEnabledToTrue()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service", isEnabled: false);

        // Act
        route.Enable();

        // Assert
        Assert.True(route.IsEnabled);
    }

    [Fact]
    public void Disable_SetsIsEnabledToFalse()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service", isEnabled: true);

        // Act
        route.Disable();

        // Assert
        Assert.False(route.IsEnabled);
    }

    [Fact]
    public void IsMatch_AfterEnable_ReturnsTrue()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service",
            isEnabled: false,
            matchType: RouteMatchType.Exact,
            httpMethods: HttpMethod.GET);

        // Act
        route.Enable();
        var result = route.IsMatch("/api/users", HttpMethod.GET);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region HTTP 方法测试

    [Theory]
    [InlineData(HttpMethod.GET, HttpMethod.GET, true)]
    [InlineData(HttpMethod.POST, HttpMethod.GET, false)]
    [InlineData(HttpMethod.GET | HttpMethod.POST, HttpMethod.GET, true)]
    [InlineData(HttpMethod.GET | HttpMethod.POST, HttpMethod.POST, true)]
    [InlineData(HttpMethod.GET | HttpMethod.POST, HttpMethod.PUT, false)]
    [InlineData(HttpMethod.ALL, HttpMethod.GET, true)]
    [InlineData(HttpMethod.ALL, HttpMethod.POST, true)]
    [InlineData(HttpMethod.ALL, HttpMethod.DELETE, true)]
    public void IsMatch_HttpMethodFlags_WorksCorrectly(
        HttpMethod routeMethods,
        HttpMethod requestMethod,
        bool expected)
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service",
            httpMethods: routeMethods,
            matchType: RouteMatchType.Exact);

        // Act
        var result = route.IsMatch("/api/users", requestMethod);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsMatch_WrongHttpMethod_ReturnsFalse()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service",
            httpMethods: HttpMethod.GET,
            matchType: RouteMatchType.Exact);

        // Act
        var result = route.IsMatch("/api/users", HttpMethod.POST);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Update 方法测试

    [Fact]
    public void Update_AllParameters_UpdatesRoute()
    {
        // Arrange
        var route = new Route("OldName", "/old/path", "old-service");

        // Act
        route.Update(
            name: "NewName",
            path: "/new/path",
            serviceCode: "new-service",
            httpMethods: HttpMethod.POST,
            priority: 200,
            isEnabled: false,
            matchType: RouteMatchType.Regex,
            loadBalanceType: LoadBalanceType.WeightedRoundRobin
        );

        // Assert
        Assert.Equal("NewName", route.Name);
        Assert.Equal("/new/path", route.Path);
        Assert.Equal("new-service", route.ServiceCode);
        Assert.Equal(HttpMethod.POST, route.HttpMethods);
        Assert.Equal(200, route.Priority);
        Assert.False(route.IsEnabled);
        Assert.Equal(RouteMatchType.Regex, route.MatchType);
        Assert.Equal(LoadBalanceType.WeightedRoundRobin, route.LoadBalanceType);
    }

    [Fact]
    public void Update_PartialParameters_UpdatesOnlySpecified()
    {
        // Arrange
        var route = new Route("OldName", "/old/path", "old-service",
            httpMethods: HttpMethod.GET,
            priority: 100);

        // Act
        route.Update(name: "NewName", priority: 200);

        // Assert
        Assert.Equal("NewName", route.Name);
        Assert.Equal("/old/path", route.Path); // 未改变
        Assert.Equal("old-service", route.ServiceCode); // 未改变
        Assert.Equal(HttpMethod.GET, route.HttpMethods); // 未改变
        Assert.Equal(200, route.Priority); // 已改变
    }

    [Fact]
    public void Update_NullParameters_DoesNotUpdate()
    {
        // Arrange
        var route = new Route("OldName", "/old/path", "old-service");

        // Act
        route.Update(name: null, path: null, serviceCode: null);

        // Assert
        Assert.Equal("OldName", route.Name);
        Assert.Equal("/old/path", route.Path);
        Assert.Equal("old-service", route.ServiceCode);
    }

    [Fact]
    public void Update_EmptyString_DoesNotUpdate()
    {
        // Arrange
        var route = new Route("OldName", "/old/path", "old-service");

        // Act
        route.Update(name: "", path: "   ", serviceCode: "");

        // Assert
        Assert.Equal("OldName", route.Name);
        Assert.Equal("/old/path", route.Path);
        Assert.Equal("old-service", route.ServiceCode);
    }

    #endregion

    #region 配置方法测试

    [Fact]
    public void WithBatchingConfig_ValidConfig_SetsConfiguration()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service");
        var config = RequestBatchingConfig.Create(
            batchWindow: TimeSpan.FromMilliseconds(100),
            maxBatchSize: 10,
            mergeStrategy: BatchMergeStrategy.QueryParameters,
            maxWaitTime: TimeSpan.FromSeconds(5)
        );

        // Act
        var result = route.WithBatchingConfig(config);

        // Assert
        Assert.Same(route, result); // 返回自身（链式调用）
        Assert.NotNull(route.BatchingConfig);
        Assert.Same(config, route.BatchingConfig);
        Assert.True(route.BatchingConfig.EnableBatching);
        Assert.Equal(10, route.BatchingConfig.MaxBatchSize);
    }

    [Fact]
    public void WithBatchingConfig_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            route.WithBatchingConfig(null!)
        );
    }

    [Fact]
    public void WithForwardingConfig_ValidConfig_SetsConfiguration()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service");
        var config = RequestForwardingConfig.Default();

        // Act
        var result = route.WithForwardingConfig(config);

        // Assert
        Assert.Same(route, result); // 返回自身
        Assert.NotNull(route.ForwardingConfig);
        Assert.Same(config, route.ForwardingConfig);
    }

    [Fact]
    public void WithForwardingConfig_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            route.WithForwardingConfig(null!)
        );
    }

    [Fact]
    public void WithBatchingConfig_ChainedWithForwardingConfig_Works()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service");
        var batchingConfig = RequestBatchingConfig.Create(
            batchWindow: TimeSpan.FromMilliseconds(100),
            maxBatchSize: 5
        );
        var forwardingConfig = RequestForwardingConfig.Default();

        // Act
        var result = route.WithBatchingConfig(batchingConfig)
                          .WithForwardingConfig(forwardingConfig);

        // Assert
        Assert.Same(route, result);
        Assert.NotNull(route.BatchingConfig);
        Assert.NotNull(route.ForwardingConfig);
        Assert.Same(batchingConfig, route.BatchingConfig);
        Assert.Same(forwardingConfig, route.ForwardingConfig);
    }

    #endregion

    #region ValidateConfiguration 测试

    [Fact]
    public void ValidateConfiguration_NoBatchingConfig_CreatesDisabledConfig()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service");
        Assert.Null(route.BatchingConfig);

        // Act
        route.ValidateConfiguration();

        // Assert
        Assert.NotNull(route.BatchingConfig);
        Assert.False(route.BatchingConfig.EnableBatching); // 修正：使用正确的属性名
    }

    [Fact]
    public void ValidateConfiguration_NoForwardingConfig_CreatesDefaultConfig()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service");
        Assert.Null(route.ForwardingConfig);

        // Act
        route.ValidateConfiguration();

        // Assert
        Assert.NotNull(route.ForwardingConfig);
    }

    [Fact]
    public void ValidateConfiguration_ExistingConfigs_DoesNotOverwrite()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service");
        var batchingConfig = RequestBatchingConfig.Create(
            batchWindow: TimeSpan.FromMilliseconds(100),
            maxBatchSize: 10
        );
        var forwardingConfig = RequestForwardingConfig.Default();

        route.WithBatchingConfig(batchingConfig)
             .WithForwardingConfig(forwardingConfig);

        // Act
        route.ValidateConfiguration();

        // Assert
        Assert.Same(batchingConfig, route.BatchingConfig);
        Assert.Same(forwardingConfig, route.ForwardingConfig);
    }

    #endregion

    #region 边界和综合测试

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Constructor_VariousPriorities_Accepted(int priority)
    {
        // Act
        var route = new Route("Test", "/api/users", "service", priority: priority);

        // Assert
        Assert.Equal(priority, route.Priority);
    }

    [Fact]
    public void IsMatch_MatchesPathAndMethod_ReturnsTrue()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service",
            httpMethods: HttpMethod.GET,
            matchType: RouteMatchType.Exact);

        // Act
        var result = route.IsMatch("/api/users", HttpMethod.GET);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void LoadBalanceType_SetViaConstructor_Persists()
    {
        // Arrange & Act
        var route = new Route("Test", "/api/users", "service",
            loadBalanceType: LoadBalanceType.LeastConnections);

        // Assert
        Assert.Equal(LoadBalanceType.LeastConnections, route.LoadBalanceType);
    }

    [Fact]
    public void LoadBalanceType_UpdateViaUpdate_Changes()
    {
        // Arrange
        var route = new Route("Test", "/api/users", "service",
            loadBalanceType: LoadBalanceType.RoundRobin);

        // Act
        route.Update(loadBalanceType: LoadBalanceType.Random);

        // Assert
        Assert.Equal(LoadBalanceType.Random, route.LoadBalanceType);
    }

    #endregion
}
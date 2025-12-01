using Domain.Routes;
using Domain.Routes.Configuration;
using Infrastructure.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;
using Tests.Integration;
using HttpMethod = Domain.Routes.HttpMethod;

/// <summary>
/// 请求转发配置集成测试
/// 测试转发选项、头部处理、流式转发、缓冲等
/// </summary>
[Collection("Gateway Integration Tests")]
public class RequestForwardingTests : IAsyncLifetime
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
    /// 测试：默认转发配置
    /// </summary>
    [Fact]
    public void Should_Create_Default_Forwarding_Config()
    {
        // Act
        var config = RequestForwardingConfig.Default();

        // Assert
        Assert.NotNull(config);
        Assert.False(config.PreserveHostHeader);
        Assert.True(config.EnableStreamingForwarding);
        Assert.True(config.PreserveClientIp);
        Assert.Equal(65536, config.BufferSize);
    }

    /// <summary>
    /// 测试：自定义转发配置
    /// </summary>
    [Fact]
    public void Should_Create_Custom_Forwarding_Config()
    {
        // Act
        var config = RequestForwardingConfig.Create(
            preserveHostHeader: true,
            enableStreamingForwarding: false,
            bufferSize: 131072,
            preserveClientIp: false
        );

        // Assert
        Assert.NotNull(config);
        Assert.True(config.PreserveHostHeader);
        Assert.False(config.EnableStreamingForwarding);
        Assert.False(config.PreserveClientIp);
        Assert.Equal(131072, config.BufferSize);
    }

    /// <summary>
    /// 测试：自定义请求头配置
    /// </summary>
    [Fact]
    public void Should_Configure_Custom_Headers()
    {
        // Arrange
        var customHeaders = new Dictionary<string, string>
        {
            { "X-Custom-Header", "custom-value" },
            { "X-API-Key", "secret-key" },
            { "Authorization", "Bearer token123" }
        };

        // Act
        var config = RequestForwardingConfig.Create(
            customHeaders: customHeaders
        );

        // Assert
        Assert.NotNull(config.CustomHeaders);
        Assert.Equal(3, config.CustomHeaders.Count);
        Assert.Equal("custom-value", config.CustomHeaders["X-Custom-Header"]);
        Assert.Equal("secret-key", config.CustomHeaders["X-API-Key"]);
    }

    /// <summary>
    /// 测试：移除指定的请求头
    /// </summary>
    [Fact]
    public void Should_Remove_Specified_Headers()
    {
        // Arrange
        var removeHeaders = new List<string> { "Connection", "Transfer-Encoding", "Upgrade", "X-Custom" };

        // Act
        var config = RequestForwardingConfig.Create(removeHeaders: removeHeaders);

        // Assert
        Assert.NotNull(config.RemoveHeaders);
        Assert.Contains("Connection", config.RemoveHeaders);
        Assert.Contains("Transfer-Encoding", config.RemoveHeaders);
        Assert.Contains("X-Custom", config.RemoveHeaders);
    }

    /// <summary>
    /// 测试：流式转发启用
    /// </summary>
    [Fact]
    public void Should_Enable_Streaming_Forwarding()
    {
        // Act
        var config = RequestForwardingConfig.Create(enableStreamingForwarding: true);

        // Assert
        Assert.True(config.EnableStreamingForwarding);
    }

    /// <summary>
    /// 测试：流式转发禁用（缓冲模式）
    /// </summary>
    [Fact]
    public void Should_Disable_Streaming_Forwarding()
    {
        // Act
        var config = RequestForwardingConfig.Create(enableStreamingForwarding: false);

        // Assert
        Assert.False(config.EnableStreamingForwarding);
    }

    /// <summary>
    /// 测试：缓冲大小验证
    /// </summary>
    [Fact]
    public void Should_Validate_Buffer_Size()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            RequestForwardingConfig.Create(bufferSize: 0)
        );

        Assert.Throws<ArgumentException>(() =>
            RequestForwardingConfig.Create(bufferSize: -1)
        );
    }

    /// <summary>
    /// 测试：大缓冲大小（处理大文件上传）
    /// </summary>
    [Fact]
    public void Should_Support_Large_Buffer_Sizes()
    {
        // Act
        var config = RequestForwardingConfig.Create(bufferSize: 10 * 1024 * 1024); // 10MB

        // Assert
        Assert.Equal(10 * 1024 * 1024, config.BufferSize);
    }

    /// <summary>
    /// 测试：保留客户端IP
    /// </summary>
    [Fact]
    public void Should_Preserve_Client_Ip()
    {
        // Act
        var config = RequestForwardingConfig.Create(preserveClientIp: true);

        // Assert
        Assert.True(config.PreserveClientIp);
    }

    /// <summary>
    /// 测试：不保留客户端IP
    /// </summary>
    [Fact]
    public void Should_Not_Preserve_Client_Ip()
    {
        // Act
        var config = RequestForwardingConfig.Create(preserveClientIp: false);

        // Assert
        Assert.False(config.PreserveClientIp);
    }

    /// <summary>
    /// 测试：保留Host头部
    /// </summary>
    [Fact]
    public void Should_Preserve_Host_Header()
    {
        // Act
        var config = RequestForwardingConfig.Create(preserveHostHeader: true);

        // Assert
        Assert.True(config.PreserveHostHeader);
    }

    /// <summary>
    /// 测试：不保留Host头部（使用目标Host）
    /// </summary>
    [Fact]
    public void Should_Not_Preserve_Host_Header()
    {
        // Act
        var config = RequestForwardingConfig.Create(preserveHostHeader: false);

        // Assert
        Assert.False(config.PreserveHostHeader);
    }

    /// <summary>
    /// 测试：将转发配置应用到路由
    /// </summary>
    [Fact]
    public async Task Should_Apply_Forwarding_Config_To_Route()
    {
        // Arrange
        var route = new Route("TestRoute", "/api/test", "test-service", HttpMethod.GET, 1, true, RouteMatchType.Exact, LoadBalanceType.RoundRobin);
        var forwardingConfig = RequestForwardingConfig.Create(
            preserveHostHeader: true,
            enableStreamingForwarding: false,
            bufferSize: 131072
        );

        // Act
        route.WithForwardingConfig(forwardingConfig);
        route.ValidateConfiguration();
        _dbContext.Routes.Add(route);
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedRoute = await _dbContext.Routes.FirstAsync();
        Assert.NotNull(savedRoute.ForwardingConfig);
        Assert.True(savedRoute.ForwardingConfig.PreserveHostHeader);
        Assert.False(savedRoute.ForwardingConfig.EnableStreamingForwarding);
        Assert.Equal(131072, savedRoute.ForwardingConfig.BufferSize);
    }

    /// <summary>
    /// 测试：多个自定义头部组合
    /// </summary>
    [Fact]
    public void Should_Handle_Multiple_Custom_Headers_With_Removals()
    {
        // Arrange
        var customHeaders = new Dictionary<string, string>
        {
            { "X-Gateway-Version", "1.0" },
            { "X-Service-Name", "TestGateway" },
            { "X-Trace-Id", "trace-123" }
        };
        var removeHeaders = new List<string> { "Connection", "Keep-Alive", "Proxy-Connection" };

        // Act
        var config = RequestForwardingConfig.Create(
            customHeaders: customHeaders,
            removeHeaders: removeHeaders
        );

        // Assert
        Assert.Equal(3, config.CustomHeaders.Count);
        Assert.Equal(3, config.RemoveHeaders.Count);
        Assert.Contains("X-Gateway-Version", config.CustomHeaders.Keys);
        Assert.Contains("Connection", config.RemoveHeaders);
    }

    /// <summary>
    /// 测试：转发配置值对象相等性
    /// </summary>
    [Fact]
    public void Should_Compare_Forwarding_Configs_For_Equality()
    {
        // Arrange
        var config1 = RequestForwardingConfig.Create(
            preserveHostHeader: true,
            enableStreamingForwarding: false,
            bufferSize: 131072
        );
        var config2 = RequestForwardingConfig.Create(
            preserveHostHeader: true,
            enableStreamingForwarding: false,
            bufferSize: 131072
        );
        var config3 = RequestForwardingConfig.Create(
            preserveHostHeader: false,
            enableStreamingForwarding: true,
            bufferSize: 65536
        );

        // Assert
        // 相同的配置应该有相同的属性值
        Assert.Equal(config1.PreserveHostHeader, config2.PreserveHostHeader);
        Assert.Equal(config1.EnableStreamingForwarding, config2.EnableStreamingForwarding);
        Assert.Equal(config1.BufferSize, config2.BufferSize);

        // 不同的配置应该有不同的属性
        Assert.NotEqual(config1.PreserveHostHeader, config3.PreserveHostHeader);
        Assert.NotEqual(config1.EnableStreamingForwarding, config3.EnableStreamingForwarding);
        Assert.NotEqual(config1.BufferSize, config3.BufferSize);
    }

    /// <summary>
    /// 测试：默认删除头部列表
    /// </summary>
    [Fact]
    public void Should_Include_Default_Remove_Headers()
    {
        // Act
        var config = RequestForwardingConfig.Default();

        // Assert
        Assert.NotNull(config.RemoveHeaders);
        Assert.NotEmpty(config.RemoveHeaders);
        // 默认应该包含Connection、Transfer-Encoding、Upgrade
        Assert.Contains("Connection", config.RemoveHeaders);
    }

    /// <summary>
    /// 测试：极端缓冲大小
    /// </summary>
    [Fact]
    public void Should_Handle_Extreme_Buffer_Sizes()
    {
        // Act - 非常大的缓冲（100MB）
        var config1 = RequestForwardingConfig.Create(bufferSize: 100 * 1024 * 1024);

        // Act - 最小缓冲（1字节）
        var config2 = RequestForwardingConfig.Create(bufferSize: 1);

        // Assert
        Assert.Equal(100 * 1024 * 1024, config1.BufferSize);
        Assert.Equal(1, config2.BufferSize);
    }

    /// <summary>
    /// 测试：空的自定义头部
    /// </summary>
    [Fact]
    public void Should_Handle_Empty_Custom_Headers()
    {
        // Act
        var config = RequestForwardingConfig.Create(customHeaders: new Dictionary<string, string>());

        // Assert
        Assert.NotNull(config.CustomHeaders);
        Assert.Empty(config.CustomHeaders);
    }

    /// <summary>
    /// 测试：所有转发选项的组合
    /// </summary>
    [Fact]
    public void Should_Configure_All_Forwarding_Options()
    {
        // Arrange
        var customHeaders = new Dictionary<string, string>
        {
            { "X-Custom", "value" }
        };
        var removeHeaders = new List<string> { "Connection" };

        // Act
        var config = RequestForwardingConfig.Create(
            preserveHostHeader: true,
            customHeaders: customHeaders,
            removeHeaders: removeHeaders,
            enableStreamingForwarding: true,
            bufferSize: 262144,
            preserveClientIp: true
        );

        // Assert
        Assert.True(config.PreserveHostHeader);
        Assert.True(config.EnableStreamingForwarding);
        Assert.True(config.PreserveClientIp);
        Assert.Equal(262144, config.BufferSize);
        Assert.Single(config.CustomHeaders);
        Assert.Single(config.RemoveHeaders);
    }
}

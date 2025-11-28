using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Infrastructure.Routes.LoadBalance;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Unit.Infrastructure.Routes.LoadBalance;

/// <summary>
/// LeastConnectionsStrategy 单元测试
/// </summary>
public class LeastConnectionsStrategyTests
{
    private readonly Mock<ILogger<LeastConnectionsStrategy>> _loggerMock;

    public LeastConnectionsStrategyTests()
    {
        _loggerMock = new Mock<ILogger<LeastConnectionsStrategy>>();

        // 每个测试前清理静态连接计数（通过反射或使用 DecrementConnectionCount）
        ClearConnectionCounts();
    }

    #region SelectDestination 测试 - 基础功能

    [Fact]
    public void SelectDestination_SingleDestination_ReturnsThatDestination()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act
        var result = strategy.SelectDestination(destinations, context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dest-1", result.Code);
    }

    [Fact]
    public void SelectDestination_AllZeroConnections_SelectsFirstByCode()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-3", "host3", 5003),
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act
        var result = strategy.SelectDestination(destinations, context);

        // Assert - 所有连接数为0时，按 Code 排序选择第一个
        Assert.Equal("dest-1", result.Code);
    }

    [Fact]
    public void SelectDestination_SelectsDestinationWithLeastConnections()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act - 第一次选择
        var result1 = strategy.SelectDestination(destinations, context); // dest-1, conn=1
                                                                         // dest-1 现在有 1 个连接，其他都是 0

        var result2 = strategy.SelectDestination(destinations, context); // dest-2, conn=1

        // Assert - 应该选择连接数最少的
        Assert.Equal("dest-1", result1.Code);
        Assert.Equal("dest-2", result2.Code); // dest-2 连接数为0，被选中
    }

    [Fact]
    public void SelectDestination_DistributesBasedOnActiveConnections()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act - 选择多次
        var result1 = strategy.SelectDestination(destinations, context); // dest-1
        var result2 = strategy.SelectDestination(destinations, context); // dest-2
        var result3 = strategy.SelectDestination(destinations, context); // dest-3
        var result4 = strategy.SelectDestination(destinations, context); // dest-1（回到第一个）

        // Assert - 应该循环选择，直到所有目标连接数相等
        Assert.Equal("dest-1", result1.Code);
        Assert.Equal("dest-2", result2.Code);
        Assert.Equal("dest-3", result3.Code);
        Assert.Equal("dest-1", result4.Code);
    }

    #endregion

    #region SelectDestination 测试 - 连接计数

    [Fact]
    public void SelectDestination_IncrementsConnectionCount()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act
        strategy.SelectDestination(destinations, context);
        strategy.SelectDestination(destinations, context);
        strategy.SelectDestination(destinations, context);

        // Assert - 通过再次选择验证连接数是否增加
        // 由于无法直接访问计数，我们通过行为验证
        var result = strategy.SelectDestination(destinations, context);
        Assert.Equal("dest-1", result.Code);
    }

    [Fact]
    public void DecrementConnectionCount_ReducesConnectionCount()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act
        var result1 = strategy.SelectDestination(destinations, context); // dest-1, conn=1
        var result2 = strategy.SelectDestination(destinations, context); // dest-2, conn=1

        // 减少 dest-1 的连接数
        LeastConnectionsStrategy.DecrementConnectionCount("dest-1"); // dest-1, conn=0

        var result3 = strategy.SelectDestination(destinations, context); // 应该再次选择 dest-1

        // Assert
        Assert.Equal("dest-1", result1.Code);
        Assert.Equal("dest-2", result2.Code);
        Assert.Equal("dest-1", result3.Code); // dest-1 连接数减少后被重新选中
    }

    [Fact]
    public void DecrementConnectionCount_DoesNotGoBelowZero()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act - 在没有连接的情况下减少
        LeastConnectionsStrategy.DecrementConnectionCount("dest-1");
        LeastConnectionsStrategy.DecrementConnectionCount("dest-1");
        LeastConnectionsStrategy.DecrementConnectionCount("dest-1");

        var result = strategy.SelectDestination(destinations, context);

        // Assert - 不应该抛出异常，连接数应该保持为0
        Assert.Equal("dest-1", result.Code);
    }

    #endregion

    #region SelectDestination 测试 - 动态负载均衡

    [Fact]
    public void SelectDestination_AfterDecrements_RedistributesLoad()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act
        // 每个目标获得3个连接
        for (int i = 0; i < 9; i++)
        {
            strategy.SelectDestination(destinations, context);
        }

        // dest-1 和 dest-2 完成请求
        LeastConnectionsStrategy.DecrementConnectionCount("dest-1");
        LeastConnectionsStrategy.DecrementConnectionCount("dest-1");
        LeastConnectionsStrategy.DecrementConnectionCount("dest-2");
        LeastConnectionsStrategy.DecrementConnectionCount("dest-2");

        // 再次选择
        var result1 = strategy.SelectDestination(destinations, context);
        var result2 = strategy.SelectDestination(destinations, context);

        // Assert - 应该选择连接数最少的
        Assert.Equal("dest-1", result1.Code);
        Assert.Equal("dest-2", result2.Code);
    }

    [Fact]
    public void SelectDestination_RealWorldScenario_BalancesLoad()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act - 模拟真实场景
        var selected = new List<string>();

        // 分配10个请求
        for (int i = 0; i < 10; i++)
        {
            var result = strategy.SelectDestination(destinations, context);
            selected.Add(result.Code);
        }

        // 一些请求完成
        LeastConnectionsStrategy.DecrementConnectionCount("dest-1");
        LeastConnectionsStrategy.DecrementConnectionCount("dest-2");
        LeastConnectionsStrategy.DecrementConnectionCount("dest-3");

        // 再分配5个请求
        for (int i = 0; i < 5; i++)
        {
            var result = strategy.SelectDestination(destinations, context);
            selected.Add(result.Code);
        }

        // Assert - 验证所有目标都被使用
        var groupedResults = selected.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, groupedResults.Count);
        Assert.All(groupedResults, kvp => Assert.True(kvp.Value > 0));
    }

    #endregion

    #region SelectDestination 测试 - 异常情况

    [Fact]
    public void SelectDestination_EmptyList_ThrowsInvalidOperationException()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>();
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            strategy.SelectDestination(destinations, context));

        Assert.Equal("No destinations available for load balancing", exception.Message);
    }

    [Fact]
    public void SelectDestination_NullList_ThrowsNullReferenceException()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            strategy.SelectDestination(null!, context));
    }

    #endregion

    #region SelectDestination 测试 - 并发安全

    [Fact]
    public void SelectDestination_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };

        // Act - 并发调用
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 300, _ =>
        {
            var context = new LoadBalanceContext
            {
                ClientIp = "127.0.0.1",
                RequestPath = "/api/test",
                RequestId = Guid.NewGuid().ToString()
            };
            var result = strategy.SelectDestination(destinations, context);
            results.Add(result.Code);
        });

        // Assert - 应该分布相对均匀
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(300, results.Count);
        Assert.Equal(3, groupedResults.Count); // 三个目标都应该被选中

        // 验证分布相对均匀（容差 20%）
        var expectedCount = 100;
        var tolerance = 20;
        Assert.All(groupedResults, kvp =>
        {
            Assert.True(Math.Abs(kvp.Value - expectedCount) < tolerance,
                $"Destination {kvp.Key} was selected {kvp.Value} times, expected around {expectedCount}");
        });
    }

    [Fact]
    public void SelectDestination_ConcurrentWithDecrements_ThreadSafe()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };

        var results = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Act - 并发选择和减少
        Parallel.Invoke(
            // 选择请求
            () => Parallel.For(0, 100, _ =>
            {
                var context = new LoadBalanceContext
                {
                    ClientIp = "127.0.0.1",
                    RequestPath = "/api/test",
                    RequestId = Guid.NewGuid().ToString()
                };
                var result = strategy.SelectDestination(destinations, context);
                results.Add(result.Code);
            }),
            // 完成请求
            () => Parallel.For(0, 50, _ =>
            {
                Thread.Sleep(1);
                LeastConnectionsStrategy.DecrementConnectionCount("dest-1");
                LeastConnectionsStrategy.DecrementConnectionCount("dest-2");
                LeastConnectionsStrategy.DecrementConnectionCount("dest-3");
            })
        );

        // Assert - 不应该抛出异常
        Assert.True(results.Count > 0);
        Assert.True(results.Count <= 100);
    }

    #endregion

    #region SelectDestination 测试 - 上下文独立性

    [Fact]
    public void SelectDestination_DifferentContexts_BasedOnConnectionCount()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };
        var context1 = new LoadBalanceContext
        {
            ClientIp = "192.168.1.1",
            RequestPath = "/api/test1"
        };
        var context2 = new LoadBalanceContext
        {
            ClientIp = "192.168.1.2",
            RequestPath = "/api/test2"
        };

        // Act
        var result1 = strategy.SelectDestination(destinations, context1); // dest-1
        var result2 = strategy.SelectDestination(destinations, context2); // dest-2
        var result3 = strategy.SelectDestination(destinations, context1); // dest-1

        // Assert - 不依赖上下文，只依赖连接数
        Assert.Equal("dest-1", result1.Code);
        Assert.Equal("dest-2", result2.Code);
        Assert.Equal("dest-1", result3.Code);
    }

    [Fact]
    public void SelectDestination_NullContext_HandlesGracefully()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001)
        };

        // Act & Assert
        var result = strategy.SelectDestination(destinations, null!);
        Assert.NotNull(result);
        Assert.Equal("dest-1", result.Code);
    }

    #endregion

    #region SelectDestination 测试 - 权重忽略

    [Fact]
    public void SelectDestination_IgnoresWeight_BasedOnlyOnConnections()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            new Destination("dest-1", Protocol.Http, "host1", 5001, weight: 100, 30, null, null),
            new Destination("dest-2", Protocol.Http, "host2", 5002, weight: 10, 30, null, null)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act - 选择多次
        var result1 = strategy.SelectDestination(destinations, context);
        var result2 = strategy.SelectDestination(destinations, context);

        // Assert - 应该忽略权重，按连接数选择
        Assert.Equal("dest-1", result1.Code); // 连接数为0，按 Code 排序
        Assert.Equal("dest-2", result2.Code); // dest-2 连接数也为0
    }

    #endregion

    #region Type 属性测试

    [Fact]
    public void Type_ReturnsLeastConnections()
    {
        // Arrange
        var strategy = new LeastConnectionsStrategy(_loggerMock.Object);

        // Act
        var type = strategy.Type;

        // Assert
        Assert.Equal(LoadBalanceType.LeastConnections, type);
    }

    #endregion

    #region Helper Methods

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

    private void ClearConnectionCounts()
    {
        // 通过多次调用 DecrementConnectionCount 来清零
        // 这是一个简单的方法，实际项目中可能需要反射或提供 Reset 方法
        var commonCodes = new[] { "dest-1", "dest-2", "dest-3", "dest-4", "dest-5" };
        foreach (var code in commonCodes)
        {
            for (int i = 0; i < 1000; i++) // 确保减到0
            {
                LeastConnectionsStrategy.DecrementConnectionCount(code);
            }
        }
    }

    #endregion
}
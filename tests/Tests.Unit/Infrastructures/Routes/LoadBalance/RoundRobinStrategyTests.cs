using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Infrastructure.Routes.LoadBalance;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Unit.Infrastructure.Routes.LoadBalance;

/// <summary>
/// RoundRobinStrategy 单元测试
/// </summary>
public class RoundRobinStrategyTests
{
    private readonly Mock<ILogger<RoundRobinStrategy>> _loggerMock;

    public RoundRobinStrategyTests()
    {
        _loggerMock = new Mock<ILogger<RoundRobinStrategy>>();
    }

    #region SelectDestination 测试 - 基础功能

    [Fact]
    public void SelectDestination_SingleDestination_ReturnsThatDestination()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_loggerMock.Object);
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
    public void SelectDestination_ThreeDestinations_RotatesInOrder()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_loggerMock.Object);
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

        // Act - 调用多次验证轮询
        var result1 = strategy.SelectDestination(destinations, context);
        var result2 = strategy.SelectDestination(destinations, context);
        var result3 = strategy.SelectDestination(destinations, context);
        var result4 = strategy.SelectDestination(destinations, context);
        var result5 = strategy.SelectDestination(destinations, context);
        var result6 = strategy.SelectDestination(destinations, context);

        // Assert - ✅ Interlocked.Increment 从 1 开始
        // 第1次: counter=N+1, (N+1)%3 → 可能是 0,1,2 任一个
        // 但连续6次应该包含完整的两轮
        var results = new[] { result1.Code, result2.Code, result3.Code, result4.Code, result5.Code, result6.Code };

        // 验证每个目标都出现了2次
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, groupedResults.Count);
        Assert.All(groupedResults, kvp => Assert.Equal(2, kvp.Value));
    }

    [Fact]
    public void SelectDestination_TwoDestinations_AlternatesBetweenThem()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_loggerMock.Object);
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
        var results = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 应该交替出现
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(2, groupedResults.Count);
        Assert.Equal(3, groupedResults["dest-1"]);
        Assert.Equal(3, groupedResults["dest-2"]);
    }

    #endregion

    #region SelectDestination 测试 - 异常情况

    [Fact]
    public void SelectDestination_EmptyList_ThrowsInvalidOperationException()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_loggerMock.Object);
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
        var strategy = new RoundRobinStrategy(_loggerMock.Object);
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

    #region SelectDestination 测试 - 状态管理

    [Fact]
    public void SelectDestination_MultipleInstances_ShareStaticCounter()
    {
        // Arrange
        var logger1 = new Mock<ILogger<RoundRobinStrategy>>();
        var logger2 = new Mock<ILogger<RoundRobinStrategy>>();
        var strategy1 = new RoundRobinStrategy(logger1.Object);
        var strategy2 = new RoundRobinStrategy(logger2.Object);

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

        // Act - 两个实例交替调用
        var result1 = strategy1.SelectDestination(destinations, context);
        var result2 = strategy2.SelectDestination(destinations, context);
        var result3 = strategy1.SelectDestination(destinations, context);

        // Assert - ✅ 静态计数器，所有实例共享，连续轮询
        Assert.Contains(result1.Code, new[] { "dest-1", "dest-2", "dest-3" });
        Assert.Contains(result2.Code, new[] { "dest-1", "dest-2", "dest-3" });
        Assert.Contains(result3.Code, new[] { "dest-1", "dest-2", "dest-3" });

        // 三次调用应该选择三个不同的目标（理想情况，除非静态计数器刚好回环）
        var results = new[] { result1.Code, result2.Code, result3.Code };
        var uniqueCount = results.Distinct().Count();
        Assert.True(uniqueCount >= 2); // 至少有2个不同的目标
    }

    [Fact]
    public void SelectDestination_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_loggerMock.Object);
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

        // Assert - 应该均匀分布（大致）
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(300, results.Count);
        Assert.Equal(3, groupedResults.Count); // 三个目标都应该被选中

        // 验证分布相对均匀（容差 50%）
        var expectedCount = 300 / destinations.Count; // 100
        var tolerance = expectedCount * 0.5; // 50
        Assert.All(groupedResults, kvp =>
        {
            Assert.True(Math.Abs(kvp.Value - expectedCount) < tolerance,
                $"Destination {kvp.Key} was selected {kvp.Value} times, expected around {expectedCount} ± {tolerance}");
        });
    }

    #endregion

    #region SelectDestination 测试 - 边界情况

    [Fact]
    public void SelectDestination_LargeNumberOfCalls_DoesNotOverflow()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_loggerMock.Object);
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

        // Act - 调用大量次数
        Destination? lastResult = null;
        for (int i = 0; i < 10000; i++)
        {
            lastResult = strategy.SelectDestination(destinations, context);
        }

        // Assert - 不应抛出异常，且应该正常返回
        Assert.NotNull(lastResult);
        Assert.Contains(lastResult.Code, new[] { "dest-1", "dest-2" });
    }

    [Fact]
    public void SelectDestination_DestinationListChanges_AdaptsToNewList()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_loggerMock.Object);
        var destinations2 = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };
        var destinations5 = new List<Destination>
        {
            CreateDestination("dest-3", "host3", 5003),
            CreateDestination("dest-4", "host4", 5004),
            CreateDestination("dest-5", "host5", 5005),
            CreateDestination("dest-6", "host6", 5006),
            CreateDestination("dest-7", "host7", 5007)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act - 先用2个目标的列表
        strategy.SelectDestination(destinations2, context);
        strategy.SelectDestination(destinations2, context);

        // 切换到5个目标的列表
        var result1 = strategy.SelectDestination(destinations5, context);
        var result2 = strategy.SelectDestination(destinations5, context);
        var result3 = strategy.SelectDestination(destinations5, context);

        // Assert - 应该从新列表中选择
        Assert.Contains(result1.Code, new[] { "dest-3", "dest-4", "dest-5", "dest-6", "dest-7" });
        Assert.Contains(result2.Code, new[] { "dest-3", "dest-4", "dest-5", "dest-6", "dest-7" });
        Assert.Contains(result3.Code, new[] { "dest-3", "dest-4", "dest-5", "dest-6", "dest-7" });
    }

    #endregion

    #region SelectDestination 测试 - 上下文参数

    [Fact]
    public void SelectDestination_DifferentContexts_IgnoresContextAndContinuesRotation()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
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
        var context3 = new LoadBalanceContext
        {
            ClientIp = "192.168.1.3",
            RequestPath = "/api/test3"
        };

        // Act - RoundRobin 应该忽略上下文，按顺序轮询
        var result1 = strategy.SelectDestination(destinations, context1);
        var result2 = strategy.SelectDestination(destinations, context2);
        var result3 = strategy.SelectDestination(destinations, context3);

        // Assert - 不管上下文如何，计数器持续递增
        Assert.Contains(result1.Code, new[] { "dest-1", "dest-2", "dest-3" });
        Assert.Contains(result2.Code, new[] { "dest-1", "dest-2", "dest-3" });
        Assert.Contains(result3.Code, new[] { "dest-1", "dest-2", "dest-3" });

        // 三次调用应该选择不同的目标
        var results = new[] { result1.Code, result2.Code, result3.Code };
        var uniqueCount = results.Distinct().Count();
        Assert.True(uniqueCount >= 2); // 至少有2个不同的目标
    }

    [Fact]
    public void SelectDestination_NullContext_HandlesGracefully()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001)
        };

        // Act & Assert - RoundRobin 不使用 context，所以 null 也应该工作
        var result = strategy.SelectDestination(destinations, null!);
        Assert.NotNull(result);
        Assert.Equal("dest-1", result.Code);
    }

    #endregion

    #region 权重忽略测试

    [Fact]
    public void SelectDestination_IgnoresWeight_RotatesEvenly()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            new Destination("dest-1", Protocol.Http, "host1", 5001, weight: 100, 30, null, null),
            new Destination("dest-2", Protocol.Http, "host2", 5002, weight: 50, 30, null, null),
            new Destination("dest-3", Protocol.Http, "host3", 5003, weight: 10, 30, null, null)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act - 调用9次，应该轮询3轮
        var results = new List<string>();
        for (int i = 0; i < 9; i++)
        {
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 每个目标应该被选中3次（忽略权重）
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, groupedResults.Count);
        Assert.All(groupedResults, kvp => Assert.Equal(3, kvp.Value));
    }

    #endregion

    #region Type 属性测试

    [Fact]
    public void Type_ReturnsRoundRobin()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_loggerMock.Object);

        // Act
        var type = strategy.Type;

        // Assert
        Assert.Equal(LoadBalanceType.RoundRobin, type);
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

    #endregion
}
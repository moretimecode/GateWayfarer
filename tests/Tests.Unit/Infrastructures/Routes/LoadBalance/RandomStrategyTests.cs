using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Infrastructure.Routes.LoadBalance;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Unit.Infrastructure.Routes.LoadBalance;

/// <summary>
/// RandomStrategy 单元测试
/// </summary>
public class RandomStrategyTests
{
    private readonly Mock<ILogger<RandomStrategy>> _loggerMock;

    public RandomStrategyTests()
    {
        _loggerMock = new Mock<ILogger<RandomStrategy>>();
    }

    #region SelectDestination 测试 - 基础功能

    [Fact]
    public void SelectDestination_SingleDestination_ReturnsThatDestination()
    {
        // Arrange
        var strategy = new RandomStrategy(_loggerMock.Object);
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
    public void SelectDestination_MultipleDestinations_ReturnsOneOfThem()
    {
        // Arrange
        var strategy = new RandomStrategy(_loggerMock.Object);
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
        var result = strategy.SelectDestination(destinations, context);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(result.Code, new[] { "dest-1", "dest-2", "dest-3" });
    }

    [Fact]
    public void SelectDestination_MultipleCalls_ReturnsRandomDistribution()
    {
        // Arrange
        var strategy = new RandomStrategy(_loggerMock.Object);
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

        // Act - 调用多次
        var results = new List<string>();
        for (int i = 0; i < 300; i++)
        {
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 应该有随机分布
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        // 验证所有目标都被选中
        Assert.Equal(3, groupedResults.Count);
        Assert.All(groupedResults, kvp => Assert.True(kvp.Value > 0));

        // 验证分布相对均匀（容差 40%，因为是随机的）
        var expectedCount = 300 / destinations.Count; // 100
        var tolerance = expectedCount * 0.4; // 40
        Assert.All(groupedResults, kvp =>
        {
            Assert.True(Math.Abs(kvp.Value - expectedCount) < tolerance,
                $"Destination {kvp.Key} was selected {kvp.Value} times, expected around {expectedCount} ± {tolerance}");
        });
    }

    #endregion

    #region SelectDestination 测试 - 异常情况

    [Fact]
    public void SelectDestination_EmptyList_ThrowsInvalidOperationException()
    {
        // Arrange
        var strategy = new RandomStrategy(_loggerMock.Object);
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
        var strategy = new RandomStrategy(_loggerMock.Object);
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

    #region SelectDestination 测试 - 随机性验证

    [Fact]
    public void SelectDestination_ConsecutiveCalls_CanReturnDifferentResults()
    {
        // Arrange
        var strategy = new RandomStrategy(_loggerMock.Object);
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

        // Act - 连续调用10次
        var results = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 应该有不同的结果（随机性）
        var uniqueCount = results.Distinct().Count();
        Assert.True(uniqueCount > 1, "Random strategy should return different destinations");
    }

    [Fact]
    public void SelectDestination_TwoDestinations_BothShouldBeSelected()
    {
        // Arrange
        var strategy = new RandomStrategy(_loggerMock.Object);
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

        // Act - 调用多次
        var results = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 两个目标都应该被选中
        Assert.Contains("dest-1", results);
        Assert.Contains("dest-2", results);
    }

    #endregion

    #region SelectDestination 测试 - 上下文独立性

    [Fact]
    public void SelectDestination_DifferentContexts_IndependentSelection()
    {
        // Arrange
        var strategy = new RandomStrategy(_loggerMock.Object);
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

        // Act - 相同上下文可能返回不同结果（随机性）
        var result1 = strategy.SelectDestination(destinations, context1);
        var result2 = strategy.SelectDestination(destinations, context1); // 相同上下文
        var result3 = strategy.SelectDestination(destinations, context2);

        // Assert - 随机策略不依赖上下文，每次都是独立选择
        Assert.Contains(result1.Code, new[] { "dest-1", "dest-2", "dest-3" });
        Assert.Contains(result2.Code, new[] { "dest-1", "dest-2", "dest-3" });
        Assert.Contains(result3.Code, new[] { "dest-1", "dest-2", "dest-3" });
    }

    [Fact]
    public void SelectDestination_NullContext_HandlesGracefully()
    {
        // Arrange
        var strategy = new RandomStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001)
        };

        // Act & Assert - Random 不使用 context
        var result = strategy.SelectDestination(destinations, null!);
        Assert.NotNull(result);
        Assert.Equal("dest-1", result.Code);
    }

    #endregion

    #region SelectDestination 测试 - 权重忽略

    [Fact]
    public void SelectDestination_IgnoresWeight_RandomDistribution()
    {
        // Arrange
        var strategy = new RandomStrategy(_loggerMock.Object);
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

        // Act - 调用多次
        var results = new List<string>();
        for (int i = 0; i < 300; i++)
        {
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 应该忽略权重，均匀分布
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, groupedResults.Count);

        // 验证每个目标都被选中，且分布相对均匀（忽略权重）
        var expectedCount = 300 / destinations.Count; // 100
        var tolerance = expectedCount * 0.5; // 50% 容差
        Assert.All(groupedResults, kvp =>
        {
            Assert.True(Math.Abs(kvp.Value - expectedCount) < tolerance,
                $"Destination {kvp.Key} was selected {kvp.Value} times, expected around {expectedCount} (ignoring weight)");
        });
    }

    #endregion

    #region SelectDestination 测试 - 并发安全

    [Fact]
    public void SelectDestination_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var strategy = new RandomStrategy(_loggerMock.Object);
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

        // Assert - 应该随机分布
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(300, results.Count);
        Assert.Equal(3, groupedResults.Count); // 三个目标都应该被选中

        // 验证分布相对均匀（容差 50%，因为是随机+并发）
        var expectedCount = 300 / destinations.Count; // 100
        var tolerance = expectedCount * 0.5; // 50
        Assert.All(groupedResults, kvp =>
        {
            Assert.True(Math.Abs(kvp.Value - expectedCount) < tolerance,
                $"Destination {kvp.Key} was selected {kvp.Value} times, expected around {expectedCount} ± {tolerance}");
        });
    }

    #endregion

    #region Type 属性测试

    [Fact]
    public void Type_ReturnsRandom()
    {
        // Arrange
        var strategy = new RandomStrategy(_loggerMock.Object);

        // Act
        var type = strategy.Type;

        // Assert
        Assert.Equal(LoadBalanceType.Random, type);
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
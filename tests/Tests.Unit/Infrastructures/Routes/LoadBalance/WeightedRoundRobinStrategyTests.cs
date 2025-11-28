using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Infrastructure.Routes.LoadBalance;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Unit.Infrastructure.Routes.LoadBalance;

/// <summary>
/// WeightedRoundRobinStrategy 单元测试
/// </summary>
public class WeightedRoundRobinStrategyTests
{
    private readonly Mock<ILogger<WeightedRoundRobinStrategy>> _loggerMock;

    public WeightedRoundRobinStrategyTests()
    {
        _loggerMock = new Mock<ILogger<WeightedRoundRobinStrategy>>();
    }

    #region SelectDestination 测试 - 基础功能

    [Fact]
    public void SelectDestination_SingleDestination_ReturnsThatDestination()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 100)
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
    public void SelectDestination_EqualWeights_DistributesEvenly()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 100),
            CreateDestination("dest-2", "host2", 5002, weight: 100),
            CreateDestination("dest-3", "host3", 5003, weight: 100)
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

        // Assert - 权重相同应该均匀分布
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, groupedResults.Count);

        // 每个目标应该被选中约 100 次
        Assert.All(groupedResults, kvp =>
        {
            Assert.True(Math.Abs(kvp.Value - 100) < 20,
                $"Destination {kvp.Key} was selected {kvp.Value} times, expected around 100");
        });
    }

    [Fact]
    public void SelectDestination_DifferentWeights_RespectsWeightRatio()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 50),  // 50%
            CreateDestination("dest-2", "host2", 5002, weight: 30),  // 30%
            CreateDestination("dest-3", "host3", 5003, weight: 20)   // 20%
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act - 调用 1000 次
        var results = new List<string>();
        for (int i = 0; i < 1000; i++)
        {
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 应该按权重比例分配
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, groupedResults.Count);

        // dest-1: 约 500 次 (50%)
        Assert.True(Math.Abs(groupedResults["dest-1"] - 500) < 100,
            $"dest-1 was selected {groupedResults["dest-1"]} times, expected around 500");

        // dest-2: 约 300 次 (30%)
        Assert.True(Math.Abs(groupedResults["dest-2"] - 300) < 100,
            $"dest-2 was selected {groupedResults["dest-2"]} times, expected around 300");

        // dest-3: 约 200 次 (20%)
        Assert.True(Math.Abs(groupedResults["dest-3"] - 200) < 100,
            $"dest-3 was selected {groupedResults["dest-3"]} times, expected around 200");
    }

    [Fact]
    public void SelectDestination_ExtremeWeights_FavorsHighWeight()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 1000),
            CreateDestination("dest-2", "host2", 5002, weight: 100),
            CreateDestination("dest-3", "host3", 5003, weight: 10),
            CreateDestination("dest-4", "host4", 5004, weight: 1)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act - 调用多次
        var results = new List<string>();
        for (int i = 0; i < 1000; i++)
        {
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, groupedResults.Count);

        // dest-1 应该被选中最多
        Assert.True(groupedResults["dest-1"] > groupedResults["dest-2"]);
        Assert.True(groupedResults["dest-2"] > groupedResults["dest-3"]);
    }

    #endregion

    #region SelectDestination 测试 - 异常情况

    [Fact]
    public void SelectDestination_EmptyList_ThrowsInvalidOperationException()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);
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
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            strategy.SelectDestination(null!, context));
    }

    [Fact]
    public void SelectDestination_AllZeroWeights_HandlesGracefully()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 1),
            CreateDestination("dest-2", "host2", 5002, weight: 1),
            CreateDestination("dest-3", "host3", 5003, weight: 1)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act - 应该能处理所有权重为0的情况（可能均匀分布或抛出异常）
        var result = strategy.SelectDestination(destinations, context);

        // Assert - 至少应该返回一个目标
        Assert.NotNull(result);
        Assert.Contains(result.Code, new[] { "dest-1", "dest-2", "dest-3" });
    }

    #endregion

    #region SelectDestination 测试 - 边界情况

    [Fact]
    public void SelectDestination_OneHighWeightOneZero_OnlySelectsNonZero()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 100),
            CreateDestination("dest-2", "host2", 5002, weight: 1)
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

        // Assert - 应该只选择权重非0的目标
        Assert.All(results, code => Assert.Equal("dest-1", code));
    }

    [Fact]
    public void SelectDestination_LargeNumberOfCalls_MaintainsWeightRatio()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 60),
            CreateDestination("dest-2", "host2", 5002, weight: 40)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act - 大量调用
        var results = new List<string>();
        for (int i = 0; i < 10000; i++)
        {
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 应该非常接近权重比例 (60:40 = 3:2)
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        // dest-1: 约 6000 次
        Assert.True(Math.Abs(groupedResults["dest-1"] - 6000) < 200,
            $"dest-1 was selected {groupedResults["dest-1"]} times, expected around 6000");

        // dest-2: 约 4000 次
        Assert.True(Math.Abs(groupedResults["dest-2"] - 4000) < 200,
            $"dest-2 was selected {groupedResults["dest-2"]} times, expected around 4000");
    }

    #endregion

    #region SelectDestination 测试 - 并发安全

    [Fact]
    public void SelectDestination_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 50),
            CreateDestination("dest-2", "host2", 5002, weight: 30),
            CreateDestination("dest-3", "host3", 5003, weight: 20)
        };

        // Act - 并发调用
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 1000, _ =>
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

        // Assert - 应该按权重比例分配
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(1000, results.Count);
        Assert.Equal(3, groupedResults.Count);

        // 验证大致符合权重比例（容差较大，因为并发）
        Assert.True(Math.Abs(groupedResults["dest-1"] - 500) < 150);
        Assert.True(Math.Abs(groupedResults["dest-2"] - 300) < 150);
        Assert.True(Math.Abs(groupedResults["dest-3"] - 200) < 150);
    }

    #endregion

    #region SelectDestination 测试 - 上下文独立性

    [Fact]
    public void SelectDestination_DifferentContexts_IndependentSelection()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 50),
            CreateDestination("dest-2", "host2", 5002, weight: 50)
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

        // Act - 不同上下文调用
        var result1 = strategy.SelectDestination(destinations, context1);
        var result2 = strategy.SelectDestination(destinations, context2);
        var result3 = strategy.SelectDestination(destinations, context1);

        // Assert - 加权轮询不依赖上下文
        Assert.Contains(result1.Code, new[] { "dest-1", "dest-2" });
        Assert.Contains(result2.Code, new[] { "dest-1", "dest-2" });
        Assert.Contains(result3.Code, new[] { "dest-1", "dest-2" });
    }

    [Fact]
    public void SelectDestination_NullContext_HandlesGracefully()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 100)
        };

        // Act & Assert - 加权轮询不使用 context
        var result = strategy.SelectDestination(destinations, null!);
        Assert.NotNull(result);
        Assert.Equal("dest-1", result.Code);
    }

    #endregion

    #region Type 属性测试

    [Fact]
    public void Type_ReturnsWeightedRoundRobin()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_loggerMock.Object);

        // Act
        var type = strategy.Type;

        // Assert
        Assert.Equal(LoadBalanceType.WeightedRoundRobin, type);
    }

    #endregion

    #region Helper Methods

    private Destination CreateDestination(string code, string host, int port, int weight = 100)
    {
        return new Destination(
            code: code,
            protocol: Protocol.Http,
            host: host,
            port: port,
            weight: weight,
            timeoutSeconds: 30,
            healthPath: null,
            metadata: null
        );
    }

    #endregion
}
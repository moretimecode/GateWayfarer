using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Infrastructure.Routes.LoadBalance;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Unit.Infrastructure.Routes.LoadBalance;

/// <summary>
/// WeightedRandomStrategy 单元测试
/// </summary>
public class WeightedRandomStrategyTests
{
    private readonly Mock<ILogger<WeightedRandomStrategy>> _loggerMock;

    public WeightedRandomStrategyTests()
    {
        _loggerMock = new Mock<ILogger<WeightedRandomStrategy>>();
    }

    #region SelectDestination 测试 - 基础功能

    [Fact]
    public void SelectDestination_SingleDestination_ReturnsThatDestination()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
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
    public void SelectDestination_MultipleDestinations_ReturnsOneOfThem()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
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

        // Act
        var result = strategy.SelectDestination(destinations, context);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(result.Code, new[] { "dest-1", "dest-2", "dest-3" });
    }

    [Fact]
    public void SelectDestination_EqualWeights_RandomDistribution()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
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

        // Assert - 权重相同应该相对均匀分布
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, groupedResults.Count);

        // 每个目标应该被选中约 100 次（容差 40%，因为是随机）
        var expectedCount = 100;
        var tolerance = expectedCount * 0.4;
        Assert.All(groupedResults, kvp =>
        {
            Assert.True(Math.Abs(kvp.Value - expectedCount) < tolerance,
                $"Destination {kvp.Key} was selected {kvp.Value} times, expected around {expectedCount} ± {tolerance}");
        });
    }

    [Fact]
    public void SelectDestination_DifferentWeights_RespectsWeightRatio()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
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

        // Assert - 应该按权重比例分配（但有随机性）
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, groupedResults.Count);

        // dest-1: 约 500 次 (50%)，容差 15%
        Assert.True(Math.Abs(groupedResults["dest-1"] - 500) < 150,
            $"dest-1 was selected {groupedResults["dest-1"]} times, expected around 500");

        // dest-2: 约 300 次 (30%)，容差 15%
        Assert.True(Math.Abs(groupedResults["dest-2"] - 300) < 150,
            $"dest-2 was selected {groupedResults["dest-2"]} times, expected around 300");

        // dest-3: 约 200 次 (20%)，容差 15%
        Assert.True(Math.Abs(groupedResults["dest-3"] - 200) < 150,
            $"dest-3 was selected {groupedResults["dest-3"]} times, expected around 200");
    }

    [Fact]
    public void SelectDestination_ExtremeWeights_FavorsHighWeight()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 100),
            CreateDestination("dest-2", "host2", 5002, weight: 10),
            CreateDestination("dest-3", "host3", 5003, weight: 1)
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
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
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
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
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
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
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

        // Act
        var result = strategy.SelectDestination(destinations, context);

        // Assert - 至少应该返回一个目标
        Assert.NotNull(result);
        Assert.Contains(result.Code, new[] { "dest-1", "dest-2", "dest-3" });
    }

    #endregion

    #region SelectDestination 测试 - 随机性验证

    [Fact]
    public void SelectDestination_ConsecutiveCalls_CanReturnDifferentResults()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 50),
            CreateDestination("dest-2", "host2", 5002, weight: 50)
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
        Assert.True(uniqueCount > 1, "Weighted random strategy should return different destinations");
    }

    [Fact]
    public void SelectDestination_SameContext_DifferentResults()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 50),
            CreateDestination("dest-2", "host2", 5002, weight: 50)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "127.0.0.1",
            RequestPath = "/api/test"
        };

        // Act - 使用相同上下文多次调用
        var results = new HashSet<string>();
        for (int i = 0; i < 20; i++)
        {
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 应该选中两个不同的目标（随机性）
        Assert.True(results.Count > 1);
    }

    #endregion

    #region SelectDestination 测试 - 边界情况

    [Fact]
    public void SelectDestination_OneHighWeightOneLow_FavorsHighWeight()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 99),
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

        // Assert - dest-1 应该被选中更多
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.True(groupedResults["dest-1"] > groupedResults.GetValueOrDefault("dest-2", 0));
    }

    [Fact]
    public void SelectDestination_VeryLargeWeights_HandlesCorrectly()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 1000000),
            CreateDestination("dest-2", "host2", 5002, weight: 500000)
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

        // Assert - 应该按比例分配 (2:1)
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(2, groupedResults.Count);

        // dest-1 应该约为 dest-2 的 2 倍（容差较大因为随机）
        var ratio = (double)groupedResults["dest-1"] / groupedResults["dest-2"];
        Assert.True(Math.Abs(ratio - 2.0) < 0.8, $"Ratio was {ratio}, expected around 2.0");
    }

    [Fact]
    public void SelectDestination_LargeNumberOfCalls_MaintainsWeightRatio()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
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

        // Assert - 应该接近权重比例 (60:40 = 3:2)
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        // dest-1: 约 6000 次，容差 5%
        Assert.True(Math.Abs(groupedResults["dest-1"] - 6000) < 500,
            $"dest-1 was selected {groupedResults["dest-1"]} times, expected around 6000");

        // dest-2: 约 4000 次，容差 5%
        Assert.True(Math.Abs(groupedResults["dest-2"] - 4000) < 500,
            $"dest-2 was selected {groupedResults["dest-2"]} times, expected around 4000");
    }

    #endregion

    #region SelectDestination 测试 - 并发安全

    [Fact]
    public void SelectDestination_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
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

        // Assert - 应该按权重比例分配（容差较大）
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(1000, results.Count);
        Assert.Equal(3, groupedResults.Count);

        // 验证大致符合权重比例（容差 25%，因为随机+并发）
        Assert.True(Math.Abs(groupedResults["dest-1"] - 500) < 200,
            $"dest-1 was selected {groupedResults["dest-1"]} times, expected around 500");
        Assert.True(Math.Abs(groupedResults["dest-2"] - 300) < 150,
            $"dest-2 was selected {groupedResults["dest-2"]} times, expected around 300");
        Assert.True(Math.Abs(groupedResults["dest-3"] - 200) < 150,
            $"dest-3 was selected {groupedResults["dest-3"]} times, expected around 200");
    }

    #endregion

    #region SelectDestination 测试 - 上下文独立性

    [Fact]
    public void SelectDestination_DifferentContexts_IndependentRandomSelection()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
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

        // Act
        var result1 = strategy.SelectDestination(destinations, context1);
        var result2 = strategy.SelectDestination(destinations, context2);
        var result3 = strategy.SelectDestination(destinations, context1);

        // Assert - 加权随机不依赖上下文
        Assert.Contains(result1.Code, new[] { "dest-1", "dest-2" });
        Assert.Contains(result2.Code, new[] { "dest-1", "dest-2" });
        Assert.Contains(result3.Code, new[] { "dest-1", "dest-2" });
    }

    [Fact]
    public void SelectDestination_NullContext_HandlesGracefully()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001, weight: 100)
        };

        // Act & Assert
        var result = strategy.SelectDestination(destinations, null!);
        Assert.NotNull(result);
        Assert.Equal("dest-1", result.Code);
    }

    #endregion

    #region Type 属性测试

    [Fact]
    public void Type_ReturnsWeightedRandom()
    {
        // Arrange
        var strategy = new WeightedRandomStrategy(_loggerMock.Object);

        // Act
        var type = strategy.Type;

        // Assert
        Assert.Equal(LoadBalanceType.WeightedRandom, type);
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
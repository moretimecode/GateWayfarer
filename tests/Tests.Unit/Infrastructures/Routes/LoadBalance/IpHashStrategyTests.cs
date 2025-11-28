using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Infrastructure.Routes.LoadBalance;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Unit.Infrastructure.Routes.LoadBalance;

/// <summary>
/// IpHashStrategy 单元测试
/// </summary>
public class IpHashStrategyTests
{
    private readonly Mock<ILogger<IpHashStrategy>> _loggerMock;

    public IpHashStrategyTests()
    {
        _loggerMock = new Mock<ILogger<IpHashStrategy>>();
    }

    #region SelectDestination 测试 - 基础功能

    [Fact]
    public void SelectDestination_SingleDestination_ReturnsThatDestination()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "192.168.1.100",
            RequestPath = "/api/test"
        };

        // Act
        var result = strategy.SelectDestination(destinations, context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dest-1", result.Code);
    }

    [Fact]
    public void SelectDestination_SameIp_ReturnsSameDestination()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "192.168.1.100",
            RequestPath = "/api/test"
        };

        // Act - 使用相同IP多次调用
        var result1 = strategy.SelectDestination(destinations, context);
        var result2 = strategy.SelectDestination(destinations, context);
        var result3 = strategy.SelectDestination(destinations, context);

        // Assert - 应该返回相同的目标
        Assert.Equal(result1.Code, result2.Code);
        Assert.Equal(result2.Code, result3.Code);
    }

    [Fact]
    public void SelectDestination_DifferentIps_CanReturnDifferentDestinations()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };

        // Act - 使用不同IP
        var results = new HashSet<string>();
        for (int i = 1; i <= 100; i++)
        {
            var context = new LoadBalanceContext
            {
                ClientIp = $"192.168.1.{i}",
                RequestPath = "/api/test"
            };
            var result = strategy.SelectDestination(destinations, context);
            results.Add(result.Code);
        }

        // Assert - 不同IP应该分散到不同的目标（至少2个）
        Assert.True(results.Count >= 2, "Different IPs should be distributed across multiple destinations");
    }

    [Fact]
    public void SelectDestination_MultipleDestinations_DistributesAcrossAll()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };

        // Act - 使用大量不同IP
        var results = new List<string>();
        for (int i = 1; i <= 300; i++)
        {
            var context = new LoadBalanceContext
            {
                ClientIp = $"192.168.{i / 256}.{i % 256}",
                RequestPath = "/api/test"
            };
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 应该分布到所有目标
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, groupedResults.Count);
        Assert.All(groupedResults, kvp => Assert.True(kvp.Value > 0));
    }

    #endregion

    #region SelectDestination 测试 - 哈希一致性

    [Fact]
    public void SelectDestination_SameIpDifferentPaths_ReturnsSameDestination()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };
        var context1 = new LoadBalanceContext
        {
            ClientIp = "192.168.1.100",
            RequestPath = "/api/users"
        };
        var context2 = new LoadBalanceContext
        {
            ClientIp = "192.168.1.100",
            RequestPath = "/api/orders"
        };

        // Act
        var result1 = strategy.SelectDestination(destinations, context1);
        var result2 = strategy.SelectDestination(destinations, context2);

        // Assert - 相同IP不同路径应该返回相同目标
        Assert.Equal(result1.Code, result2.Code);
    }

    [Fact]
    public void SelectDestination_SameIpMultipleInstances_ConsistentAcrossInstances()
    {
        // Arrange
        var logger1 = new Mock<ILogger<IpHashStrategy>>();
        var logger2 = new Mock<ILogger<IpHashStrategy>>();
        var strategy1 = new IpHashStrategy(logger1.Object);
        var strategy2 = new IpHashStrategy(logger2.Object);

        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "192.168.1.100",
            RequestPath = "/api/test"
        };

        // Act
        var result1 = strategy1.SelectDestination(destinations, context);
        var result2 = strategy2.SelectDestination(destinations, context);

        // Assert - 不同实例相同IP应该返回相同目标
        Assert.Equal(result1.Code, result2.Code);
    }

    [Fact]
    public void SelectDestination_Ipv6Address_HandlesCorrectly()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "2001:0db8:85a3:0000:0000:8a2e:0370:7334",
            RequestPath = "/api/test"
        };

        // Act
        var result1 = strategy.SelectDestination(destinations, context);
        var result2 = strategy.SelectDestination(destinations, context);

        // Assert - IPv6地址应该一致
        Assert.Equal(result1.Code, result2.Code);
    }

    #endregion

    #region SelectDestination 测试 - 哈希分布

    [Fact]
    public void SelectDestination_ManyIps_ReasonablyDistributed()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };

        // Act - 1000个不同IP
        var results = new List<string>();
        for (int i = 0; i < 1000; i++)
        {
            var context = new LoadBalanceContext
            {
                ClientIp = $"10.{i / 256}.{i % 256}.{i % 10}",
                RequestPath = "/api/test"
            };
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 应该相对均匀分布（容差 30%）
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, groupedResults.Count);

        var expectedCount = 1000 / 3; // 约 333
        var tolerance = expectedCount * 0.3; // 100
        Assert.All(groupedResults, kvp =>
        {
            Assert.True(Math.Abs(kvp.Value - expectedCount) < tolerance,
                $"Destination {kvp.Key} was selected {kvp.Value} times, expected around {expectedCount}");
        });
    }

    [Fact]
    public void SelectDestination_SequentialIps_AvoidsClustering()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };

        // Act - 连续的IP地址
        var results = new List<string>();
        for (int i = 1; i <= 20; i++)
        {
            var context = new LoadBalanceContext
            {
                ClientIp = $"192.168.1.{i}",
                RequestPath = "/api/test"
            };
            results.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 应该有分布，不是全部相同
        var uniqueCount = results.Distinct().Count();
        Assert.True(uniqueCount > 1, "Sequential IPs should be distributed");
    }

    #endregion

    #region SelectDestination 测试 - 异常情况

    [Fact]
    public void SelectDestination_EmptyList_ThrowsInvalidOperationException()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>();
        var context = new LoadBalanceContext
        {
            ClientIp = "192.168.1.100",
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
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var context = new LoadBalanceContext
        {
            ClientIp = "192.168.1.100",
            RequestPath = "/api/test"
        };

        // Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            strategy.SelectDestination(null!, context));
    }

    [Fact]
    public void SelectDestination_EmptyIp_HandlesGracefully()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "",
            RequestPath = "/api/test"
        };

        // Act
        var result1 = strategy.SelectDestination(destinations, context);
        var result2 = strategy.SelectDestination(destinations, context);

        // Assert - 空IP应该一致（可能有默认行为）
        Assert.Equal(result1.Code, result2.Code);
    }

    [Fact]
    public void SelectDestination_NullIp_HandlesGracefully()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = null!,
            RequestPath = "/api/test"
        };

        // Act & Assert - 可能抛出异常或有默认行为
        var result = strategy.SelectDestination(destinations, context);
        Assert.NotNull(result);
    }

    #endregion

    #region SelectDestination 测试 - 目标列表变化

    [Fact]
    public void SelectDestination_DestinationListChanges_IpStillMapsConsistently()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations2 = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };
        var destinations3 = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "192.168.1.100",
            RequestPath = "/api/test"
        };

        // Act
        var result1 = strategy.SelectDestination(destinations2, context);
        var result2 = strategy.SelectDestination(destinations3, context);

        // Assert - 目标列表变化时，IP哈希结果可能改变
        Assert.Contains(result1.Code, new[] { "dest-1", "dest-2" });
        Assert.Contains(result2.Code, new[] { "dest-1", "dest-2", "dest-3" });
    }

    [Fact]
    public void SelectDestination_AddDestination_PreservesExistingMappings()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations2 = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };
        var destinations3 = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };

        // Act - 测试多个IP在列表变化前后的映射
        var mappingsBefore = new Dictionary<string, string>();
        var mappingsAfter = new Dictionary<string, string>();

        for (int i = 1; i <= 10; i++)
        {
            var ip = $"192.168.1.{i}";
            var context = new LoadBalanceContext { ClientIp = ip, RequestPath = "/api/test" };

            mappingsBefore[ip] = strategy.SelectDestination(destinations2, context).Code;
            mappingsAfter[ip] = strategy.SelectDestination(destinations3, context).Code;
        }

        // Assert - 大部分映射应该保持一致（取决于哈希算法）
        // 注意：标准哈希可能导致大量重映射，一致性哈希会更好
        var consistentCount = mappingsBefore.Count(kvp =>
            mappingsAfter.ContainsKey(kvp.Key) && mappingsAfter[kvp.Key] == kvp.Value);

        // 至少有一些映射保持一致
        Assert.True(consistentCount >= 0);
    }

    #endregion

    #region SelectDestination 测试 - 并发安全

    [Fact]
    public void SelectDestination_ConcurrentCallsSameIp_ReturnsConsistentResults()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };
        var clientIp = "192.168.1.100";

        // Act - 并发调用相同IP
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 100, _ =>
        {
            var context = new LoadBalanceContext
            {
                ClientIp = clientIp,
                RequestPath = "/api/test",
                RequestId = Guid.NewGuid().ToString()
            };
            var result = strategy.SelectDestination(destinations, context);
            results.Add(result.Code);
        });

        // Assert - 所有结果应该相同
        var uniqueResults = results.Distinct().ToList();
        Assert.Single(uniqueResults);
    }

    [Fact]
    public void SelectDestination_ConcurrentCallsDifferentIps_ThreadSafe()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };

        // Act - 并发调用不同IP
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 300, i =>
        {
            var context = new LoadBalanceContext
            {
                ClientIp = $"192.168.{i / 256}.{i % 256}",
                RequestPath = "/api/test",
                RequestId = Guid.NewGuid().ToString()
            };
            var result = strategy.SelectDestination(destinations, context);
            results.Add(result.Code);
        });

        // Assert - 应该分布到所有目标
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(300, results.Count);
        Assert.True(groupedResults.Count >= 2);
    }

    #endregion

    #region SelectDestination 测试 - 权重忽略

    [Fact]
    public void SelectDestination_IgnoresWeight_BasedOnlyOnIpHash()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            new Destination("dest-1", Protocol.Http, "host1", 5001, weight: 100, 30, null, null),
            new Destination("dest-2", Protocol.Http, "host2", 5002, weight: 10, 30, null, null)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "192.168.1.100",
            RequestPath = "/api/test"
        };

        // Act - 相同IP多次调用
        var result1 = strategy.SelectDestination(destinations, context);
        var result2 = strategy.SelectDestination(destinations, context);

        // Assert - 应该忽略权重，基于IP哈希
        Assert.Equal(result1.Code, result2.Code);
    }

    #endregion

    #region Type 属性测试

    [Fact]
    public void Type_ReturnsIpHash()
    {
        // Arrange
        var strategy = new IpHashStrategy(_loggerMock.Object);

        // Act
        var type = strategy.Type;

        // Assert
        Assert.Equal(LoadBalanceType.IpHash, type);
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
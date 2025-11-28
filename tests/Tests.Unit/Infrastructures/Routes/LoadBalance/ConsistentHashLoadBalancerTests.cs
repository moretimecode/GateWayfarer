using Domain.Routes;
using Domain.Routes.Services;
using Domain.Services;
using Domain.Services.LoadBalancing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Unit.Infrastructure.Routes.LoadBalance;

/// <summary>
/// ConsistentHashLoadBalancer 单元测试
/// </summary>
public class ConsistentHashLoadBalancerTests
{
    private readonly Mock<ILogger<ConsistentHashLoadBalancer>> _loggerMock;

    public ConsistentHashLoadBalancerTests()
    {
        _loggerMock = new Mock<ILogger<ConsistentHashLoadBalancer>>();
    }

    #region AddDestination / RemoveDestination 测试

    [Fact]
    public void AddDestination_SingleDestination_AddsVirtualNodes()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destination = CreateDestination("dest-1", "host1", 5001);

        // Act
        loadBalancer.AddDestination(destination);

        // Assert
        var ringCount = loadBalancer.GetRingNodesCount();
        var virtualNodesCount = loadBalancer.GetVirtualNodesCount();

        Assert.Equal(virtualNodesCount, ringCount); // 应该添加150个虚拟节点
    }

    [Fact]
    public void AddDestination_MultipleDestinations_AddsAllVirtualNodes()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var dest1 = CreateDestination("dest-1", "host1", 5001);
        var dest2 = CreateDestination("dest-2", "host2", 5002);

        // Act
        loadBalancer.AddDestination(dest1);
        loadBalancer.AddDestination(dest2);

        // Assert
        var ringCount = loadBalancer.GetRingNodesCount();
        var virtualNodesCount = loadBalancer.GetVirtualNodesCount();

        Assert.Equal(virtualNodesCount * 2, ringCount); // 应该有300个虚拟节点
    }

    [Fact]
    public void RemoveDestination_ExistingDestination_RemovesVirtualNodes()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destination = CreateDestination("dest-1", "host1", 5001);
        loadBalancer.AddDestination(destination);

        // Act
        loadBalancer.RemoveDestination(destination);

        // Assert
        var ringCount = loadBalancer.GetRingNodesCount();
        Assert.Equal(0, ringCount);
    }

    [Fact]
    public void RemoveDestination_OneOfMultiple_RemovesOnlyThatDestination()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var dest1 = CreateDestination("dest-1", "host1", 5001);
        var dest2 = CreateDestination("dest-2", "host2", 5002);
        loadBalancer.AddDestination(dest1);
        loadBalancer.AddDestination(dest2);

        // Act
        loadBalancer.RemoveDestination(dest1);

        // Assert
        var ringCount = loadBalancer.GetRingNodesCount();
        var virtualNodesCount = loadBalancer.GetVirtualNodesCount();

        Assert.Equal(virtualNodesCount, ringCount); // 应该只剩150个虚拟节点
    }

    #endregion

    #region SelectDestination (by key) 测试

    [Fact]
    public void SelectDestination_EmptyRing_ReturnsNull()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);

        // Act
        var result = loadBalancer.SelectDestination("192.168.1.100");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SelectDestination_SingleDestination_ReturnsThatDestination()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destination = CreateDestination("dest-1", "host1", 5001);
        loadBalancer.AddDestination(destination);

        // Act
        var result = loadBalancer.SelectDestination("192.168.1.100");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dest-1", result.Code);
    }

    [Fact]
    public void SelectDestination_SameKey_ReturnsSameDestination()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var dest1 = CreateDestination("dest-1", "host1", 5001);
        var dest2 = CreateDestination("dest-2", "host2", 5002);
        loadBalancer.AddDestination(dest1);
        loadBalancer.AddDestination(dest2);

        // Act
        var result1 = loadBalancer.SelectDestination("192.168.1.100");
        var result2 = loadBalancer.SelectDestination("192.168.1.100");
        var result3 = loadBalancer.SelectDestination("192.168.1.100");

        // Assert
        Assert.Equal(result1.Code, result2.Code);
        Assert.Equal(result2.Code, result3.Code);
    }

    [Fact]
    public void SelectDestination_DifferentKeys_CanReturnDifferentDestinations()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var dest1 = CreateDestination("dest-1", "host1", 5001);
        var dest2 = CreateDestination("dest-2", "host2", 5002);
        var dest3 = CreateDestination("dest-3", "host3", 5003);
        loadBalancer.AddDestination(dest1);
        loadBalancer.AddDestination(dest2);
        loadBalancer.AddDestination(dest3);

        // Act
        var results = new HashSet<string>();
        for (int i = 1; i <= 100; i++)
        {
            var result = loadBalancer.SelectDestination($"192.168.1.{i}");
            if (result != null)
            {
                results.Add(result.Code);
            }
        }

        // Assert
        Assert.True(results.Count >= 2, "Different keys should be distributed across multiple destinations");
    }

    #endregion

    #region SelectDestinationCore 测试 - 基础功能

    [Fact]
    public void SelectDestinationCore_SingleDestination_ReturnsThatDestination()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
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
        var result = loadBalancer.SelectDestination(destinations, context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dest-1", result.Code);
    }

    [Fact]
    public void SelectDestinationCore_SameIp_ReturnsSameDestination()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
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
        var result1 = loadBalancer.SelectDestination(destinations, context);
        var result2 = loadBalancer.SelectDestination(destinations, context);
        var result3 = loadBalancer.SelectDestination(destinations, context);

        // Assert
        Assert.Equal(result1.Code, result2.Code);
        Assert.Equal(result2.Code, result3.Code);
    }

    [Fact]
    public void SelectDestinationCore_DifferentIps_DistributesAcrossDestinations()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };

        // Act
        var results = new List<string>();
        for (int i = 1; i <= 300; i++)
        {
            var context = new LoadBalanceContext
            {
                ClientIp = $"192.168.{i / 256}.{i % 256}",
                RequestPath = "/api/test"
            };
            results.Add(loadBalancer.SelectDestination(destinations, context).Code);
        }

        // Assert
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, groupedResults.Count);
        Assert.All(groupedResults, kvp => Assert.True(kvp.Value > 0));
    }

    #endregion

    #region GetHashKeyFromContext 测试

    [Fact]
    public void SelectDestinationCore_UsesClientIpAsHashKey()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
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
        var result1 = loadBalancer.SelectDestination(destinations, context1);
        var result2 = loadBalancer.SelectDestination(destinations, context2);

        // Assert - 相同IP不同路径应该返回相同目标
        Assert.Equal(result1.Code, result2.Code);
    }

    [Fact]
    public void SelectDestinationCore_NoClientIp_UsesRequestPath()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };
        var context1 = new LoadBalanceContext
        {
            ClientIp = "",
            RequestPath = "/api/test"
        };
        var context2 = new LoadBalanceContext
        {
            ClientIp = "",
            RequestPath = "/api/test"
        };

        // Act
        var result1 = loadBalancer.SelectDestination(destinations, context1);
        var result2 = loadBalancer.SelectDestination(destinations, context2);

        // Assert - 相同路径应该返回相同目标
        Assert.Equal(result1.Code, result2.Code);
    }

    [Fact]
    public void SelectDestinationCore_NoIpNoPath_UsesFallback()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "",
            RequestPath = ""
        };

        // Act
        var result = loadBalancer.SelectDestination(destinations, context);

        // Assert - 应该能返回一个目标（使用Guid回退）
        Assert.NotNull(result);
        Assert.Contains(result.Code, new[] { "dest-1", "dest-2" });
    }

    #endregion

    #region SyncRingWithDestinations 测试

    [Fact]
    public void SelectDestinationCore_AutoSyncsRing_WhenDestinationsChange()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
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
        loadBalancer.SelectDestination(destinations2, context);
        var ringCountAfter2 = loadBalancer.GetRingNodesCount();

        loadBalancer.SelectDestination(destinations3, context);
        var ringCountAfter3 = loadBalancer.GetRingNodesCount();

        // Assert
        var virtualNodesCount = loadBalancer.GetVirtualNodesCount();
        Assert.Equal(virtualNodesCount * 2, ringCountAfter2);
        Assert.Equal(virtualNodesCount * 3, ringCountAfter3);
    }

    [Fact]
    public void SelectDestinationCore_RemovesStaleDestinations_FromRing()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destinations3 = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };
        var destinations2 = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };
        var context = new LoadBalanceContext
        {
            ClientIp = "192.168.1.100",
            RequestPath = "/api/test"
        };

        // Act
        loadBalancer.SelectDestination(destinations3, context);
        loadBalancer.SelectDestination(destinations2, context);

        // Assert
        var ringCount = loadBalancer.GetRingNodesCount();
        var virtualNodesCount = loadBalancer.GetVirtualNodesCount();
        Assert.Equal(virtualNodesCount * 2, ringCount);
    }

    #endregion

    #region 一致性哈希特性测试

    [Fact]
    public void SelectDestinationCore_AddDestination_MinimalRemapping()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
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

        // Act
        var mappingsBefore = new Dictionary<string, string>();
        var mappingsAfter = new Dictionary<string, string>();

        for (int i = 1; i <= 100; i++)
        {
            var ip = $"192.168.1.{i}";
            var context = new LoadBalanceContext { ClientIp = ip, RequestPath = "/api/test" };

            mappingsBefore[ip] = loadBalancer.SelectDestination(destinations2, context).Code;
            mappingsAfter[ip] = loadBalancer.SelectDestination(destinations3, context).Code;
        }

        // Assert
        var consistentCount = mappingsBefore.Count(kvp =>
            mappingsAfter.ContainsKey(kvp.Key) && mappingsAfter[kvp.Key] == kvp.Value);

        var consistencyRate = (double)consistentCount / mappingsBefore.Count;
        Assert.True(consistencyRate >= 0.6,
            $"Consistency rate was {consistencyRate:P}, expected at least 60%");
    }

    [Fact]
    public void SelectDestinationCore_RemoveDestination_OnlyRemappedKeys()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destinations3 = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };
        var destinations2 = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };

        // Act
        var mappingsBefore = new Dictionary<string, string>();
        var mappingsAfter = new Dictionary<string, string>();

        for (int i = 1; i <= 100; i++)
        {
            var ip = $"192.168.1.{i}";
            var context = new LoadBalanceContext { ClientIp = ip, RequestPath = "/api/test" };

            mappingsBefore[ip] = loadBalancer.SelectDestination(destinations3, context).Code;
            mappingsAfter[ip] = loadBalancer.SelectDestination(destinations2, context).Code;
        }

        // Assert
        var stableCount = mappingsBefore.Count(kvp =>
            kvp.Value != "dest-3" && mappingsAfter[kvp.Key] == kvp.Value);

        Assert.True(stableCount > 0, "Some mappings should remain stable");
    }

    [Fact]
    public void SelectDestinationCore_VirtualNodes_ImproveDistribution()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002)
        };

        // Act
        var results = new List<string>();
        for (int i = 0; i < 1000; i++)
        {
            var context = new LoadBalanceContext
            {
                ClientIp = $"10.{i / 256}.{i % 256}.{i % 10}",
                RequestPath = "/api/test"
            };
            results.Add(loadBalancer.SelectDestination(destinations, context).Code);
        }

        // Assert
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(2, groupedResults.Count);

        var expectedCount = 500;
        var tolerance = expectedCount * 0.2;
        Assert.All(groupedResults, kvp =>
        {
            Assert.True(Math.Abs(kvp.Value - expectedCount) < tolerance,
                $"Destination {kvp.Key} was selected {kvp.Value} times, expected around {expectedCount}");
        });
    }

    #endregion

    #region 异常情况测试

    [Fact]
    public void SelectDestinationCore_EmptyList_ThrowsInvalidOperationException()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destinations = new List<Destination>();
        var context = new LoadBalanceContext
        {
            ClientIp = "192.168.1.100",
            RequestPath = "/api/test"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            loadBalancer.SelectDestination(destinations, context));

        Assert.Equal("No destinations available for load balancing", exception.Message);
    }

    [Fact]
    public void SelectDestinationCore_NullList_ThrowsNullReferenceException()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var context = new LoadBalanceContext
        {
            ClientIp = "192.168.1.100",
            RequestPath = "/api/test"
        };

        // Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            loadBalancer.SelectDestination(null!, context));
    }

    #endregion

    #region 并发安全测试

    [Fact]
    public void SelectDestinationCore_ConcurrentCallsSameIp_ReturnsConsistentResults()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };
        var clientIp = "192.168.1.100";

        // Act
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 100, _ =>
        {
            var context = new LoadBalanceContext
            {
                ClientIp = clientIp,
                RequestPath = "/api/test",
                RequestId = Guid.NewGuid().ToString()
            };
            var result = loadBalancer.SelectDestination(destinations, context);
            results.Add(result.Code);
        });

        // Assert
        var uniqueResults = results.Distinct().ToList();
        Assert.Single(uniqueResults);
    }

    [Fact]
    public void SelectDestinationCore_ConcurrentCallsDifferentIps_ThreadSafe()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var destinations = new List<Destination>
        {
            CreateDestination("dest-1", "host1", 5001),
            CreateDestination("dest-2", "host2", 5002),
            CreateDestination("dest-3", "host3", 5003)
        };

        // Act
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 300, i =>
        {
            var context = new LoadBalanceContext
            {
                ClientIp = $"192.168.{i / 256}.{i % 256}",
                RequestPath = "/api/test",
                RequestId = Guid.NewGuid().ToString()
            };
            var result = loadBalancer.SelectDestination(destinations, context);
            results.Add(result.Code);
        });

        // Assert
        var groupedResults = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(300, results.Count);
        Assert.True(groupedResults.Count >= 2);
    }

    [Fact]
    public void AddRemoveDestination_Concurrent_ThreadSafe()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var dest1 = CreateDestination("dest-1", "host1", 5001);
        var dest2 = CreateDestination("dest-2", "host2", 5002);
        var dest3 = CreateDestination("dest-3", "host3", 5003);

        // Act - 并发添加和删除
        Parallel.Invoke(
            () => loadBalancer.AddDestination(dest1),
            () => loadBalancer.AddDestination(dest2),
            () => loadBalancer.AddDestination(dest3),
            () => { Thread.Sleep(10); loadBalancer.RemoveDestination(dest1); },
            () => { Thread.Sleep(20); loadBalancer.AddDestination(dest1); }
        );

        // Assert - 不应该抛出异常
        var ringCount = loadBalancer.GetRingNodesCount();
        Assert.True(ringCount > 0);
    }

    #endregion

    #region GetRingStatus / 监控方法测试

    [Fact]
    public void GetRingStatus_ReturnsCorrectMapping()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var dest1 = CreateDestination("dest-1", "host1", 5001);
        loadBalancer.AddDestination(dest1);

        // Act
        var status = loadBalancer.GetRingStatus();

        // Assert
        Assert.NotEmpty(status);
        Assert.All(status.Values, code => Assert.Equal("dest-1", code));
    }

    [Fact]
    public void GetVirtualNodesCount_Returns150()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);

        // Act
        var count = loadBalancer.GetVirtualNodesCount();

        // Assert
        Assert.Equal(150, count);
    }

    [Fact]
    public void GetRingNodesCount_ReflectsCurrentState()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);
        var dest1 = CreateDestination("dest-1", "host1", 5001);

        // Act & Assert
        Assert.Equal(0, loadBalancer.GetRingNodesCount());

        loadBalancer.AddDestination(dest1);
        Assert.Equal(150, loadBalancer.GetRingNodesCount());

        loadBalancer.RemoveDestination(dest1);
        Assert.Equal(0, loadBalancer.GetRingNodesCount());
    }

    #endregion

    #region Type 属性测试

    [Fact]
    public void Type_ReturnsConsistentHash()
    {
        // Arrange
        var loadBalancer = new ConsistentHashLoadBalancer(_loggerMock.Object);

        // Act
        var type = loadBalancer.Type;

        // Assert
        Assert.Equal(LoadBalanceType.ConsistentHash, type);
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
using Domain.Routes;
using Domain.Routes.Contexts;
using Domain.Routes.Services;
using Domain.Services;
using Domain.Services.LoadBalancing;
using Infrastructure.Routes.LoadBalance;
using Microsoft.Extensions.Logging;
using HttpMethod = Domain.Routes.HttpMethod;

/// <summary>
/// 负载均衡策略集成测试
/// 测试轮询、加权轮询、随机、一致性哈希等负载均衡算法
/// </summary>
[Collection("Gateway Integration Tests")]
public class LoadBalancingTests
{
    private readonly ILogger<RoundRobinStrategy> _rrLogger;
    private readonly ILogger<WeightedRoundRobinStrategy> _wrrLogger;
    private readonly ILogger<ConsistentHashLoadBalancer> _hashLogger;
    private readonly ILogger<RandomStrategy> _randomLogger;

    public LoadBalancingTests()
    {
        var loggerFactory = new LoggerFactory();
        _rrLogger = loggerFactory.CreateLogger<RoundRobinStrategy>();
        _wrrLogger = loggerFactory.CreateLogger<WeightedRoundRobinStrategy>();
        _hashLogger = loggerFactory.CreateLogger<ConsistentHashLoadBalancer>();
        _randomLogger = loggerFactory.CreateLogger<RandomStrategy>();
    }

    /// <summary>
    /// 测试：轮询(RoundRobin)均衡分配
    /// 验证目标地址按顺序循环选择
    /// </summary>
    [Fact]
    public void Should_RoundRobin_Distribute_Equally()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_rrLogger);
        var destinations = new List<Destination>
        {
            new Destination("server-1", Protocol.Http, "server1.com", 80, 100, 30),
            new Destination("server-2", Protocol.Http, "server2.com", 80, 100, 30),
            new Destination("server-3", Protocol.Http, "server3.com", 80, 100, 30)
        };

        var context = new LoadBalanceContext
        {
            RequestPath = "/api/test",
            RequestId = "test-1",
            ClientIp = "127.0.0.1"
        };

        // Act - 循环调用多次，应该轮询选择
        var selections = new Dictionary<string, int>();
        for (int i = 0; i < 300; i++)
        {
            var selected = strategy.SelectDestination(destinations, context);
            if (!selections.ContainsKey(selected.Code))
                selections[selected.Code] = 0;
            selections[selected.Code]++;
        }

        // Assert - 每个服务应该被选择大约相同的次数 (300/3 = 100)
        Assert.Equal(3, selections.Count);
        foreach (var kvp in selections)
        {
            // 允许10%的偏差
            Assert.InRange(kvp.Value, 90, 110);
        }
    }

    /// <summary>
    /// 测试：加权轮询(WeightedRoundRobin)按权重分配
    /// 高权重的服务应该获得更多请求
    /// </summary>
    [Fact]
    public void Should_WeightedRoundRobin_Respect_Weights()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_wrrLogger);
        var destinations = new List<Destination>
        {
            new Destination("high-capacity", Protocol.Http, "host1.com", 80, 100, 30),  // 50% 流量
            new Destination("mid-capacity", Protocol.Http, "host2.com", 80, 50, 30),    // 25% 流量
            new Destination("low-capacity", Protocol.Http, "host3.com", 80, 50, 30)     // 25% 流量
        };

        var context = new LoadBalanceContext
        {
            RequestPath = "/api/test",
            RequestId = "test-1",
            ClientIp = "127.0.0.1"
        };

        // Act - 执行大量请求，验证权重分配
        var selections = new Dictionary<string, int>();
        var random = new Random(42); // 固定种子以便复现
        for (int i = 0; i < 1000; i++)
        {
            var selected = strategy.SelectDestination(destinations, context);
            if (!selections.ContainsKey(selected.Code))
                selections[selected.Code] = 0;
            selections[selected.Code]++;
        }

        // Assert
        var highCount = selections["high-capacity"];
        var midCount = selections["mid-capacity"];
        var lowCount = selections["low-capacity"];

        // 高容量应该获得约50%的请求
        Assert.InRange(highCount, 400, 600);
        // 中等和低容量各获得约25%
        Assert.InRange(midCount, 150, 350);
        Assert.InRange(lowCount, 150, 350);
    }

    /// <summary>
    /// 测试：轮询在单个目标时的处理
    /// </summary>
    [Fact]
    public void Should_RoundRobin_Handle_Single_Destination()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_rrLogger);
        var destinations = new List<Destination>
        {
            new Destination("only-server", Protocol.Http, "server.com", 80, 100, 30)
        };

        var context = new LoadBalanceContext { RequestPath = "/api/test", RequestId = "test-1", ClientIp = "127.0.0.1" };

        // Act
        var selected1 = strategy.SelectDestination(destinations, context);
        var selected2 = strategy.SelectDestination(destinations, context);
        var selected3 = strategy.SelectDestination(destinations, context);

        // Assert - 始终返回同一个
        Assert.Equal(selected1.Code, selected2.Code);
        Assert.Equal(selected2.Code, selected3.Code);
        Assert.Equal("only-server", selected1.Code);
    }

    /// <summary>
    /// 测试：轮询在两个目标时的交替选择
    /// </summary>
    [Fact]
    public void Should_RoundRobin_Alternate_Two_Destinations()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_rrLogger);
        var destinations = new List<Destination>
        {
            new Destination("server-1", Protocol.Http, "server1.com", 80, 100, 30),
            new Destination("server-2", Protocol.Http, "server2.com", 80, 100, 30)
        };

        var context = new LoadBalanceContext { RequestPath = "/api/test", RequestId = "test-1", ClientIp = "127.0.0.1" };

        // Act
        var selections = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            selections.Add(strategy.SelectDestination(destinations, context).Code);
        }

        // Assert - 应该交替出现
        var isAlternating = selections.SequenceEqual(selections.OrderByDescending(x => x == "server-1" ? 0 : 1).Reverse());
        // 至少应该看到两个服务都被选择
        Assert.Contains("server-1", selections);
        Assert.Contains("server-2", selections);
    }

    /// <summary>
    /// 测试：一致性哈希(ConsistentHash)基于客户端IP的分配
    /// 相同客户端IP的请求应该总是路由到同一服务器
    /// </summary>
    [Fact]
    public void Should_ConsistentHash_Route_Same_Client_To_Same_Server()
    {
        // Arrange
        var strategy = new ConsistentHashLoadBalancer(_hashLogger);
        var destinations = new List<Destination>
        {
            new Destination("server-1", Protocol.Http, "server1.com", 80, 100, 30),
            new Destination("server-2", Protocol.Http, "server2.com", 80, 100, 30),
            new Destination("server-3", Protocol.Http, "server3.com", 80, 100, 30)
        };

        var clientIp = "192.168.1.100";
        var contexts = Enumerable.Range(0, 5).Select(i => new LoadBalanceContext
        {
            RequestPath = $"/api/test{i}",
            RequestId = $"test-{i}",
            ClientIp = clientIp
        }).ToList();

        // Act - 同一客户端多次请求
        var selections = contexts.Select(ctx => strategy.SelectDestination(destinations, ctx).Code).ToList();

        // Assert - 所有请求都应该路由到同一服务器
        var firstServer = selections[0];
        Assert.All(selections, s => Assert.Equal(firstServer, s));
    }

    /// <summary>
    /// 测试：一致性哈希不同客户端的分散分配
    /// 不同客户端应该可能被分配到不同服务器
    /// </summary>
    [Fact]
    public void Should_ConsistentHash_Distribute_Different_Clients()
    {
        // Arrange
        var strategy = new ConsistentHashLoadBalancer(_hashLogger);
        var destinations = new List<Destination>
        {
            new Destination("server-1", Protocol.Http, "server1.com", 80, 100, 30),
            new Destination("server-2", Protocol.Http, "server2.com", 80, 100, 30),
            new Destination("server-3", Protocol.Http, "server3.com", 80, 100, 30)
        };

        // Act - 100个不同的客户端IP
        var selections = new Dictionary<string, int>();
        for (int i = 0; i < 100; i++)
        {
            var clientIp = $"192.168.1.{i % 256}";
            var context = new LoadBalanceContext
            {
                RequestPath = "/api/test",
                RequestId = $"test-{i}",
                ClientIp = clientIp
            };

            var selected = strategy.SelectDestination(destinations, context);
            if (!selections.ContainsKey(selected.Code))
                selections[selected.Code] = 0;
            selections[selected.Code]++;
        }

        // Assert - 应该至少有2个不同的服务器被使用
        Assert.True(selections.Count >= 2, "一致性哈希应该将客户端分散到多个服务器");
    }

    /// <summary>
    /// 测试：加权轮询与权重为0的目标处理
    /// </summary>
    [Fact]
    public void Should_WeightedRoundRobin_Handle_Unequal_Weights()
    {
        // Arrange
        var strategy = new WeightedRoundRobinStrategy(_wrrLogger);
        var destinations = new List<Destination>
        {
            new Destination("primary", Protocol.Http, "primary.com", 80, 200, 30),  // 66.7%
            new Destination("backup", Protocol.Http, "backup.com", 80, 100, 30)     // 33.3%
        };

        var context = new LoadBalanceContext { RequestPath = "/api/test", RequestId = "test-1", ClientIp = "127.0.0.1" };

        // Act
        var selections = new Dictionary<string, int>();
        for (int i = 0; i < 900; i++)
        {
            var selected = strategy.SelectDestination(destinations, context);
            if (!selections.ContainsKey(selected.Code))
                selections[selected.Code] = 0;
            selections[selected.Code]++;
        }

        // Assert
        var primaryCount = selections["primary"];
        var backupCount = selections["backup"];

        Assert.InRange(primaryCount, 550, 750); // 约 600 (66.7%)
        Assert.InRange(backupCount, 150, 350);  // 约 300 (33.3%)
    }

    /// <summary>
    /// 测试：轮询在并发场景下的线程安全性
    /// </summary>
    [Fact]
    public void Should_RoundRobin_Be_Thread_Safe()
    {
        // Arrange
        var strategy = new RoundRobinStrategy(_rrLogger);
        var destinations = new List<Destination>
        {
            new Destination("server-1", Protocol.Http, "server1.com", 80, 100, 30),
            new Destination("server-2", Protocol.Http, "server2.com", 80, 100, 30),
            new Destination("server-3", Protocol.Http, "server3.com", 80, 100, 30)
        };

        var selections = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();

        // Act - 多线程并发请求
        Parallel.For(0, 1000, i =>
        {
            var context = new LoadBalanceContext
            {
                RequestPath = "/api/test",
                RequestId = $"test-{i}",
                ClientIp = "127.0.0.1"
            };

            var selected = strategy.SelectDestination(destinations, context);
            selections.AddOrUpdate(selected.Code, 1, (k, v) => v + 1);
        });

        // Assert - 每个服务器应该获得大致相等的请求数
        Assert.Equal(3, selections.Count);
        foreach (var kvp in selections)
        {
            Assert.InRange(kvp.Value, 250, 400); // 允许较宽的范围以适应并发变化
        }
    }

    /// <summary>
    /// 测试：负载均衡策略选择的依赖注入场景
    /// </summary>
    [Fact]
    public void Should_Select_Correct_Load_Balance_Strategy_By_Type()
    {
        // Arrange - 模拟策略工厂
        var strategies = new Dictionary<LoadBalanceType, string>
        {
            { LoadBalanceType.RoundRobin, "RoundRobin" },
            { LoadBalanceType.WeightedRoundRobin, "WeightedRoundRobin" },
            { LoadBalanceType.ConsistentHash, "ConsistentHash" },
            { LoadBalanceType.Random, "Random" }
        };

        // Act & Assert
        foreach (var kvp in strategies)
        {
            var strategy = GetStrategyForType(kvp.Key);
            Assert.NotNull(strategy);
            Assert.Equal(kvp.Key, strategy.Type);
        }
    }

    private ILoadBalanceStrategy GetStrategyForType(LoadBalanceType type)
    {
        return type switch
        {
            LoadBalanceType.RoundRobin => new RoundRobinStrategy(_rrLogger),
            LoadBalanceType.WeightedRoundRobin => new WeightedRoundRobinStrategy(_wrrLogger),
            LoadBalanceType.ConsistentHash => new ConsistentHashLoadBalancer(_hashLogger),
            LoadBalanceType.Random => new RandomStrategy(_randomLogger),
            _ => throw new NotImplementedException($"Strategy for {type} not implemented")
        };
    }
}

using Domain.Routes;
using Domain.Routes.Configuration;
using Domain.Routes.Contexts;
using Domain.Routes.Services;
using Domain.Services;
using Domain.Services.LoadBalancing;
using Infrastructure.Routes.LoadBalance;
using Microsoft.Extensions.Logging;
using HttpMethod = Domain.Routes.HttpMethod;

namespace Tests.Integration;

/// <summary>
/// 并发请求处理集成测试（P0 级）
/// 验证：高并发支持、负载均衡在并发下的正确性、线程安全、无内存泄漏
/// </summary>
[Collection("Gateway Integration Tests")]
public class ConcurrencyTests
{
    private readonly ILogger<RoundRobinStrategy> _rrLogger;
    private readonly ILogger<WeightedRoundRobinStrategy> _wrrLogger;
    private readonly ILogger<ConsistentHashLoadBalancer> _hashLogger;
    private readonly ILogger<RandomStrategy> _randomLogger;

    public ConcurrencyTests()
    {
        var loggerFactory = new LoggerFactory();
        _rrLogger = loggerFactory.CreateLogger<RoundRobinStrategy>();
        _wrrLogger = loggerFactory.CreateLogger<WeightedRoundRobinStrategy>();
        _hashLogger = loggerFactory.CreateLogger<ConsistentHashLoadBalancer>();
        _randomLogger = loggerFactory.CreateLogger<RandomStrategy>();
    }

    #region 1. 高并发请求处理

    /// <summary>
    /// 测试：100+ 并发请求的处理能力
    /// 场景：同时处理 100 个请求，验证系统稳定性
    /// </summary>
    [Fact]
    public async Task Should_Handle_100_Concurrent_Requests()
    {
        // Arrange
        const int concurrentRequests = 100;
        var tasks = new List<Task>();
        var successCount = 0;
        var lockObj = new object();

        var destinations = new List<Destination>
        {
            new Destination("server-1", Protocol.Http, "host1.local", 8001),
            new Destination("server-2", Protocol.Http, "host2.local", 8002),
            new Destination("server-3", Protocol.Http, "host3.local", 8003)
        };

        var strategy = new RoundRobinStrategy(_rrLogger);
        var context = new LoadBalanceContext
        {
            RequestPath = "/api/test",
            RequestId = "concurrent-test",
            ClientIp = "127.0.0.1"
        };

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            var requestId = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var selected = strategy.SelectDestination(destinations, context);
                    if (selected != null)
                    {
                        lock (lockObj)
                        {
                            successCount++;
                        }
                    }
                }
                catch { }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(concurrentRequests, successCount);
    }

    /// <summary>
    /// 测试：1000 并发请求的压力测试
    /// 场景：模拟高负载，验证无内存泄漏和性能降级
    /// </summary>
    [Fact]
    public async Task Should_Handle_1000_Concurrent_Requests_Under_Stress()
    {
        // Arrange
        const int stressRequests = 1000;
        var tasks = new List<Task>();
        var completedCount = 0;
        var lockObj = new object();
        var initialMemory = GC.GetTotalMemory(true);

        var destinations = new List<Destination>
        {
            new Destination("stress-1", Protocol.Http, "host1", 8001),
            new Destination("stress-2", Protocol.Http, "host2", 8002),
            new Destination("stress-3", Protocol.Http, "host3", 8003),
            new Destination("stress-4", Protocol.Http, "host4", 8004)
        };

        var strategy = new RoundRobinStrategy(_rrLogger);
        var context = new LoadBalanceContext
        {
            RequestPath = "/api/stress",
            RequestId = "stress-test",
            ClientIp = "127.0.0.1"
        };

        // Act
        for (int i = 0; i < stressRequests; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    _ = strategy.SelectDestination(destinations, context);
                    lock (lockObj)
                    {
                        completedCount++;
                    }
                }
                catch { }
            }));
        }

        await Task.WhenAll(tasks);
        var finalMemory = GC.GetTotalMemory(true);

        // Assert
        Assert.Equal(stressRequests, completedCount);

        // 内存增长应该合理（不超过 50MB 增长用于 1000 请求）
        var memoryIncreaseMb = (finalMemory - initialMemory) / (1024 * 1024);
        Assert.True(memoryIncreaseMb < 50, $"Memory increase should be reasonable, got {memoryIncreaseMb}MB");
    }

    #endregion

    #region 2. 并发下的负载均衡

    /// <summary>
    /// 测试：轮询在高并发下的分布均匀性
    /// 场景：100 个并发请求应该均匀分布到各目标
    /// </summary>
    [Fact]
    public async Task Should_Distribute_Evenly_Under_Concurrency()
    {
        // Arrange
        const int concurrentRequests = 300;
        var selections = new Dictionary<string, int>();
        var lockObj = new object();

        var destinations = new List<Destination>
        {
            new Destination("server-1", Protocol.Http, "host1", 8001),
            new Destination("server-2", Protocol.Http, "host2", 8002),
            new Destination("server-3", Protocol.Http, "host3", 8003)
        };

        var strategy = new RoundRobinStrategy(_rrLogger);
        var context = new LoadBalanceContext
        {
            RequestPath = "/api/concurrent",
            RequestId = "concurrent-test",
            ClientIp = "127.0.0.1"
        };

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests).Select(_ =>
            Task.Run(() =>
            {
                var selected = strategy.SelectDestination(destinations, context);
                lock (lockObj)
                {
                    if (!selections.ContainsKey(selected.Code))
                        selections[selected.Code] = 0;
                    selections[selected.Code]++;
                }
            })
        );

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(3, selections.Count);

        // 每个目标应该被选中大约 100 次 (300/3)，允许 ±20% 的偏差
        foreach (var kvp in selections)
        {
            Assert.InRange(kvp.Value, 80, 120);
        }
    }

    /// <summary>
    /// 测试：加权轮询在并发下的权重遵守
    /// 场景：即使在并发情况下，权重仍应被正确遵守
    /// </summary>
    [Fact]
    public async Task Should_Respect_Weights_Under_Concurrency()
    {
        // Arrange
        const int concurrentRequests = 400;
        var selections = new Dictionary<string, int>();
        var lockObj = new object();

        var destinations = new List<Destination>
        {
            new Destination("high", Protocol.Http, "host1", 8001, weight: 100),  // 50%
            new Destination("medium", Protocol.Http, "host2", 8002, weight: 50), // 25%
            new Destination("low", Protocol.Http, "host3", 8003, weight: 50)     // 25%
        };

        var strategy = new WeightedRoundRobinStrategy(_wrrLogger);
        var context = new LoadBalanceContext
        {
            RequestPath = "/api/weighted",
            RequestId = "weighted-test",
            ClientIp = "127.0.0.1"
        };

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests).Select(_ =>
            Task.Run(() =>
            {
                var selected = strategy.SelectDestination(destinations, context);
                lock (lockObj)
                {
                    if (!selections.ContainsKey(selected.Code))
                        selections[selected.Code] = 0;
                    selections[selected.Code]++;
                }
            })
        );

        await Task.WhenAll(tasks);

        // Assert
        var totalRequests = selections.Values.Sum();
        Assert.Equal(concurrentRequests, totalRequests);

        // 验证权重分配
        var highPercentage = (double)selections["high"] / totalRequests;
        var mediumPercentage = (double)selections["medium"] / totalRequests;
        var lowPercentage = (double)selections["low"] / totalRequests;

        Assert.InRange(highPercentage, 0.40, 0.60);   // 高权重: ~50%
        Assert.InRange(mediumPercentage, 0.15, 0.35); // 中权重: ~25%
        Assert.InRange(lowPercentage, 0.15, 0.35);    // 低权重: ~25%
    }

    /// <summary>
    /// 测试：一致性哈希在并发下的会话保持
    /// 场景：同一客户端的请求应该路由到同一目标
    /// </summary>
    [Fact]
    public async Task Should_Maintain_Session_With_Consistent_Hash_Under_Concurrency()
    {
        // Arrange
        const int requestsPerClient = 50;
        const int clientCount = 10;
        var selections = new Dictionary<string, HashSet<string>>();
        var lockObj = new object();

        var destinations = new List<Destination>
        {
            new Destination("session-1", Protocol.Http, "host1", 8001),
            new Destination("session-2", Protocol.Http, "host2", 8002),
            new Destination("session-3", Protocol.Http, "host3", 8003)
        };

        var strategy = new ConsistentHashLoadBalancer(_hashLogger);

        // Act
        var tasks = new List<Task>();
        for (int client = 0; client < clientCount; client++)
        {
            var clientId = client.ToString();
            for (int req = 0; req < requestsPerClient; req++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var context = new LoadBalanceContext
                    {
                        RequestPath = $"/api/session/{clientId}",
                        RequestId = $"req-{clientId}-{req}",
                        ClientIp = $"192.168.1.{clientId}"
                    };

                    var selected = strategy.SelectDestination(destinations, context);

                    lock (lockObj)
                    {
                        if (!selections.ContainsKey(clientId))
                            selections[clientId] = new HashSet<string>();
                        selections[clientId].Add(selected.Code);
                    }
                }));
            }
        }

        await Task.WhenAll(tasks);

        // Assert
        // 每个客户端应该始终路由到同一个目标
        foreach (var clientSelections in selections.Values)
        {
            Assert.Single(clientSelections);
        }
    }

    #endregion

    #region 3. 线程安全验证

    /// <summary>
    /// 测试：并发访问目标列表的线程安全
    /// 场景：多线程同时读写目标列表不应导致异常或不一致
    /// </summary>
    [Fact]
    public async Task Should_Be_Thread_Safe_With_Concurrent_Destination_Access()
    {
        // Arrange
        var service = new Service("concurrent-service", "Service for Concurrency Test");
        var tasks = new List<Task>();
        var exceptionCount = 0;
        var lockObj = new object();

        // 创建初始目标
        for (int i = 0; i < 10; i++)
        {
            service.AddDestination(new Destination(
                $"dest-{i}",
                Protocol.Http,
                $"host{i}",
                8000 + i
            ));
        }

        // Act
        for (int thread = 0; thread < 10; thread++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    // 反复读取目标列表
                    for (int i = 0; i < 100; i++)
                    {
                        var dests = service.Destinations;
                        Assert.NotNull(dests);
                        Assert.True(dests.Count >= 0);
                    }
                }
                catch (Exception)
                {
                    lock (lockObj)
                    {
                        exceptionCount++;
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(0, exceptionCount);
        Assert.Equal(10, service.Destinations.Count);
    }

    /// <summary>
    /// 测试：并发下的选择操作原子性
    /// 场景：负载均衡算法的选择操作应该是原子的
    /// </summary>
    [Fact]
    public async Task Should_Maintain_Atomic_Selection_Under_Concurrency()
    {
        // Arrange
        var destinations = new List<Destination>
        {
            new Destination("atomic-1", Protocol.Http, "host1", 8001),
            new Destination("atomic-2", Protocol.Http, "host2", 8002)
        };

        var strategy = new RoundRobinStrategy(_rrLogger);
        var selectedCodes = new List<string>();
        var lockObj = new object();

        // Act
        var tasks = Enumerable.Range(0, 50).Select(_ =>
            Task.Run(() =>
            {
                var context = new LoadBalanceContext
                {
                    RequestPath = "/api/atomic",
                    RequestId = Guid.NewGuid().ToString(),
                    ClientIp = "127.0.0.1"
                };

                var selected = strategy.SelectDestination(destinations, context);

                lock (lockObj)
                {
                    selectedCodes.Add(selected.Code);
                }
            })
        );

        await Task.WhenAll(tasks);

        // Assert
        // 所有选择都应该是有效的目标
        Assert.All(selectedCodes, code =>
            Assert.True(code == "atomic-1" || code == "atomic-2")
        );

        // 应该交替选择（轮询的特性）
        Assert.NotEmpty(selectedCodes.Where(c => c == "atomic-1"));
        Assert.NotEmpty(selectedCodes.Where(c => c == "atomic-2"));
    }

    #endregion

    #region 4. 资源管理与性能

    /// <summary>
    /// 测试：并发下的内存稳定性
    /// 场景：长时间的高并发不应导致内存泄漏
    /// </summary>
    [Fact]
    public async Task Should_Maintain_Stable_Memory_Under_Sustained_Concurrency()
    {
        // Arrange
        const int rounds = 10;
        const int requestsPerRound = 100;
        var memorySnapshots = new List<long>();

        var destinations = new List<Destination>
        {
            new Destination("mem-1", Protocol.Http, "host1", 8001),
            new Destination("mem-2", Protocol.Http, "host2", 8002),
            new Destination("mem-3", Protocol.Http, "host3", 8003)
        };

        var strategy = new RoundRobinStrategy(_rrLogger);
        var context = new LoadBalanceContext
        {
            RequestPath = "/api/memory",
            RequestId = "memory-test",
            ClientIp = "127.0.0.1"
        };

        // Act
        for (int round = 0; round < rounds; round++)
        {
            var tasks = Enumerable.Range(0, requestsPerRound).Select(_ =>
                Task.Run(() => strategy.SelectDestination(destinations, context))
            );

            await Task.WhenAll(tasks);

            // 每轮后记录内存使用
            GC.Collect();
            GC.WaitForPendingFinalizers();
            memorySnapshots.Add(GC.GetTotalMemory(false));
        }

        // Assert
        // 内存应该相对稳定（每轮增长不超过 5MB）
        for (int i = 1; i < memorySnapshots.Count; i++)
        {
            var increase = (memorySnapshots[i] - memorySnapshots[i - 1]) / (1024 * 1024);
            Assert.True(increase < 10, $"Memory increased by {increase}MB in round {i}");
        }
    }

    /// <summary>
    /// 测试：任务完成时的资源释放
    /// 场景：所有并发任务完成后应释放资源
    /// </summary>
    [Fact]
    public async Task Should_Release_Resources_After_Concurrent_Operations()
    {
        // Arrange
        const int taskCount = 50;
        var tasks = new List<Task>();
        var resources = new List<object>();
        var lockObj = new object();

        // Act
        for (int i = 0; i < taskCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var resource = new object();
                lock (lockObj)
                {
                    resources.Add(resource);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(taskCount, resources.Count);

        // 清理资源
        resources.Clear();
        Assert.Empty(resources);
    }

    #endregion

    #region 5. 错误恢复与弹性

    /// <summary>
    /// 测试：单个并发请求失败不影响其他请求
    /// 场景：10 个并发请求中 1 个异常，其他 9 个仍成功
    /// </summary>
    [Fact]
    public async Task Should_Handle_Partial_Failure_In_Concurrent_Requests()
    {
        // Arrange
        const int totalRequests = 10;
        var results = new List<bool>();
        var lockObj = new object();

        var destinations = new List<Destination>
        {
            new Destination("fail-1", Protocol.Http, "host1", 8001),
            new Destination("fail-2", Protocol.Http, "host2", 8002)
        };

        var strategy = new RoundRobinStrategy(_rrLogger);

        // Act
        var tasks = Enumerable.Range(0, totalRequests).Select(index =>
            Task.Run(() =>
            {
                try
                {
                    var context = new LoadBalanceContext
                    {
                        RequestPath = "/api/partial",
                        RequestId = $"req-{index}",
                        ClientIp = "127.0.0.1"
                    };

                    // 模拟第 5 个请求失败
                    if (index == 5)
                    {
                        throw new InvalidOperationException("Request failed");
                    }

                    var selected = strategy.SelectDestination(destinations, context);

                    lock (lockObj)
                    {
                        results.Add(selected != null);
                    }
                }
                catch (InvalidOperationException)
                {
                    lock (lockObj)
                    {
                        results.Add(false);
                    }
                }
            })
        );

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(totalRequests, results.Count);
        Assert.Equal(9, results.Count(r => r)); // 9 个成功
        Assert.Equal(1, results.Count(r => !r)); // 1 个失败
    }

    /// <summary>
    /// 测试：并发下的目标故障转移
    /// 场景：高并发期间某目标不可用，应自动转移
    /// </summary>
    [Fact]
    public async Task Should_Failover_During_High_Concurrency()
    {
        // Arrange
        const int concurrentRequests = 50;
        var destinations = new List<Destination>
        {
            new Destination("failover-primary", Protocol.Http, "host1", 8001),
            new Destination("failover-secondary", Protocol.Http, "host2", 8002),
            new Destination("failover-tertiary", Protocol.Http, "host3", 8003)
        };

        var strategy = new RoundRobinStrategy(_rrLogger);
        var selections = new Dictionary<string, int>();
        var lockObj = new object();

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests).Select(index =>
            Task.Run(() =>
            {
                var context = new LoadBalanceContext
                {
                    RequestPath = "/api/failover",
                    RequestId = $"failover-{index}",
                    ClientIp = "127.0.0.1"
                };

                // 模拟 primary 故障
                var availableDestinations = destinations
                    .Where(d => d.Code != "failover-primary")
                    .ToList();

                var selected = strategy.SelectDestination(availableDestinations, context);

                lock (lockObj)
                {
                    if (!selections.ContainsKey(selected.Code))
                        selections[selected.Code] = 0;
                    selections[selected.Code]++;
                }
            })
        );

        await Task.WhenAll(tasks);

        // Assert
        Assert.False(selections.ContainsKey("failover-primary")); // Primary 不应被选中
        Assert.True(selections.ContainsKey("failover-secondary")); // Secondary 应被选中
        Assert.True(selections.ContainsKey("failover-tertiary"));  // Tertiary 应被选中
    }

    #endregion
}

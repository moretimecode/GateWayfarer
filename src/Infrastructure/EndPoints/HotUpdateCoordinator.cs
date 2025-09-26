using Domain.Routes;
using Domain.Routes.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Threading.Channels;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 热更新协调器实现
    /// 使用Channel进行流控，简化设计，提高性能
    /// </summary>
    public class HotUpdateCoordinator : IHotUpdateCoordinator, IDisposable
    {
        private readonly IOptionsMonitor<Domain.Routes.Configuration.RouteOptions> _optionsMonitor;
        private readonly IDynamicEndpointManager _endpointManager;
        private readonly RegexMatcher _regexMatcher;
        private readonly ILogger<HotUpdateCoordinator> _logger;
        
        // 使用Channel替代信号量，支持队列和背压
        private readonly Channel<HotUpdateRequest> _updateChannel;
        private readonly ChannelWriter<HotUpdateRequest> _updateWriter;
        private readonly ChannelReader<HotUpdateRequest> _updateReader;
        private readonly Task _processingTask;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public HotUpdateCoordinator(
            IOptionsMonitor<Domain.Routes.Configuration.RouteOptions> optionsMonitor,
            IDynamicEndpointManager endpointManager,
            RegexMatcher regexMatcher,
            ILogger<HotUpdateCoordinator> logger)
        {
            _optionsMonitor = optionsMonitor;
            _endpointManager = endpointManager;
            _regexMatcher = regexMatcher;
            _logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();

            var options = new BoundedChannelOptions(capacity: 100)
            {
                FullMode = BoundedChannelFullMode.Wait, 
                SingleReader = true, 
                SingleWriter = false 
            };

            _updateChannel = Channel.CreateBounded<HotUpdateRequest>(options);
            _updateWriter = _updateChannel.Writer;
            _updateReader = _updateChannel.Reader;

            _processingTask = Task.Run(ProcessUpdatesAsync, _cancellationTokenSource.Token);

            _logger.LogInformation("热更新协调器已启动，Channel容量: {Capacity}", options.Capacity);
        }

        /// <summary>
        /// 应用路由配置变更（异步提交到队列）
        /// </summary>
        public async Task<HotUpdateResult> ApplyChangesAsync(List<RouteConfigChangeInfo> changes)
        {
            var taskCompletionSource = new TaskCompletionSource<HotUpdateResult>();
            
            var request = new HotUpdateRequest
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Changes = changes,
                CompletionSource = taskCompletionSource,
                SubmittedAt = DateTime.UtcNow
            };

            try
            {
                await _updateWriter.WriteAsync(request, _cancellationTokenSource.Token);
                
                _logger.LogDebug("热更新请求已提交到队列: {RequestId}, 变更数量: {Count}", 
                    request.Id, changes.Count);

                return await taskCompletionSource.Task;
            }
            catch (OperationCanceledException)
            {
                return new HotUpdateResult
                {
                    Success = false,
                    Message = "热更新操作被取消",
                    Errors = ["操作被取消"]
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "提交热更新请求失败: {RequestId}", request.Id);
                return new HotUpdateResult
                {
                    Success = false,
                    Message = "提交热更新请求失败",
                    Errors = [ex.Message]
                };
            }
        }

        /// <summary>
        /// 后台处理更新请求
        /// </summary>
        private async Task ProcessUpdatesAsync()
        {
            _logger.LogInformation("热更新处理器开始运行");

            await foreach (var request in _updateReader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                var result =  ProcessSingleUpdate(request);
                request.CompletionSource.SetResult(result);
            }

            _logger.LogInformation("热更新处理器已停止");
        }

        /// <summary>
        /// 处理单个更新请求
        /// </summary>
        private  HotUpdateResult ProcessSingleUpdate(HotUpdateRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("🚀 开始处理热更新请求: {RequestId}, 变更数量: {Count}", 
                    request.Id, request.Changes.Count);

                var validationResult = ValidateChanges(request.Changes);
                if (!validationResult.Success)
                {
                    _logger.LogWarning("请求 {RequestId} 验证失败: {Message}", request.Id, validationResult.Message);
                    return validationResult;
                }

                var result =  ApplyChangesInPhases(request.Changes);
                
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.AffectedRoutesCount = request.Changes.Count;

                if (result.Success)
                {
                    _logger.LogInformation("✅ 热更新请求 {RequestId} 成功完成，耗时: {Duration}ms", 
                        request.Id, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogError("❌ 热更新请求 {RequestId} 失败: {Message}", request.Id, result.Message);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "❌ 处理热更新请求 {RequestId} 时发生异常", request.Id);
                
                return new HotUpdateResult
                {
                    Success = false,
                    Errors = [ex.Message],
                    Message = "处理热更新请求时发生异常",
                    Duration = stopwatch.Elapsed
                };
            }
        }

        /// <summary>
        /// 验证变更的合理性（同步方法，快速验证）
        /// </summary>
        private HotUpdateResult ValidateChanges(List<RouteConfigChangeInfo> changes)
        {
            var result = new HotUpdateResult { Success = true };
            
            foreach (var change in changes)
            {
                // 验证路由名称
                if (string.IsNullOrWhiteSpace(change.RouteName))
                {
                    result.Errors.Add("路由名称不能为空");
                    continue;
                }

                // 验证新增和修改的路由配置
                if (change.ChangeType is RouteChangeType.Added or RouteChangeType.Modified or RouteChangeType.Enabled)
                {
                    if (change.NewConfig == null)
                    {
                        result.Errors.Add($"路由 {change.RouteName} 缺少新配置信息");
                        continue;
                    }

                    // 验证路径格式
                    if (string.IsNullOrWhiteSpace(change.NewConfig.Path))
                    {
                        result.Errors.Add($"路由 {change.RouteName} 的路径不能为空");
                    }

                    // 验证正则表达式（如果是正则路由）
                    if (change.NewConfig.MatchType == RouteMatchType.Regex)
                    {
                        try
                        {
                            var regex = new System.Text.RegularExpressions.Regex(change.NewConfig.Path);
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"路由 {change.RouteName} 的正则表达式无效: {ex.Message}");
                        }
                    }

                    // 验证优先级范围
                    if (change.NewConfig.Priority is < 0 or > 10000)
                    {
                        result.Warnings.Add($"路由 {change.RouteName} 的优先级 {change.NewConfig.Priority} 超出建议范围 [0-10000]");
                    }
                }
            }

            result.Success = !result.Errors.Any();
            result.Message = result.Success ? "验证通过" : $"发现 {result.Errors.Count} 个验证错误";

            return result;
        }

        /// <summary>
        /// 创建当前路由状态快照（同步方法）
        /// </summary>
        public RouteSnapshot CreateSnapshot()
        {
            var currentOptions = _optionsMonitor.CurrentValue;
            
            var snapshot = new RouteSnapshot
            {
                CreatedAt = DateTime.UtcNow,
                Routes = currentOptions.Routes?.ToList() ?? [],
                Metadata = new Dictionary<string, object>
                {
                    ["TotalRoutes"] = currentOptions.Routes?.Count ?? 0,
                    ["EnabledRoutes"] = currentOptions.Routes?.Count(r => r.IsEnabled) ?? 0,
                    ["RegexRoutes"] = currentOptions.Routes?.Count(r => r.MatchType == RouteMatchType.Regex) ?? 0
                }
            };

            _logger.LogDebug("创建路由快照: {Version}，包含 {Count} 个路由", 
                snapshot.Version, snapshot.Routes.Count);

            return snapshot;
        }

        /// <summary>
        /// 强制重新加载所有路由
        /// </summary>
        public async Task<HotUpdateResult> ForceReloadAllAsync()
        {
            _logger.LogInformation("🔄 开始强制重新加载所有路由");
            
            // 获取当前所有路由配置
            var currentOptions = _optionsMonitor.CurrentValue;
            var allRoutes = currentOptions.Routes ?? [];

            var changes = allRoutes.Select(route => new RouteConfigChangeInfo
            {
                RouteName = route.Name,
                ChangeType = RouteChangeType.Added,
                NewConfig = route,
                ChangeDescription = $"强制重新加载: {route.Path}"
            }).ToList();

            // 先清空所有现有路由
             _endpointManager.ClearAllEndpoints();
            _logger.LogDebug("已清空所有现有路由");

            // 重新应用所有路由
            return await ApplyChangesAsync(changes);
        }

        /// <summary>
        /// 从快照恢复路由状态
        /// </summary>
        public async Task<HotUpdateResult> RestoreSnapshotAsync(RouteSnapshot snapshot)
        {
            _logger.LogInformation("🔄 开始从快照恢复路由状态: {Version}", snapshot.Version);

            // 先清空现有路由
             _endpointManager.ClearAllEndpoints();

            // 创建恢复变更列表
            var restoreChanges = snapshot.Routes.Select(route => new RouteConfigChangeInfo
            {
                RouteName = route.Name,
                ChangeType = RouteChangeType.Added,
                NewConfig = route,
                ChangeDescription = $"从快照恢复: {route.Path}"
            }).ToList();

            return await ApplyChangesAsync(restoreChanges);
        }

        private HotUpdateResult ApplyChangesInPhases(List<RouteConfigChangeInfo> changes)
        {
            var result = new HotUpdateResult { Success = true };

            try
            {
                // Phase 1: 处理删除和禁用
                var removeChanges = changes.Where(c => 
                    c.ChangeType is RouteChangeType.Removed or RouteChangeType.Disabled).ToList();
                
                if (removeChanges.Any())
                {
                    _logger.LogDebug("Phase 1: 处理 {Count} 个删除/禁用变更", removeChanges.Count);
                     ApplyRemovalChanges(removeChanges, result);
                    if (!result.Success) return result;
                }

                // Phase 2: 处理修改
                var modifyChanges = changes.Where(c => c.ChangeType == RouteChangeType.Modified).ToList();
                if (modifyChanges.Any())
                {
                    _logger.LogDebug("Phase 2: 处理 {Count} 个修改变更", modifyChanges.Count);
                     ApplyModificationChanges(modifyChanges, result);
                    if (!result.Success) return result;
                }

                // Phase 3: 处理新增和启用
                var addChanges = changes.Where(c => 
                    c.ChangeType is RouteChangeType.Added or RouteChangeType.Enabled).ToList();
                
                if (addChanges.Any())
                {
                    _logger.LogDebug("Phase 3: 处理 {Count} 个新增/启用变更", addChanges.Count);
                     ApplyAdditionChanges(addChanges, result);
                }

                result.Message = result.Success ? 
                    $"成功应用 {changes.Count} 个配置变更" : 
                    $"部分变更应用失败，请查看错误详情";

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分阶段应用变更时发生异常");
                result.Success = false;
                result.Errors.Add(ex.Message);
                result.Message = "分阶段应用变更时发生异常";
                return result;
            }
        }

        private  void ApplyRemovalChanges(List<RouteConfigChangeInfo> removeChanges, HotUpdateResult result)
        {
            foreach (var change in removeChanges)
            {
                try
                {
                     _endpointManager.RemoveEndpoint(change.RouteName);
                    _logger.LogDebug("✅ 成功删除路由: {RouteName}", change.RouteName);
                }
                catch (Exception ex)
                {
                    var error = $"删除路由 {change.RouteName} 失败: {ex.Message}";
                    result.Errors.Add(error);
                    _logger.LogError(ex, "删除路由失败: {RouteName}", change.RouteName);
                }
            }
        }

        private  void ApplyModificationChanges(List<RouteConfigChangeInfo> modifyChanges, HotUpdateResult result)
        {
            foreach (var change in modifyChanges)
            {
                try
                {
                    if (change.NewConfig == null)
                    {
                        result.Errors.Add($"路由 {change.RouteName} 缺少新配置");
                        continue;
                    }

                     _endpointManager.UpdateEndpoint(change.RouteName, change.NewConfig);
                    _logger.LogDebug("✅ 成功修改路由: {RouteName}", change.RouteName);
                }
                catch (Exception ex)
                {
                    var error = $"修改路由 {change.RouteName} 失败: {ex.Message}";
                    result.Errors.Add(error);
                    _logger.LogError(ex, "修改路由失败: {RouteName}", change.RouteName);
                }
            }
        }

        private void ApplyAdditionChanges(List<RouteConfigChangeInfo> addChanges, HotUpdateResult result)
        {
            // 先更新正则匹配器
            var regexRoutes = addChanges
                .Where(c => c.NewConfig?.MatchType == RouteMatchType.Regex)
                .Select(c => c.NewConfig!)
                .ToList();

            if (regexRoutes.Any())
            {
                try
                {
                    _regexMatcher.PrecompileRegexRoutes(regexRoutes);
                    _logger.LogDebug("✅ 预编译了 {Count} 个正则路由", regexRoutes.Count);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"预编译正则路由失败: {ex.Message}");
                    _logger.LogError(ex, "预编译正则路由失败");
                    return;
                }
            }

            // 逐个添加路由
            foreach (var change in addChanges)
            {
                try
                {
                    if (change.NewConfig == null)
                    {
                        result.Errors.Add($"路由 {change.RouteName} 缺少配置信息");
                        continue;
                    }

                     _endpointManager.AddEndpoint(change.NewConfig);
                    _logger.LogDebug("✅ 成功添加路由: {RouteName}", change.RouteName);
                }
                catch (Exception ex)
                {
                    var error = $"添加路由 {change.RouteName} 失败: {ex.Message}";
                    result.Errors.Add(error);
                    _logger.LogError(ex, "添加路由失败: {RouteName}", change.RouteName);
                }
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _updateWriter?.Complete();
            
            try
            {
                _processingTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "等待处理任务完成时发生异常");
            }

            _cancellationTokenSource?.Dispose();
            _logger.LogInformation("热更新协调器已停止");
        }

        /// <summary>
        /// 热更新请求
        /// </summary>
        private class HotUpdateRequest
        {
            public required string Id { get; set; }
            public required List<RouteConfigChangeInfo> Changes { get; set; }
            public required TaskCompletionSource<HotUpdateResult> CompletionSource { get; set; }
            public DateTime SubmittedAt { get; set; }
        }
    }
}
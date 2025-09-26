using Domain.Routes.Configuration;
using Infrastructure.Endpoints;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 热更新协调器接口
    /// 协调整个热更新过程，确保原子性和一致性
    /// </summary>
    public interface IHotUpdateCoordinator
    {
        /// <summary>
        /// 应用路由配置变更
        /// </summary>
        /// <param name="changes">变更列表</param>
        /// <returns>更新结果</returns>
        Task<HotUpdateResult> ApplyChangesAsync(List<RouteConfigChangeInfo> changes);

        /// <summary>
        /// 强制重新加载所有路由
        /// </summary>
        /// <returns>更新结果</returns>
        Task<HotUpdateResult> ForceReloadAllAsync();

        /// <summary>
        /// 创建当前路由状态快照
        /// </summary>
        /// <returns>路由快照</returns>
        RouteSnapshot CreateSnapshot();

        /// <summary>
        /// 从快照恢复路由状态
        /// </summary>
        /// <param name="snapshot">要恢复的快照</param>
        /// <returns>恢复结果</returns>
        Task<HotUpdateResult> RestoreSnapshotAsync(RouteSnapshot snapshot);
    }

    /// <summary>
    /// 热更新结果
    /// </summary>
    public class HotUpdateResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = [];
        public List<string> Warnings { get; set; } = [];
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan Duration { get; set; }
        public int AffectedRoutesCount { get; set; }
    }

    /// <summary>
    /// 路由状态快照
    /// </summary>
    public class RouteSnapshot
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<RouteConfigItem> Routes { get; set; } = [];
        public Dictionary<string, object> Metadata { get; set; } = [];
        public string Version { get; set; } = Guid.NewGuid().ToString("N")[..8];
    }
}
using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;
using Shared;

namespace Infrastructure.Services
{
    /// <summary>
    /// 基于内存的健康指标存储实现（同步版本）
    /// </summary>
    public class MemoryHealthMetricsStorage : IHealthMetricsStorage, IDisposable
    {
        private readonly Dictionary<string, CircularHealthBuffer> _buffers;
        private readonly int _bufferCapacity;
        private readonly ReaderWriterLockSlim _rwLock = new();

        public MemoryHealthMetricsStorage(IEnumerable<string> destinationCodes, int bufferCapacity = 100)
        {
            ArgumentNullException.ThrowIfNull(destinationCodes);

            if (bufferCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferCapacity), "Buffer capacity must be greater than 0");

            _bufferCapacity = bufferCapacity;
            _buffers = new Dictionary<string, CircularHealthBuffer>(destinationCodes.Count());

            foreach (var code in destinationCodes)
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    _buffers[code] = new CircularHealthBuffer(bufferCapacity);
                }
            }
        }

        /// <summary>
        /// 存储健康检查指标
        /// </summary>
        public void StoreMetrics(string destinationCode, CompactHealthMetrics metrics)
        {
            if (string.IsNullOrWhiteSpace(destinationCode))
                return;

            using (_rwLock.EnterRead())
            {
                if (_buffers.TryGetValue(destinationCode, out var buffer))
                {
                    buffer.Add(metrics);
                }
            }
        }

        /// <summary>
        /// 获取指定目标的最近N条记录
        /// </summary>
        public ReadOnlySpan<CompactHealthMetrics> GetRecentMetrics(string destinationCode, int count)
        {
            if (string.IsNullOrWhiteSpace(destinationCode) || count <= 0)
                return Array.Empty<CompactHealthMetrics>();

            using (_rwLock.EnterRead())
            {
                return _buffers.TryGetValue(destinationCode, out var buffer)
                    ? buffer.GetRecent(count)
                    : Array.Empty<CompactHealthMetrics>();
            }
        }

        /// <summary>
        /// 检查是否有足够的数据
        /// </summary>
        public bool HasSufficientData(string destinationCode, int requiredDataPoints)
        {
            if (string.IsNullOrWhiteSpace(destinationCode) || requiredDataPoints <= 0)
                return false;

            using (_rwLock.EnterRead())
            {
                return _buffers.TryGetValue(destinationCode, out var buffer)
                    && buffer.Count >= requiredDataPoints;
            }
        }

        /// <summary>
        /// 获取指定目标的统计信息
        /// </summary>
        public HealthBufferStats GetStats(string destinationCode)
        {
            if (string.IsNullOrWhiteSpace(destinationCode))
                return HealthBufferStats.Empty;

            using (_rwLock.EnterRead())
            {
                return _buffers.TryGetValue(destinationCode, out var buffer)
                    ? buffer.GetStats()
                    : HealthBufferStats.Empty;
            }
        }

        public void AddDestination(string destinationCode)
        {
            if (string.IsNullOrWhiteSpace(destinationCode))
                return;

            using (_rwLock.EnterWrite())
            {
                if (!_buffers.ContainsKey(destinationCode))
                {
                    _buffers[destinationCode] = new CircularHealthBuffer(_bufferCapacity);
                }
            }
        }

        /// <summary>
        /// 移除destination
        /// </summary>
        public void RemoveDestination(string destinationCode)
        {
            if (string.IsNullOrWhiteSpace(destinationCode))
                return;

            using (_rwLock.EnterWrite())
            {
                _buffers.Remove(destinationCode);
            }
        }

        /// <summary>
        /// 批量更新destinations
        /// </summary>
        public void UpdateDestinations(IEnumerable<string> newDestinationCodes)
        {
            ArgumentNullException.ThrowIfNull(newDestinationCodes);

            var newCodes = new HashSet<string>(newDestinationCodes.Where(c => !string.IsNullOrWhiteSpace(c)));

            using (_rwLock.EnterWrite())
            {
                var toRemove = _buffers.Keys.Where(k => !newCodes.Contains(k)).ToArray();
                foreach (var code in toRemove)
                {
                    _buffers.Remove(code);
                }

                foreach (var code in newCodes)
                {
                    if (!_buffers.ContainsKey(code))
                    {
                        _buffers[code] = new CircularHealthBuffer(_bufferCapacity);
                    }
                }
            }
        }

        /// <summary>
        /// 获取所有destination的统计信息
        /// </summary>
        public Dictionary<string, HealthBufferStats> GetAllStats()
        {
            using (_rwLock.EnterRead())
            {
                var result = new Dictionary<string, HealthBufferStats>();
                foreach (var kvp in _buffers)
                {
                    result[kvp.Key] = kvp.Value.GetStats();
                }
                return result;
            }
        }

        /// <summary>
        /// 获取内存使用统计
        /// </summary>
        public MemoryUsageStats GetMemoryUsage()
        {
            using (_rwLock.EnterRead())
            {
                var totalBuffers = _buffers.Count;
                var totalRecords = _buffers.Values.Sum(b => b.Count);
                var totalCapacity = _buffers.Values.Sum(b => b.Capacity);

                var estimatedMemoryBytes = totalRecords * 13L + totalBuffers * 64L;

                return new MemoryUsageStats
                {
                    TotalDestinations = totalBuffers,
                    TotalRecords = totalRecords,
                    TotalCapacity = totalCapacity,
                    MemoryUsageBytes = estimatedMemoryBytes,
                    AverageRecordsPerDestination = totalBuffers > 0 ? (double)totalRecords / totalBuffers : 0
                };
            }
        }

        /// <summary>
        /// 获取所有destination代码
        /// </summary>
        public string[] GetAllDestinationCodes()
        {
            using (_rwLock.EnterRead())
            {
                return _buffers.Keys.ToArray();
            }
        }

        /// <summary>
        /// 清理所有数据
        /// </summary>
        public void ClearAll()
        {
            using (_rwLock.EnterWrite())
            {
                _buffers.Clear();
            }
        }

        public void Dispose()
        {
            _rwLock?.Dispose();
        }
    }

    /// <summary>
    /// 内存使用统计
    /// </summary>
    public struct MemoryUsageStats
    {
        public int TotalDestinations { get; set; }
        public int TotalRecords { get; set; }
        public int TotalCapacity { get; set; }
        public long MemoryUsageBytes { get; set; }
        public double AverageRecordsPerDestination { get; set; }

        public readonly string GetDescription()
        {
            var memoryMB = MemoryUsageBytes / (1024.0 * 1024.0);
            return $"内存使用: {memoryMB:F2}MB, 目标数: {TotalDestinations}, 记录数: {TotalRecords}, 平均每目标: {AverageRecordsPerDestination:F1}条";
        }

        public readonly double GetMemoryUsageMB()
        {
            return MemoryUsageBytes / (1024.0 * 1024.0);
        }
    }
}
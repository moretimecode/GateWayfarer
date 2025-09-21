namespace Domain.Services.HealthChecking
{
    /// <summary>
    /// 循环健康检查缓冲区
    /// </summary>
    public class CircularHealthBuffer
    {
        private readonly CompactHealthMetrics[] _buffer;
        private int _currentIndex;
        private int _count;
        private readonly int _capacity;
        private readonly object _lock = new object();
        public CircularHealthBuffer(int capacity = 100)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than 0");

            _capacity = capacity;
            _buffer = new CompactHealthMetrics[capacity];
            _currentIndex = 0;
            _count = 0;
        }

        /// <summary>
        /// 当前存储的记录数
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        /// <summary>
        /// 缓冲区容量
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// 添加新的健康检查记录
        /// </summary>
        public void Add(CompactHealthMetrics metrics)
        {
            lock (_lock)
            {
                _buffer[_currentIndex] = metrics;
                _currentIndex = (_currentIndex + 1) % _capacity;

                if (_count < _capacity)
                {
                    _count++;
                }
            }
        }

        /// <summary>
        /// 获取最近的N条记录（按时间倒序）
        /// </summary>
        public ReadOnlySpan<CompactHealthMetrics>GetRecent(int count)
        {
           if (count <= 0)
                return ReadOnlySpan<CompactHealthMetrics>.Empty;

            lock (_lock)
            {
                if (_count == 0)
                    return ReadOnlySpan<CompactHealthMetrics>.Empty;

                var actualCount = Math.Min(count, _count);
                var start = (_currentIndex - actualCount + _capacity) % _capacity;

                if (start + actualCount <= _capacity)
                {
                    // 连续块，直接返回Span
                    return new ReadOnlySpan<CompactHealthMetrics>(_buffer, start, actualCount);
                }
                else
                {
                    // 跨界情况，fallback到数组复制（为了简单；未来可返回自定义序列）
                    var result = new CompactHealthMetrics[actualCount];
                    for (int i = 0; i < actualCount; i++)
                    {
                        var index = (start + i) % _capacity;
                        result[i] = _buffer[index];
                    }
                    return new ReadOnlySpan<CompactHealthMetrics>(result);
                }
            }
        }


        /// <summary>
        /// 🎯 扩展点：按时间范围查询（为未来的时间窗口策略预留）
        /// </summary>
        public CompactHealthMetrics[] GetByTimeRange(DateTimeOffset from, DateTimeOffset to)
        {
            var fromMs = from.ToUnixTimeMilliseconds();
            var toMs = to.ToUnixTimeMilliseconds();

            lock (_lock)
            {
                var result = new List<CompactHealthMetrics>();

                for (int i = 0; i < _count; i++)
                {
                    var index = (_currentIndex - 1 - i + _capacity) % _capacity;
                    var metrics = _buffer[index];

                    if (metrics.TimestampMs >= fromMs && metrics.TimestampMs <= toMs)
                    {
                        result.Add(metrics);
                    }
                    else if (metrics.TimestampMs < fromMs)
                    {
                        break; 
                    }
                }

                return result.ToArray();
            }
        }

        /// <summary>
        /// 按条件筛选（为复杂策略预留）
        /// </summary>
        public CompactHealthMetrics[] GetByPredicate(Func<CompactHealthMetrics, bool> predicate, int maxCount = int.MaxValue)
        {
            lock (_lock)
            {
                var result = new List<CompactHealthMetrics>();
                var checked_count = 0;

                for (int i = 0; i < _count && checked_count < maxCount; i++)
                {
                    var index = (_currentIndex - 1 - i + _capacity) % _capacity;
                    var metrics = _buffer[index];

                    if (predicate(metrics))
                    {
                        result.Add(metrics);
                        checked_count++;
                    }

                }

                return result.ToArray();
            }
        }

        /// <summary>
        ///  获取统计信息（为复杂策略预留）
        /// </summary>
        public HealthBufferStats GetStats()
        {
            lock (_lock)
            {
                if (_count == 0)
                {
                    return HealthBufferStats.Empty;
                }

                var metrics = GetRecent(_count);
                return HealthBufferStats.CreateFromMetrics(metrics);
            }
        }

        private CompactHealthMetrics? GetLatest()
        {
            if (_count == 0) return null;
            var latestIndex = (_currentIndex - 1 + _capacity) % _capacity;
            return _buffer[latestIndex];
        }
    }
}
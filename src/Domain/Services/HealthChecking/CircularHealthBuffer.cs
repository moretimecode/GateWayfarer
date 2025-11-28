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
        private readonly System.Threading.Lock _lock = new();

        public CircularHealthBuffer(int capacity = 100)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than 0");

            _capacity = capacity;
            _buffer = new CompactHealthMetrics[capacity];
            _currentIndex = capacity - 1; // 🎯 从末尾开始
            _count = 0;
        }

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

        public int Capacity => _capacity;

        /// <summary>
        /// 添加新的健康检查记录（从后往前填充）
        /// </summary>
        public void Add(CompactHealthMetrics metrics)
        {
            lock (_lock)
            {
                if (_count > 0)
                {
                    var latestIndex = (_currentIndex + 1) % _capacity;
                    var latestTimestamp = _buffer[latestIndex].TimestampMs;

                    if (metrics.TimestampMs < latestTimestamp)
                    {
                        return;
                    }
                }

                _buffer[_currentIndex] = metrics;
                _currentIndex = (_currentIndex - 1 + _capacity) % _capacity;

                if (_count < _capacity)
                {
                    _count++;
                }
            }
        }

        /// <summary>
        /// 获取最近的N条记录（按时间倒序，最新的在前）
        /// 
        /// 性能说明：
        /// - 如果数据连续（未跨越数组边界），返回零拷贝的 ReadOnlySpan
        /// - 如果数据跨界（环绕），会复制到新数组
        /// </summary>
        public ReadOnlySpan<CompactHealthMetrics> GetRecent(int count)
        {
            if (count <= 0)
                return ReadOnlySpan<CompactHealthMetrics>.Empty;

            lock (_lock)
            {
                if (_count == 0)
                    return ReadOnlySpan<CompactHealthMetrics>.Empty;

                var actualCount = Math.Min(count, _count);

                // 最新的数据在 currentIndex + 1 位置
                var start = (_currentIndex + 1) % _capacity;

                if (start + actualCount <= _capacity)
                {
                    // 连续块，零拷贝返回 Span（最新到最旧）
                    return new ReadOnlySpan<CompactHealthMetrics>(_buffer, start, actualCount);
                }
                else
                {
                    // 跨界情况（从 start 到末尾 + 从0开始的部分）
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

        public CompactHealthMetrics[] GetByTimeRange(DateTimeOffset from, DateTimeOffset to)
        {
            var fromMs = from.ToUnixTimeMilliseconds();
            var toMs = to.ToUnixTimeMilliseconds();

            lock (_lock)
            {
                var result = new List<CompactHealthMetrics>();

                // 🎯 从最新（currentIndex+1）开始遍历
                for (int i = 0; i < _count; i++)
                {
                    var index = (_currentIndex + 1 + i) % _capacity;
                    var metrics = _buffer[index];

                    // 因为是从新到旧，如果已经太旧了就停止
                    if (metrics.TimestampMs < fromMs)
                    {
                        break;
                    }

                    if (metrics.TimestampMs >= fromMs && metrics.TimestampMs <= toMs)
                    {
                        result.Add(metrics);
                    }
                }

                return result.ToArray();
            }
        }

        public CompactHealthMetrics[] GetByPredicate(Func<CompactHealthMetrics, bool> predicate, int maxCount = int.MaxValue)
        {
            lock (_lock)
            {
                var result = new List<CompactHealthMetrics>();

                // 🎯 从最新（currentIndex+1）开始遍历
                for (int i = 0; i < _count && result.Count < maxCount; i++)
                {
                    var index = (_currentIndex + 1 + i) % _capacity;
                    var metrics = _buffer[index];

                    if (predicate(metrics))
                    {
                        result.Add(metrics);
                    }
                }

                return result.ToArray();
            }
        }

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
            lock (_lock)
            {
                if (_count == 0) return null;
                var latestIndex = (_currentIndex + 1) % _capacity;
                return _buffer[latestIndex];
            }
        }
    }
}
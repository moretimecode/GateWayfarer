namespace Infrastructure.Protocol.Streaming;

/// <summary>
/// 流式转发并发限流器实现
/// 使用 SemaphoreSlim 实现并发控制
/// </summary>
public class StreamingConcurrencyLimiter : IStreamingConcurrencyLimiter
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxCount;
    private bool _disposed;

    public StreamingConcurrencyLimiter(int maxConcurrentStreams)
    {
        if (maxConcurrentStreams <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentStreams), "最大并发数必须大于0");

        _maxCount = maxConcurrentStreams;
        _semaphore = new SemaphoreSlim(maxConcurrentStreams, maxConcurrentStreams);
    }

    /// <summary>
    /// 当前并发数
    /// </summary>
    public int CurrentCount => _maxCount - _semaphore.CurrentCount;

    /// <summary>
    /// 最大并发数
    /// </summary>
    public int MaxCount => _maxCount;

    /// <summary>
    /// 尝试获取流式转发许可
    /// </summary>
    public async Task<bool> TryAcquireAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(StreamingConcurrencyLimiter));

        return await _semaphore.WaitAsync(timeout, cancellationToken);
    }

    /// <summary>
    /// 释放流式转发许可
    /// </summary>
    public void Release()
    {
        if (_disposed)
            return;

        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // 已经释放过了，忽略
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _semaphore?.Dispose();
        _disposed = true;
    }
}
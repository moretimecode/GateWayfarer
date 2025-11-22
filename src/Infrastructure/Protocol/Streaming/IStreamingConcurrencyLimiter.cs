namespace Infrastructure.Protocol.Streaming;

/// <summary>
/// 流式转发并发限流器接口
/// 防止同时进行过多的流式转发导致资源耗尽
/// </summary>
public interface IStreamingConcurrencyLimiter : IDisposable
{
    /// <summary>
    /// 尝试获取流式转发许可
    /// </summary>
    /// <param name="timeout">等待超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功获取许可</returns>
    Task<bool> TryAcquireAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 释放流式转发许可
    /// </summary>
    void Release();

    /// <summary>
    /// 当前并发数
    /// </summary>
    int CurrentCount { get; }

    /// <summary>
    /// 最大并发数
    /// </summary>
    int MaxCount { get; }
}
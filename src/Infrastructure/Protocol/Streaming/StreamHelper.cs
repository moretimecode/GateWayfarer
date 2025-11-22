namespace Infrastructure.Protocol.Streaming;

/// <summary>
/// Stream 复制静态辅助类
/// 提供流式复制的通用方法
/// </summary>
public static class StreamHelper
{
    /// <summary>
    /// 异步复制Stream
    /// </summary>
    /// <param name="source">源Stream</param>
    /// <param name="destination">目标Stream</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>复制的字节数</returns>
    public static async Task<long> CopyStreamAsync(
        Stream source,
        Stream destination,
        int bufferSize = 81920,
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[bufferSize];
        long totalBytes = 0;
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytes += bytesRead;
        }

        return totalBytes;
    }

    /// <summary>
    /// 带超时的Stream复制
    /// </summary>
    public static async Task<long> CopyStreamWithTimeoutAsync(
        Stream source,
        Stream destination,
        TimeSpan timeout,
        int bufferSize = 81920,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await CopyStreamAsync(source, destination, bufferSize, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Stream复制超时: {timeout.TotalSeconds}秒");
        }
    }

    /// <summary>
    /// 带Content-Length验证的Stream复制
    /// </summary>
    public static async Task<long> CopyStreamWithValidationAsync(
        Stream source,
        Stream destination,
        long? expectedLength,
        int bufferSize = 81920,
        CancellationToken cancellationToken = default)
    {
        var totalBytes = await CopyStreamAsync(source, destination, bufferSize, cancellationToken);

        if (expectedLength.HasValue && totalBytes != expectedLength.Value)
        {
            throw new InvalidOperationException(
                $"Content-Length验证失败: 期望 {expectedLength.Value} 字节，实际 {totalBytes} 字节");
        }

        return totalBytes;
    }
}
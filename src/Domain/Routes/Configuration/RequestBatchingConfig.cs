namespace Domain.Routes.Configuration
{
    public enum BatchMergeStrategy
    {
        /// <summary>
        /// 不合并，每个请求独立处理
        /// </summary>
        None,
        /// <summary>
        /// 基于查询参数合并
        /// </summary>
        QueryParameters,
        /// <summary>
        /// 基于请求体内容合并
        /// </summary>
        RequestBody,
        /// <summary>
        /// 基于自定义表达式合并
        /// </summary>
        CustomExpression
    }

    public class RequestBatchingConfig : ValueObject
    {
        // 是否启用批处理
        public bool EnableBatching { get; private set; }

        // 批次时间窗口（如100ms内的请求合并）
        public TimeSpan BatchWindow { get; private set; }

        // 批次最大请求数（如最多10个请求一批）
        public int MaxBatchSize { get; private set; }

        // 合并策略：按查询参数/请求体/自定义表达式
        public BatchMergeStrategy MergeStrategy { get; private set; }

        // 自定义合并键表达式（如"userId={userId}"）
        public string? BatchKeyExpression { get; private set; }

        // 最大等待时间
        public TimeSpan MaxWaitTime { get; private set; }

        private RequestBatchingConfig(bool enableBatching, TimeSpan batchWindow,
            int maxBatchSize, BatchMergeStrategy mergeStrategy,
            string? batchKeyExpression, TimeSpan maxWaitTime)
        {
            EnableBatching = enableBatching;
            BatchWindow = batchWindow;
            MaxBatchSize = maxBatchSize;
            MergeStrategy = mergeStrategy;
            BatchKeyExpression = batchKeyExpression;
            MaxWaitTime = maxWaitTime;
        }

        public static RequestBatchingConfig Disabled() =>
            new(false, TimeSpan.Zero, 0, BatchMergeStrategy.None, null, TimeSpan.Zero);

        public static RequestBatchingConfig Create(TimeSpan batchWindow, int maxBatchSize = 10,
            BatchMergeStrategy mergeStrategy = BatchMergeStrategy.QueryParameters,
            string? batchKeyExpression = null, TimeSpan? maxWaitTime = null)
        {
            if (maxBatchSize <= 0)
                throw new ArgumentException("Max batch size must be greater than 0", nameof(maxBatchSize));

            if (batchWindow <= TimeSpan.Zero)
                throw new ArgumentException("Batch window must be greater than 0", nameof(batchWindow));

            return new RequestBatchingConfig(
                true,
                batchWindow,
                maxBatchSize,
                mergeStrategy,
                batchKeyExpression,
                maxWaitTime ?? TimeSpan.FromSeconds(5));
        }

        public IEnumerable<object> GetEqualityComponents()
        {
            yield return EnableBatching;
            yield return BatchWindow;
            yield return MaxBatchSize;
            yield return MergeStrategy;
            yield return BatchKeyExpression ?? string.Empty;
            yield return MaxWaitTime;
        }
    }
}
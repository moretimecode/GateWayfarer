using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Domain.Services.Configuration
{
    public class RetryPolicy : ValueObject
    {
        // 最大重试次数（默认3次）
        public int MaxRetryAttempts { get; private set; }

         // 基础延迟时间（默认100ms）
        public TimeSpan BaseDelay { get; private set; }

        // 最大延迟时间（默认30秒）
        public TimeSpan MaxDelay { get; private set; }

         /// <summary>
         /// 退避策略：固定/线性/指数/指数+抖动
         /// 固定: 100ms, 100ms, 100ms
         /// 指数: 100ms, 200ms, 400ms
         /// 指数+抖动: 100ms±随机, 200ms±随机, 400ms±随机
         /// </summary>
         /// <value></value>
        public RetryBackoffType BackoffType { get; private set; }

        // 哪些HTTP状态码需要重试（500,502,503,504）
        public List<int> RetriableHttpStatusCodes { get; private set; }

          // 哪些异常类型需要重试
        public List<Type> RetriableExceptionTypes { get; private set; }
        public bool Enabled { get; private set; }

        private RetryPolicy(int maxRetryAttempts, TimeSpan baseDelay, TimeSpan maxDelay,
            RetryBackoffType backoffType, List<int> retriableHttpStatusCodes,
            List<Type> retriableExceptionTypes, bool enabled)
        {
            MaxRetryAttempts = maxRetryAttempts;
            BaseDelay = baseDelay;
            MaxDelay = maxDelay;
            BackoffType = backoffType;
            RetriableHttpStatusCodes = retriableHttpStatusCodes ?? new List<int>();
            RetriableExceptionTypes = retriableExceptionTypes ?? new List<Type>();
            Enabled = enabled;
        }

        public static RetryPolicy Create(int maxRetryAttempts = 3,
            TimeSpan? baseDelay = null, TimeSpan? maxDelay = null,
            RetryBackoffType backoffType = RetryBackoffType.ExponentialWithJitter,
            List<int>? retriableHttpStatusCodes = null, bool enabled = true)
        {
            if (maxRetryAttempts < 0)
                throw new ArgumentException("Max retry attempts cannot be negative", nameof(maxRetryAttempts));

            var defaultRetriableStatusCodes = retriableHttpStatusCodes ?? new List<int>
            {
                500, // Internal Server Error
                502, // Bad Gateway
                503, // Service Unavailable
                504  // Gateway Timeout
            };

            var defaultRetriableExceptions = new List<Type>
            {
                typeof(HttpRequestException),
                typeof(TaskCanceledException),
                typeof(SocketException)
            };

            return new RetryPolicy(
                maxRetryAttempts,
                baseDelay ?? TimeSpan.FromMilliseconds(100),
                maxDelay ?? TimeSpan.FromSeconds(30),
                backoffType,
                defaultRetriableStatusCodes,
                defaultRetriableExceptions,
                enabled);
        }

        public static RetryPolicy Disabled() => Create(0, enabled: false);
        public static RetryPolicy Default() => Create();

        public IEnumerable<object> GetEqualityComponents()
        {
            yield return MaxRetryAttempts;
            yield return BaseDelay;
            yield return MaxDelay;
            yield return BackoffType;
            yield return Enabled;
            foreach (var statusCode in RetriableHttpStatusCodes)
                yield return statusCode;
        }
    }
}
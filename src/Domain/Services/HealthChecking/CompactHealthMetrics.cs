using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Domain.Services.HealthChecking
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CompactHealthMetrics : IEquatable<CompactHealthMetrics>, IComparable<CompactHealthMetrics>
    {
        /// <summary>
        /// 检查时间戳（Unix毫秒）
        /// </summary>
        public long TimestampMs { get; set; }

        /// <summary>
        /// 响应时间（毫秒），最大65535ms
        /// </summary>
        public ushort ResponseTimeMs { get; set; }

        /// <summary>
        /// HTTP状态码或自定义状态码
        /// </summary>
        public ushort StatusCode { get; set; }

        /// <summary>
        /// 健康检查标志位
        /// </summary>
        public HealthCheckFlags Flags { get; set; }


        #region 基础属性访问器

        /// <summary>
        /// 检查是否成功
        /// </summary>
        public readonly bool IsSuccessful => Flags.HasFlag(HealthCheckFlags.IsSuccessful);

        /// <summary>
        /// 是否超时
        /// </summary>
        public readonly bool IsTimeout => Flags.HasFlag(HealthCheckFlags.IsTimeout);

        /// <summary>
        /// 是否网络错误
        /// </summary>
        public readonly bool IsNetworkError => Flags.HasFlag(HealthCheckFlags.IsNetworkError);

        /// <summary>
        /// 是否服务器错误
        /// </summary>
        public readonly bool IsServerError => Flags.HasFlag(HealthCheckFlags.IsServerError);

        /// <summary>
        /// 是否客户端错误
        /// </summary>
        public readonly bool IsClientError => Flags.HasFlag(HealthCheckFlags.IsClientError);

        /// <summary>
        /// 获取检查时间
        /// </summary>
        public readonly DateTimeOffset GetCheckTime() => DateTimeOffset.FromUnixTimeMilliseconds(TimestampMs);

        /// <summary>
        /// 获取响应时间
        /// </summary>
        public readonly TimeSpan GetResponseTime() => TimeSpan.FromMilliseconds(ResponseTimeMs);

        #endregion

        #region 扩展性支持方法

        public readonly bool HasFlag(HealthCheckFlags flag) => Flags.HasFlag(flag);

        public readonly string GetErrorType()
        {
            return StatusCode switch
            {
                >= 60000 => "Custom Internal Error",  // 自定义状态码范围
                >= 500 => "Server Error",
                408 => "Timeout",
                >= 400 => "Client Error", 
                0 => "Network Error",
                _ => IsSuccessful ? "Success" : "Unknown Error"
            };
        }

        public readonly int CalculateHealthScore()
        {
            if (IsSuccessful) return 100;
            
            var score = 0;
            
            if (IsTimeout) score = 20;           // 超时还有机会恢复
            else if (IsClientError) score = 40;  // 客户端错误可能是配置问题
            else if (IsServerError) score = 10;  // 服务器错误比较严重
            else if (IsNetworkError) score = 5;  // 网络错误最严重
            
            if (ResponseTimeMs > 5000) score = Math.Max(0, score - 20);  // 响应太慢扣分
            
            return score;
        }

        #endregion

        #region 工厂方法 - 支持不同创建方式

        /// <summary>
        /// 🎯 创建成功的健康指标
        /// </summary>
        public static CompactHealthMetrics CreateSuccess(long timestampMs, ushort responseTimeMs, ushort statusCode)
        {
            return new CompactHealthMetrics
            {
                TimestampMs = timestampMs,
                ResponseTimeMs = responseTimeMs,
                StatusCode = statusCode,
                Flags = HealthCheckFlags.IsSuccessful
            };
        }

        /// <summary>
        /// 🎯 创建成功的健康指标（使用当前时间）
        /// </summary>
        public static CompactHealthMetrics CreateSuccess(ushort responseTimeMs, ushort statusCode)
        {
            return CreateSuccess(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), responseTimeMs, statusCode);
        }

        /// <summary>
        /// 🎯 创建失败的健康指标
        /// </summary>
        public static CompactHealthMetrics CreateFailure(long timestampMs, ushort responseTimeMs, ushort statusCode, HealthCheckFlags additionalFlags = HealthCheckFlags.None)
        {
            return new CompactHealthMetrics
            {
                TimestampMs = timestampMs,
                ResponseTimeMs = responseTimeMs,
                StatusCode = statusCode,
                Flags = additionalFlags 
            };
        }

        /// <summary>
        /// 🎯 根据HTTP响应创建指标（最常用的工厂方法）
        /// </summary>
        public static CompactHealthMetrics CreateFromHttpResponse(int statusCode, TimeSpan responseTime, Exception? exception = null)
        {
            var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var responseTimeMs = (ushort)Math.Min(responseTime.TotalMilliseconds, ushort.MaxValue);
            var statusCodeUshort = (ushort)Math.Max(0, Math.Min(statusCode, ushort.MaxValue));

            var flags = HealthCheckFlags.None;

            if (statusCode >= 200 && statusCode <= 299)
            {
                flags |= HealthCheckFlags.IsSuccessful;
            }
            else if (statusCode >= 400 && statusCode <= 499)
            {
                flags |= HealthCheckFlags.IsClientError;
            }
            else if (statusCode >= 500 && statusCode <= 599)
            {
                flags |= HealthCheckFlags.IsServerError;
            }

            if (exception != null)
            {
                if (exception is TaskCanceledException || exception is TimeoutException)
                {
                    flags |= HealthCheckFlags.IsTimeout;
                    statusCodeUshort = 408;
                }
                else if (exception is HttpRequestException)
                {
                    flags |= HealthCheckFlags.IsNetworkError;
                    statusCodeUshort = 0;
                }
            }

            return new CompactHealthMetrics
            {
                TimestampMs = timestampMs,
                ResponseTimeMs = responseTimeMs,
                StatusCode = statusCodeUshort,
                Flags = flags
            };
        }

        /// <summary>
        /// 🎯 扩展点：创建自定义状态的指标
        /// </summary>
        public static CompactHealthMetrics CreateCustom(ushort customStatusCode, ushort responseTimeMs, HealthCheckFlags customFlags)
        {
            return new CompactHealthMetrics
            {
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ResponseTimeMs = responseTimeMs,
                StatusCode = customStatusCode,
                Flags = customFlags
            };
        }

        #endregion

        #region 标准接口实现

        public readonly bool Equals(CompactHealthMetrics other)
        {
            return TimestampMs == other.TimestampMs &&
                   ResponseTimeMs == other.ResponseTimeMs &&
                   StatusCode == other.StatusCode &&
                   Flags == other.Flags;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is CompactHealthMetrics other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(TimestampMs, ResponseTimeMs, StatusCode, Flags);
        }

        public readonly int CompareTo(CompactHealthMetrics other)
        {
            return TimestampMs.CompareTo(other.TimestampMs);
        }

        public static bool operator ==(CompactHealthMetrics left, CompactHealthMetrics right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CompactHealthMetrics left, CompactHealthMetrics right)
        {
            return !left.Equals(right);
        }

        public override readonly string ToString()
        {
            return $"[{GetCheckTime():HH:mm:ss}] {StatusCode} in {ResponseTimeMs}ms - {GetErrorType()}";
        }

        #endregion
    }
}
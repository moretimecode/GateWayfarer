using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Services.Configuration
{
    public class TimeoutConfig : ValueObject
    {
        public TimeSpan RequestTimeout { get; private set; }
        public TimeSpan ConnectionTimeout { get; private set; }
        public TimeSpan KeepAliveTimeout { get; private set; }
        public bool Enabled { get; private set; }

        private TimeoutConfig(TimeSpan requestTimeout, TimeSpan connectionTimeout,
            TimeSpan keepAliveTimeout, bool enabled)
        {
            RequestTimeout = requestTimeout;
            ConnectionTimeout = connectionTimeout;
            KeepAliveTimeout = keepAliveTimeout;
            Enabled = enabled;
        }

        public static TimeoutConfig Create(TimeSpan? requestTimeout = null,
            TimeSpan? connectionTimeout = null, TimeSpan? keepAliveTimeout = null, bool enabled = true)
        {
            return new TimeoutConfig(
                requestTimeout ?? TimeSpan.FromSeconds(30),
                connectionTimeout ?? TimeSpan.FromSeconds(10),
                keepAliveTimeout ?? TimeSpan.FromMinutes(2),
                enabled);
        }

        public static TimeoutConfig Default() => Create();

        public IEnumerable<object> GetEqualityComponents()
        {
            yield return RequestTimeout;
            yield return ConnectionTimeout;
            yield return KeepAliveTimeout;
            yield return Enabled;
        }
    }
}
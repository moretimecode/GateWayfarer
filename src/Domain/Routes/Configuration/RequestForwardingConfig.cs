namespace Domain.Routes.Configuration
{
    public class RequestForwardingConfig : ValueObject
    {
        // 是否保留原始Host头（默认false，使用目标服务Host）
        public bool PreserveHostHeader { get; private set; }

        // 自定义添加的请求头
        public Dictionary<string, string> CustomHeaders { get; private set; }

        // 需要移除的请求头
        public List<string> RemoveHeaders { get; private set; }

        // 是否启用流式转发（大文件优化）
        public bool EnableStreamingForwarding { get; private set; }

        // 缓冲区大小（64KB）
        public int BufferSize { get; private set; }

        // 是否保留客户端IP信息
        public bool PreserveClientIp { get; private set; }

        private RequestForwardingConfig(bool preserveHostHeader,
            Dictionary<string, string> customHeaders, List<string> removeHeaders,
            bool enableStreamingForwarding, int bufferSize, bool preserveClientIp)
        {
            PreserveHostHeader = preserveHostHeader;
            CustomHeaders = customHeaders ?? new Dictionary<string, string>();
            RemoveHeaders = removeHeaders ?? new List<string>();
            EnableStreamingForwarding = enableStreamingForwarding;
            BufferSize = bufferSize;
            PreserveClientIp = preserveClientIp;
        }

        public static RequestForwardingConfig Create(bool preserveHostHeader = false,
            Dictionary<string, string>? customHeaders = null, List<string>? removeHeaders = null,
            bool enableStreamingForwarding = true, int bufferSize = 65536, bool preserveClientIp = true)
        {
            if (bufferSize <= 0)
                throw new ArgumentException("Buffer size must be greater than 0", nameof(bufferSize));

            var defaultRemoveHeaders = removeHeaders ?? new List<string>
            {
                "Connection", "Transfer-Encoding", "Upgrade"
            };

            return new RequestForwardingConfig(
                preserveHostHeader,
                customHeaders ?? new Dictionary<string, string>(),
                defaultRemoveHeaders,
                enableStreamingForwarding,
                bufferSize,
                preserveClientIp);
        }

        public static RequestForwardingConfig Default() => Create();

        public IEnumerable<object> GetEqualityComponents()
        {
            yield return PreserveHostHeader;
            yield return EnableStreamingForwarding;
            yield return BufferSize;
            yield return PreserveClientIp;

            foreach (var header in CustomHeaders.OrderBy(x => x.Key))
            {
                yield return header.Key;
                yield return header.Value;
            }

            foreach (var header in RemoveHeaders.OrderBy(x => x))
                yield return header;
        }
    }
}
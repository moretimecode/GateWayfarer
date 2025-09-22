namespace Domain.Services.Configuration;

public class FallbackStrategy : ValueObject
{

    // 降级类型：无/静态响应/备用端点/缓存响应
    public FallbackType Type { get; private set; }

    // 备用端点地址
    public string? FallbackEndpoint { get; private set; }

    // 静态响应内容
    public string? StaticResponseBody { get; private set; }

    // 静态响应状态码
    public int StaticResponseStatusCode { get; private set; }

    // 静态响应头
    public Dictionary<string, string> StaticResponseHeaders { get; private set; }

    // 缓存最大存活时间（秒）
    public int CacheMaxAgeSeconds { get; private set; }
    public bool Enabled { get; private set; }

    private FallbackStrategy(FallbackType type, string? fallbackEndpoint,
        string? staticResponseBody, int staticResponseStatusCode,
        Dictionary<string, string> staticResponseHeaders, int cacheMaxAgeSeconds, bool enabled)
    {
        Type = type;
        FallbackEndpoint = fallbackEndpoint;
        StaticResponseBody = staticResponseBody;
        StaticResponseStatusCode = staticResponseStatusCode;
        StaticResponseHeaders = staticResponseHeaders ?? new Dictionary<string, string>();
        CacheMaxAgeSeconds = cacheMaxAgeSeconds;
        Enabled = enabled;
    }

    public static FallbackStrategy None() =>
        new(FallbackType.None, null, null, 200, new Dictionary<string, string>(), 0, false);

    public static FallbackStrategy StaticResponse(string responseBody,
        int statusCode = 200, Dictionary<string, string>? headers = null) =>
        new(FallbackType.StaticResponse, null, responseBody, statusCode,
            headers ?? new Dictionary<string, string>(), 0, true);

    public static FallbackStrategy AlternativeEndpoint(string fallbackEndpoint) =>
        new(FallbackType.AlternativeEndpoint, fallbackEndpoint, null, 200,
            new Dictionary<string, string>(), 0, true);

    public static FallbackStrategy CachedResponse(int maxAgeSeconds = 300) =>
        new(FallbackType.CachedResponse, null, null, 200,
            new Dictionary<string, string>(), maxAgeSeconds, true);

    public IEnumerable<object> GetEqualityComponents()
    {
        yield return Type;
        yield return FallbackEndpoint ?? string.Empty;
        yield return StaticResponseBody ?? string.Empty;
        yield return StaticResponseStatusCode;
        yield return CacheMaxAgeSeconds;
        yield return Enabled;
    }
}

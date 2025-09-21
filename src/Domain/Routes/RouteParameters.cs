namespace Domain.Routes;

public readonly struct RouteParameters
{
    private readonly Dictionary<string, string> _pathParameters;
    private readonly Dictionary<string, string> _queryParameters;

    public readonly IReadOnlyDictionary<string, string> PathParameters;
    public readonly IReadOnlyDictionary<string, string> QueryParameters;

    public RouteParameters(Dictionary<string, string>? pathParameters = null, 
                          Dictionary<string, string>? queryParameters = null)
    {
        _pathParameters = pathParameters ?? new Dictionary<string, string>();
        _queryParameters = queryParameters ?? new Dictionary<string, string>();
        
        PathParameters = _pathParameters.AsReadOnly();
        QueryParameters = _queryParameters.AsReadOnly();
    }

    public string? GetPathParameter(string key) => PathParameters.TryGetValue(key, out var value) ? value : null;
    public string? GetQueryParameter(string key) => QueryParameters.TryGetValue(key, out var value) ? value : null;
}
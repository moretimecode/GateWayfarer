using Domain.Routes;
using Domain.Routes.Services;

public class TemplateParameterExtractor : IRouteParameterExtractor
{
    public RouteMatchType MatchType => RouteMatchType.Template;

    public RouteParameters ExtractParameters(string routePath, string requestPath)
    {
        var parameters = new Dictionary<string, string>();

        var templateSegments = routePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = requestPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (templateSegments.Length != pathSegments.Length)
            return new RouteParameters(parameters);

        for (int i = 0; i < templateSegments.Length; i++)
        {
            var templateSegment = templateSegments[i];

            if (templateSegment.StartsWith("{") && templateSegment.EndsWith("}"))
            {
                var paramName = templateSegment.Trim('{', '}');
                parameters[paramName] = pathSegments[i];
            }
        }

        return new RouteParameters(parameters);
    }
}
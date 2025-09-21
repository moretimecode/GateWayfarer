using Domain.Routes;
using Domain.Routes.Contexts;
using Domain.Routes.Services;

namespace Infrastructure.Routes.ParameterExtractors
{
    public class PrefixParameterExtractor : IRouteParameterExtractor
    {
        public RouteMatchType MatchType => RouteMatchType.Prefix;

        public RouteParameters ExtractParameters(string routePath, string requestPath)
        {
            var parameters = new Dictionary<string, string>();

            var cleanRoutePath = routePath.TrimEnd('*', '/');
            
            if (requestPath.StartsWith(cleanRoutePath, StringComparison.OrdinalIgnoreCase))
            {
                var remainingPath = requestPath.Substring(cleanRoutePath.Length).TrimStart('/');
                if (!string.IsNullOrEmpty(remainingPath))
                {
                    parameters["remainingPath"] = remainingPath;
                    
                    var segments = remainingPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < segments.Length; i++)
                    {
                        parameters[$"segment{i}"] = segments[i];
                    }
                }
            }

            return new RouteParameters(parameters);
        }
    }
}
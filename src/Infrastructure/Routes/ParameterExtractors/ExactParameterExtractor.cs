using Domain.Routes;
using Domain.Routes.Contexts;
using Domain.Routes.Services;

namespace Infrastructure.Routes.ParameterExtractors
{
    public class ExactParameterExtractor : IRouteParameterExtractor
    {
        public RouteMatchType MatchType => RouteMatchType.Exact;

        public RouteParameters ExtractParameters(string routePath, string requestPath)
        {
            var parameters = new Dictionary<string, string>
            {
                ["routePath"] = routePath,
                ["requestPath"] = requestPath,
                ["isExactMatch"] = routePath.Equals(requestPath, StringComparison.OrdinalIgnoreCase).ToString()
            };

            return new RouteParameters(parameters);
        }
    }
}
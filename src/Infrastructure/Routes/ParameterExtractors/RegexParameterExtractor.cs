using Domain.Routes;
using Domain.Routes.Contexts;
using Domain.Routes.Services;
using System.Text.RegularExpressions;

namespace Infrastructure.Routes.ParameterExtractors
{
    public class RegexParameterExtractor : IRouteParameterExtractor
    {
        public RouteMatchType MatchType => RouteMatchType.Regex;

        public RouteParameters ExtractParameters(string routePath, string requestPath)
        {
            var parameters = new Dictionary<string, string>();

            try
            {
                var regex = new Regex(routePath, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                var match = regex.Match(requestPath);

                if (match.Success)
                {
                    foreach (string groupName in regex.GetGroupNames())
                    {
                        if (groupName != "0")
                        {
                            var group = match.Groups[groupName];
                            if (group.Success)
                            {
                                parameters[groupName] = group.Value;
                            }
                        }
                    }

                    for (int i = 1; i < match.Groups.Count; i++)
                    {
                        var group = match.Groups[i];
                        if (group.Success && !parameters.ContainsKey($"group{i}"))
                        {
                            parameters[$"group{i}"] = group.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                parameters["error"] = ex.Message;
            }

            return new RouteParameters(parameters);
        }
    }
}
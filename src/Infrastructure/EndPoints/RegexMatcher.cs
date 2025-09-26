using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Domain.Routes;
using Domain.Routes.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// 正则表达式匹配器
    /// 负责预编译正则表达式并进行高效匹配
    /// </summary>
    public class RegexMatcher(ILogger<RegexMatcher> logger)
    {
        private readonly ILogger<RegexMatcher> _logger = logger;
        private readonly List<CompiledRegexRoute> _compiledRoutes = new List<CompiledRegexRoute>();
        private readonly object _lockObject = new();

        /// <summary>
        /// 预编译所有正则路由
        /// </summary>
        public void PrecompileRegexRoutes(IEnumerable<RouteConfigItem> regexRoutes)
        {
            var routesToCompile = regexRoutes.Where(r => r.MatchType == RouteMatchType.Regex).ToList();
            
            _logger.LogInformation("开始预编译 {Count} 个正则路由", routesToCompile.Count);

            lock (_lockObject)
            {
                _compiledRoutes.Clear();

                foreach (var route in routesToCompile)
                {
                    try
                    {
                        var compiledRoute = CompileRegexRoute(route);
                        _compiledRoutes.Add(compiledRoute);

                        _logger.LogDebug("成功编译正则路由: {RouteName} -> {Pattern}, 参数: [{Parameters}]",
                            route.Name, route.Path, string.Join(", ", compiledRoute.ParameterNames));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "编译正则路由失败: {RouteName} -> {Pattern}",
                            route.Name, route.Path);
                        throw;
                    }
                }

                // 按优先级排序，优先级高的先匹配
                _compiledRoutes.Sort((a, b) => b.RouteConfig.Priority.CompareTo(a.RouteConfig.Priority));

                _logger.LogInformation("正则路由预编译完成，共 {Count} 个路由，按优先级排序完成", _compiledRoutes.Count);
            }
        }

        /// <summary>
        /// 尝试匹配请求路径
        /// </summary>
        public RegexMatchResult? TryMatch(string requestPath, string httpMethod)
        {
            if (string.IsNullOrEmpty(requestPath))
                return null;

            lock (_lockObject)
            {
                foreach (var compiledRoute in _compiledRoutes)
                {
                    // 1. 检查HTTP方法
                    if (!IsHttpMethodMatch(compiledRoute.RouteConfig.HttpMethods, httpMethod))
                        continue;

                    // 2. 进行正则匹配
                    var match = compiledRoute.CompiledRegex.Match(requestPath);
                    if (match.Success)
                    {
                        _logger.LogDebug("正则匹配成功: {RouteName} 匹配 {RequestPath}",
                            compiledRoute.RouteConfig.Name, requestPath);

                        // 3. 提取参数
                        var parameters = ExtractParameters(match, compiledRoute);

                        return new RegexMatchResult
                        {
                            RouteConfig = compiledRoute.RouteConfig,
                            Match = match,
                            Parameters = parameters,
                            IsSuccess = true
                        };
                    }
                }
            }

            _logger.LogDebug("没有正则路由匹配路径: {RequestPath} ({HttpMethod})", requestPath, httpMethod);
            return null;
        }

        /// <summary>
        /// 获取所有已编译的正则路由信息（用于调试）
        /// </summary>
        public IReadOnlyList<CompiledRegexRoute> GetCompiledRoutes()
        {
            lock (_lockObject)
            {
                return _compiledRoutes.ToList();
            }
        }

        /// <summary>
        /// 编译单个正则路由
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CompiledRegexRoute CompileRegexRoute(RouteConfigItem routeConfig)
        {
            var regexOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

            // 编译正则表达式
            var regex = new Regex(routeConfig.Path, regexOptions);
            
            // 分析正则表达式，提取参数名
            var parameterNames = ExtractParameterNames(routeConfig.Path, regex);

            return new CompiledRegexRoute
            {
                RouteConfig = routeConfig,
                CompiledRegex = regex,
                ParameterNames = parameterNames
            };
        }

        /// <summary>
        /// 提取正则表达式中的参数名
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<string> ExtractParameterNames(string pattern, Regex regex)
        {
            var parameterNames = new List<string>();

            // 1. 检查是否有命名组 (?<name>pattern) 或 (?'name'pattern)
            foreach (string groupName in regex.GetGroupNames())
            {
                // 跳过整个匹配组（名称为"0"）和数字组名
                if (groupName != "0" && !int.TryParse(groupName, out _))
                {
                    parameterNames.Add(groupName);
                }
            }

            // 2. 如果没有命名组，使用默认参数名
            if (parameterNames.Count == 0)
            {
                var groupCount = regex.GetGroupNumbers().Length - 1; // 减去整个匹配的组(0)
                for (int i = 1; i <= groupCount; i++)
                {
                    parameterNames.Add($"param{i}");
                }
            }

            _logger.LogDebug("从正则 '{Pattern}' 中提取参数: [{Parameters}]", 
                pattern, string.Join(", ", parameterNames));

            return parameterNames;
        }

        /// <summary>
        /// 从匹配结果中提取参数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Dictionary<string, string> ExtractParameters(Match match, CompiledRegexRoute compiledRoute)
        {
            var parameters = new Dictionary<string, string>();

            // 提取命名组
            foreach (var paramName in compiledRoute.ParameterNames)
            {
                var group = match.Groups[paramName];
                if (group.Success)
                {
                    parameters[paramName] = group.Value;
                    _logger.LogTrace("提取命名参数: {ParamName} = {Value}", paramName, group.Value);
                }
            }

            // 如果没有命名组，按位置提取
            if (parameters.Count == 0 && compiledRoute.ParameterNames.Count > 0)
            {
                for (int i = 1; i < match.Groups.Count && i <= compiledRoute.ParameterNames.Count; i++)
                {
                    if (match.Groups[i].Success)
                    {
                        var paramName = compiledRoute.ParameterNames[i - 1];
                        parameters[paramName] = match.Groups[i].Value;
                        _logger.LogTrace("提取位置参数: {ParamName} = {Value}", paramName, match.Groups[i].Value);
                    }
                }
            }

            return parameters;
        }

        /// <summary>
        /// 检查HTTP方法是否匹配
        /// </summary>
       [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsHttpMethodMatch(Domain.Routes.HttpMethod configMethods, string requestMethod)
        {
            var requestHttpMethod = Enum.TryParse<Domain.Routes.HttpMethod>(requestMethod, true, out var parsed)
                ? parsed
                : Domain.Routes.HttpMethod.GET;

            return configMethods.HasFlag(requestHttpMethod);
        }
    }

    /// <summary>
    /// 编译后的正则路由
    /// </summary>
    public class CompiledRegexRoute
    {
        public required RouteConfigItem RouteConfig { get; set; }
        public required Regex CompiledRegex { get; set; }
        public required List<string> ParameterNames { get; set; }
    }

    /// <summary>
    /// 正则匹配结果
    /// </summary>
    public class RegexMatchResult
    {
        public required RouteConfigItem RouteConfig { get; set; }
        public required Match Match { get; set; }
        public required Dictionary<string, string> Parameters { get; set; }
        public required bool IsSuccess { get; set; }
    }
}
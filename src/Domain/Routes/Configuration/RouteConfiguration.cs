using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Routes.Configuration;

public class RouteOptions
{
    public const string SectionName = "ApiGateway:Routes";
    public List<RouteConfigItem> Routes { get; set; } = new();
}

public class RouteConfigItem
{
    /// <summary>
    /// 路由名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 匹配路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 关联的服务代码
    /// </summary>
    public string ServiceCode { get; set; } = string.Empty;

    /// <summary>
    /// HTTP方法，多个用逗号分隔 (如: GET,POST,PUT)
    /// </summary>
    public HttpMethod HttpMethods { get; set; } = HttpMethod.GET;

    /// <summary>
    /// 路由优先级，数值越大优先级越高
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 匹配类型 (Prefix/Exact/Regex)
    /// </summary>
    public RouteMatchType MatchType { get; set; } = RouteMatchType.Exact;

    /// <summary>
    /// 负载均衡类型
    /// </summary>
    public LoadBalanceType LoadBalanceType { get; set; } = LoadBalanceType.RoundRobin;

    /// <summary>
    /// 授权策略名称
    /// 指定此路由使用哪个认证授权策略
    /// 例如："public", "authenticated", "admin"
    /// 默认使用 "public" （任何人都可以访问）
    /// 
    /// 参考YARP的做法，在appsettings.json中配置
    /// </summary>
    public string? AuthorizationPolicy { get; set; } = "public";

    /// <summary>
    /// 是否允许匿名访问（覆盖策略中的认证要求）
    /// </summary>
    public bool AllowAnonymous { get; set; } = false;
}


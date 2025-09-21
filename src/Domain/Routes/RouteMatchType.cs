namespace Domain.Routes;

public enum RouteMatchType
{
    /// <summary>
    /// 精确匹配：/api/users
    /// </summary>
    Exact = 0,

    /// <summary>
    /// 前缀匹配：/api/users/** 
    /// </summary>
    Prefix = 1,

    /// <summary>
    /// 正则表达式匹配
    /// </summary>
    Regex = 2,

    /// <summary>
    /// 通配符匹配：/api/*/users
    /// </summary>
    Wildcard = 3
}
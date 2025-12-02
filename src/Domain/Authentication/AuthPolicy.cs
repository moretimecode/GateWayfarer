using System.Collections.Generic;

namespace Domain.Authentication;

/// <summary>
/// 认证授权策略 - 定义哪些用户/角色能访问某个路由
/// 参考 YARP 的实现方式
/// </summary>
public class AuthPolicy
{
    /// <summary>
    /// 策略名称（唯一标识）
    /// 例如："public", "authenticated", "admin-only"
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 策略描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否要求认证（必须有有效的Token）
    /// </summary>
    public bool RequireAuthentication { get; set; } = false;

    /// <summary>
    /// 需要的角色列表（如果为空则不检查角色）
    /// 例如：["admin", "user"]
    /// 用户拥有其中任意一个角色即可通过
    /// </summary>
    public List<string> RequiredRoles { get; set; } = new();

    /// <summary>
    /// 需要的声明（Claims）列表
    /// 例如：["sub", "email", "scope:api.read"]
    /// 用户必须包含这些声明
    /// </summary>
    public List<string> RequiredClaims { get; set; } = new();

    /// <summary>
    /// 允许的范围（scopes）
    /// 例如：["api.read", "api.write"]
    /// 用户必须拥有其中至少一个scope
    /// </summary>
    public List<string> AllowedScopes { get; set; } = new();

    /// <summary>
    /// 是否公开（不需要任何检查）
    /// </summary>
    public bool IsPublic => !RequireAuthentication && RequiredRoles.Count == 0 && RequiredClaims.Count == 0;
}

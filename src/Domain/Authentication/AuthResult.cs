using System.Collections.Generic;
using System.Security.Claims;

namespace Domain.Authentication;

/// <summary>
/// 认证结果 - 表示认证操作的结果
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// 认证是否成功
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// 如果认证失败，返回失败原因
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// 认证成功后的用户身份信息
    /// </summary>
    public ClaimsPrincipal? Principal { get; set; }

    /// <summary>
    /// Token值（用于日志记录或向下游服务传递）
    /// </summary>
    public string? Token { get; set; }

    // 静态工厂方法，更便于使用

    /// <summary>
    /// 创建成功的认证结果
    /// </summary>
    public static AuthenticationResult Success(ClaimsPrincipal principal, string token)
    {
        return new AuthenticationResult
        {
            IsAuthenticated = true,
            Principal = principal,
            Token = token
        };
    }

    /// <summary>
    /// 创建失败的认证结果
    /// </summary>
    public static AuthenticationResult Failure(string reason)
    {
        return new AuthenticationResult
        {
            IsAuthenticated = false,
            FailureReason = reason
        };
    }
}

/// <summary>
/// 授权结果 - 表示授权检查的结果
/// </summary>
public class AuthorizationResult
{
    /// <summary>
    /// 授权是否通过
    /// </summary>
    public bool IsAuthorized { get; set; }

    /// <summary>
    /// 如果授权失败，返回失败原因
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// 失败的检查项（用于调试）
    /// 例如："Missing role: admin" 或 "Missing claim: email"
    /// </summary>
    public List<string> FailureDetails { get; set; } = new();

    // 静态工厂方法

    /// <summary>
    /// 创建授权成功的结果
    /// </summary>
    public static AuthorizationResult Success()
    {
        return new AuthorizationResult
        {
            IsAuthorized = true
        };
    }

    /// <summary>
    /// 创建授权失败的结果
    /// </summary>
    public static AuthorizationResult Failure(string reason)
    {
        return new AuthorizationResult
        {
            IsAuthorized = false,
            FailureReason = reason
        };
    }

    /// <summary>
    /// 创建授权失败的结果，包含详细信息
    /// </summary>
    public static AuthorizationResult Failure(string reason, List<string> details)
    {
        return new AuthorizationResult
        {
            IsAuthorized = false,
            FailureReason = reason,
            FailureDetails = details
        };
    }
}

using System.Security.Claims;
using Domain.Authentication;

namespace Domain.Services;

/// <summary>
/// 授权服务接口
/// 用于检查已认证的用户是否有权执行某个操作
/// 
/// 实现说明：
/// - 根据Policy检查用户的角色和声明
/// - 支持灵活的授权策略
/// - 返回详细的失败原因（用于日志和调试）
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// 同步检查用户是否有权访问某个资源
    /// </summary>
    /// <param name="user">认证后的用户Principal</param>
    /// <param name="policy">授权策略</param>
    /// <returns>授权结果</returns>
    AuthorizationResult CheckAuthorization(ClaimsPrincipal? user, AuthPolicy? policy);

    /// <summary>
    /// 异步检查用户是否有权访问某个资源
    /// </summary>
    /// <param name="user">认证后的用户Principal</param>
    /// <param name="policy">授权策略</param>
    /// <returns>授权结果</returns>
    Task<AuthorizationResult> CheckAuthorizationAsync(ClaimsPrincipal? user, AuthPolicy? policy);

    /// <summary>
    /// 检查用户是否有特定的角色
    /// </summary>
    /// <param name="user">用户Principal</param>
    /// <param name="role">角色名</param>
    /// <returns>用户是否拥有该角色</returns>
    bool HasRole(ClaimsPrincipal? user, string role);

    /// <summary>
    /// 检查用户是否有特定的声明（Claim）
    /// </summary>
    /// <param name="user">用户Principal</param>
    /// <param name="claimType">声明类型</param>
    /// <param name="claimValue">声明值（可选，如果不指定则只检查是否存在该声明类型）</param>
    /// <returns>用户是否拥有该声明</returns>
    bool HasClaim(ClaimsPrincipal? user, string claimType, string? claimValue = null);

    /// <summary>
    /// 获取用户的所有角色
    /// </summary>
    /// <param name="user">用户Principal</param>
    /// <returns>角色列表</returns>
    IEnumerable<string> GetUserRoles(ClaimsPrincipal? user);

    /// <summary>
    /// 获取用户的特定声明值
    /// </summary>
    /// <param name="user">用户Principal</param>
    /// <param name="claimType">声明类型</param>
    /// <returns>声明值列表</returns>
    IEnumerable<string> GetClaimValues(ClaimsPrincipal? user, string claimType);
}

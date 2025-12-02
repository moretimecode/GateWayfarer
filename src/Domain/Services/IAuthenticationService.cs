using System.Security.Claims;
using Domain.Authentication;

namespace Domain.Services;

/// <summary>
/// 认证服务接口
/// 用于验证Token并提取用户信息
/// 
/// 实现说明：
/// - 验证JWT Token的签名和过期时间
/// - 从Token中提取Claims（声明）
/// - 返回ClaimsPrincipal（用于ASP.NET Core Identity）
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// 验证并提取Token中的信息
    /// </summary>
    /// <param name="token">Token字符串（不包含 "Bearer " 前缀）</param>
    /// <returns>认证结果，包含用户Principal或失败原因</returns>
    AuthenticationResult VerifyToken(string token);

    /// <summary>
    /// 异步版本的验证Token
    /// </summary>
    Task<AuthenticationResult> VerifyTokenAsync(string token);

    /// <summary>
    /// 生成Token（用于测试或内部使用）
    /// </summary>
    /// <param name="claims">用户声明</param>
    /// <param name="expirationMinutes">Token过期时间（分钟）</param>
    /// <returns>生成的Token字符串</returns>
    string GenerateToken(IEnumerable<Claim> claims, int expirationMinutes = 60);

    /// <summary>
    /// 从HttpRequest中提取Token
    /// 支持多种格式：
    /// - Authorization: Bearer {token}
    /// - X-API-Key: {token}
    /// </summary>
    /// <param name="authorizationHeader">Authorization Header值</param>
    /// <returns>Token字符串，如果格式不正确则返回null</returns>
    string? ExtractToken(string? authorizationHeader);
}

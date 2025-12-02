using System.Security.Claims;
using Domain.Authentication;
using Domain.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// 授权服务实现
/// 根据Policy和用户声明进行授权检查
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(ILogger<AuthorizationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 同步检查授权
    /// </summary>
    public AuthorizationResult CheckAuthorization(ClaimsPrincipal? user, AuthPolicy? policy)
    {
        // 如果没有Policy，则允许访问
        if (policy == null || policy.IsPublic)
        {
            _logger.LogDebug("✅ 公开策略，允许访问");
            return AuthorizationResult.Success();
        }

        // 检查是否需要认证
        if (policy.RequireAuthentication)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("❌ 需要认证但用户未认证");
                return AuthorizationResult.Failure("User is not authenticated");
            }
        }

        var failures = new List<string>();

        // 检查必需的角色
        if (policy.RequiredRoles.Any())
        {
            var userRoles = GetUserRoles(user).ToList();
            var hasRequiredRole = policy.RequiredRoles.Any(r => userRoles.Contains(r));

            if (!hasRequiredRole)
            {
                var missingRoles = string.Join(", ", policy.RequiredRoles);
                var message = $"User lacks required role(s): {missingRoles}";
                failures.Add(message);
                _logger.LogWarning("❌ 缺少必需的角色: {RequiredRoles}, 用户角色: {UserRoles}",
                    missingRoles, string.Join(", ", userRoles));
            }
        }

        // 检查必需的声明
        if (policy.RequiredClaims.Any())
        {
            var missingClaims = new List<string>();
            foreach (var claim in policy.RequiredClaims)
            {
                if (!HasClaim(user, claim))
                {
                    missingClaims.Add(claim);
                }
            }

            if (missingClaims.Any())
            {
                var message = $"User missing required claims: {string.Join(", ", missingClaims)}";
                failures.Add(message);
                _logger.LogWarning("❌ 缺少必需的声明: {MissingClaims}",
                    string.Join(", ", missingClaims));
            }
        }

        // 检查允许的范围（Scopes）
        if (policy.AllowedScopes.Any())
        {
            var userScopes = GetClaimValues(user, "scope").ToList();
            var hasAllowedScope = policy.AllowedScopes.Any(s => userScopes.Contains(s));

            if (!hasAllowedScope)
            {
                var missingScopes = string.Join(", ", policy.AllowedScopes);
                var message = $"User lacks required scope(s): {missingScopes}";
                failures.Add(message);
                _logger.LogWarning("❌ 缺少必需的scope: {AllowedScopes}, 用户scope: {UserScopes}",
                    missingScopes, string.Join(", ", userScopes));
            }
        }

        if (failures.Any())
        {
            return AuthorizationResult.Failure(
                "User is not authorized to access this resource",
                failures);
        }

        _logger.LogInformation("✅ 用户授权检查通过，用户: {UserId}",
            user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");

        return AuthorizationResult.Success();
    }

    /// <summary>
    /// 异步检查授权
    /// </summary>
    public async Task<AuthorizationResult> CheckAuthorizationAsync(ClaimsPrincipal? user, AuthPolicy? policy)
    {
        return await Task.FromResult(CheckAuthorization(user, policy));
    }

    /// <summary>
    /// 检查用户是否拥有特定角色
    /// </summary>
    public bool HasRole(ClaimsPrincipal? user, string role)
    {
        if (user == null || string.IsNullOrWhiteSpace(role))
            return false;

        return user.IsInRole(role);
    }

    /// <summary>
    /// 检查用户是否拥有特定声明
    /// </summary>
    public bool HasClaim(ClaimsPrincipal? user, string claimType, string? claimValue = null)
    {
        if (user == null || string.IsNullOrWhiteSpace(claimType))
            return false;

        if (string.IsNullOrWhiteSpace(claimValue))
        {
            // 只检查声明类型是否存在
            return user.HasClaim(c => c.Type == claimType);
        }

        // 检查声明类型和值都匹配
        return user.HasClaim(claimType, claimValue);
    }

    /// <summary>
    /// 获取用户的所有角色
    /// </summary>
    public IEnumerable<string> GetUserRoles(ClaimsPrincipal? user)
    {
        if (user == null)
            return Enumerable.Empty<string>();

        return user.FindAll(ClaimTypes.Role).Select(c => c.Value);
    }

    /// <summary>
    /// 获取用户的特定声明值
    /// </summary>
    public IEnumerable<string> GetClaimValues(ClaimsPrincipal? user, string claimType)
    {
        if (user == null || string.IsNullOrWhiteSpace(claimType))
            return Enumerable.Empty<string>();

        return user.FindAll(claimType).Select(c => c.Value);
    }
}

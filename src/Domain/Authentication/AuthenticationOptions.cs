using System.Collections.Generic;

namespace Domain.Authentication;

/// <summary>
/// 认证配置选项 - 用于配置全局的认证行为
/// 在 appsettings.json 中的 ApiGateway:Authentication 部分配置
/// </summary>
public class AuthenticationOptions
{
    public const string SectionName = "ApiGateway:Authentication";

    /// <summary>
    /// 是否启用认证
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Token来源 - 通常是 "Authorization" 或 "X-API-Key"
    /// </summary>
    public string TokenHeader { get; set; } = "Authorization";

    /// <summary>
    /// Token前缀 - 通常是 "Bearer" (格式: "Bearer token_value")
    /// </summary>
    public string TokenScheme { get; set; } = "Bearer";

    /// <summary>
    /// JWT密钥 - 用于验证Token签名
    /// 生产环境应该从安全存储（如Key Vault）读取
    /// </summary>
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>
    /// JWT颁发者
    /// </summary>
    public string JwtIssuer { get; set; } = "ApiGateway";

    /// <summary>
    /// JWT观众（受众）
    /// </summary>
    public string JwtAudience { get; set; } = "ApiGatewayClient";

    /// <summary>
    /// Token过期时间（分钟）
    /// </summary>
    public int TokenExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// 默认的授权策略 - 当路由没有指定policy时使用
    /// </summary>
    public string DefaultAuthorizationPolicy { get; set; } = "public";

    /// <summary>
    /// 预定义的授权策略列表
    /// </summary>
    public Dictionary<string, AuthPolicy> Policies { get; set; } = new()
    {
        // 公开策略 - 任何人都可以访问
        {
            "public", new AuthPolicy
            {
                Name = "public",
                Description = "Public access - no authentication required",
                RequireAuthentication = false
            }
        },
        // 认证策略 - 需要有效Token，但没有特定角色要求
        {
            "authenticated", new AuthPolicy
            {
                Name = "authenticated",
                Description = "Authenticated users only",
                RequireAuthentication = true,
                RequiredClaims = new List<string> { "sub" }  // "sub" 是标准的用户ID声明
            }
        },
        // 管理员策略 - 需要有admin角色
        {
            "admin", new AuthPolicy
            {
                Name = "admin",
                Description = "Administrators only",
                RequireAuthentication = true,
                RequiredRoles = new List<string> { "admin" },
                RequiredClaims = new List<string> { "sub", "role" }
            }
        }
    };
}

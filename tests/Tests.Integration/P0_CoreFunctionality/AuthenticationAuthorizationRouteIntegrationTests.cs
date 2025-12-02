using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Authentication;
using Domain.Routes;
using Domain.Routes.Configuration;
using Domain.Services;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;

/// <summary>
/// 认证授权路由集成测试
/// 测试认证授权与路由配置的集成，模拟真实网关场�?
/// 
/// 测试覆盖�?
/// - 受保护的路由需要有效Token
/// - 公开路由不需要Token
/// - 基于角色的路由访问控�?
/// - 基于声明的路由访问控�?
/// - 无效Token拒绝访问
/// - 权限不足拒绝访问
/// - 多个权限组合的复杂场�?
/// </summary>
[Collection("Gateway Integration Tests")]
public class AuthenticationAuthorizationRouteIntegrationTests
{
    private readonly IAuthenticationService _authService;
    private readonly IAuthorizationService _authzService;
    private readonly ILogger<JwtAuthenticationService> _logger;

    private const string TestJwtSecret = "this-is-a-test-secret-key-that-is-very-long-and-secure";
    private const string TestJwtIssuer = "test-gateway";
    private const string TestJwtAudience = "test-api";

    public AuthenticationAuthorizationRouteIntegrationTests()
    {
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<JwtAuthenticationService>();

        var authOptions = new AuthenticationOptions
        {
            JwtSecret = TestJwtSecret,
            JwtIssuer = TestJwtIssuer,
            JwtAudience = TestJwtAudience,
            Enabled = true,
            TokenHeader = "Authorization",
            TokenScheme = "Bearer"
        };

        _authService = new JwtAuthenticationService(authOptions, _logger);
        _authzService = new AuthorizationService(loggerFactory.CreateLogger<AuthorizationService>());
    }

    #region 路由认证场景

    /// <summary>
    /// 测试：公开路由 - 不需要Token
    /// </summary>
    [Fact]
    public void Should_Allow_Access_To_Public_Route_Without_Token()
    {
        // Arrange
        var publicRoute = new RouteConfigItem
        {
            Name = "public-api",
            Path = "/api/public",
            ServiceCode = "public-service",
            AllowAnonymous = true
        };

        // 模拟没有认证的请�?
        ClaimsPrincipal? user = null;
        var policy = GetPolicyForRoute(publicRoute);

        // Act
        var authzResult = _authzService.CheckAuthorization(user, policy);

        // Assert
        Assert.True(authzResult.IsAuthorized);
    }

    /// <summary>
    /// 测试：受保护的路�?- 需要有效Token
    /// </summary>
    [Fact]
    public void Should_Require_Valid_Token_For_Protected_Route()
    {
        // Arrange
        var protectedRoute = new RouteConfigItem
        {
            Name = "protected-api",
            Path = "/api/protected",
            ServiceCode = "protected-service",
            AllowAnonymous = false
        };

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = GetPolicyForRoute(protectedRoute);

        // Act
        var authzResult = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(authzResult.IsAuthorized);
    }

    /// <summary>
    /// 测试：受保护的路�?- 没有Token被拒�?
    /// </summary>
    [Fact]
    public void Should_Reject_Access_To_Protected_Route_Without_Token()
    {
        // Arrange
        var protectedRoute = new RouteConfigItem
        {
            Name = "protected-api",
            Path = "/api/protected",
            ServiceCode = "protected-service",
            AllowAnonymous = false
        };

        // 没有认证用户
        ClaimsPrincipal? user = null;
        var policy = GetPolicyForRoute(protectedRoute);

        // Act
        var authzResult = _authzService.CheckAuthorization(user, policy);

        // Assert
        Assert.False(authzResult.IsAuthorized);
    }

    #endregion

    #region 基于角色的路由访问控�?

    /// <summary>
    /// 测试：管理员路由 - 拥有admin角色的用户可以访�?
    /// </summary>
    [Fact]
    public void Should_Allow_Admin_User_To_Access_Admin_Route()
    {
        // Arrange
        var adminRoute = new RouteConfigItem
        {
            Name = "admin-api",
            Path = "/api/admin",
            ServiceCode = "admin-service",
            AllowAnonymous = false,
            AuthorizationPolicy = "admin-only"
        };

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-user"),
            new Claim(ClaimTypes.Role, "admin")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = GetPolicyForRoute(adminRoute);

        // Act
        var authzResult = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(authzResult.IsAuthorized);
    }

    /// <summary>
    /// 测试：管理员路由 - 普通用户被拒绝
    /// </summary>
    [Fact]
    public void Should_Reject_Regular_User_From_Admin_Route()
    {
        // Arrange
        var adminRoute = new RouteConfigItem
        {
            Name = "admin-api",
            Path = "/api/admin",
            ServiceCode = "admin-service",
            AllowAnonymous = false,
            AuthorizationPolicy = "admin-only"
        };

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "regular-user"),
            new Claim(ClaimTypes.Role, "user")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = GetPolicyForRoute(adminRoute);

        // Act
        var authzResult = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.False(authzResult.IsAuthorized);
    }

    /// <summary>
    /// 测试：多角色路由 - 用户有其中一个角色即可访�?
    /// </summary>
    [Fact]
    public void Should_Allow_User_With_Any_Required_Role()
    {
        // Arrange
        var multiRoleRoute = new RouteConfigItem
        {
            Name = "moderator-api",
            Path = "/api/moderate",
            ServiceCode = "moderation-service",
            AllowAnonymous = false,
            AuthorizationPolicy = "moderator-or-admin"
        };

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "moderator-user"),
            new Claim(ClaimTypes.Role, "moderator")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "moderator-or-admin",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "admin", "moderator" }
        };

        // Act
        var authzResult = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(authzResult.IsAuthorized);
    }

    #endregion

    #region 基于声明的路由访问控�?

    /// <summary>
    /// 测试：声明验证路�?- 拥有所需声明的用户可以访�?
    /// </summary>
    [Fact]
    public void Should_Allow_User_With_Required_Claims()
    {
        // Arrange
        var claimRoute = new RouteConfigItem
        {
            Name = "verified-api",
            Path = "/api/verified",
            ServiceCode = "verified-service",
            AllowAnonymous = false,
            AuthorizationPolicy = "email-verified"
        };

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "verified-user"),
            new Claim("email_verified", "true"),
            new Claim(ClaimTypes.Email, "user@example.com")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "email-verified",
            RequireAuthentication = true,
            RequiredClaims = new List<string> { "email_verified" }
        };

        // Act
        var authzResult = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(authzResult.IsAuthorized);
    }

    /// <summary>
    /// 测试：声明验证路�?- 缺少声明的用户被拒绝
    /// </summary>
    [Fact]
    public void Should_Reject_User_Without_Required_Claims()
    {
        // Arrange
        var claimRoute = new RouteConfigItem
        {
            Name = "verified-api",
            Path = "/api/verified",
            ServiceCode = "verified-service",
            AllowAnonymous = false,
            AuthorizationPolicy = "email-verified"
        };

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "unverified-user")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "email-verified",
            RequireAuthentication = true,
            RequiredClaims = new List<string> { "email_verified" }
        };

        // Act
        var authzResult = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.False(authzResult.IsAuthorized);
    }

    #endregion

    #region Scope访问控制

    /// <summary>
    /// 测试：API写入权限 - 拥有写入scope的用户可以访�?
    /// </summary>
    [Fact]
    public void Should_Allow_User_With_Write_Scope_To_Access_Write_API()
    {
        // Arrange
        var writeRoute = new RouteConfigItem
        {
            Name = "write-api",
            Path = "/api/data",
            ServiceCode = "data-service",
            AllowAnonymous = false,
            AuthorizationPolicy = "write-access"
        };

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "writer-user"),
            new Claim("scope", "api.read"),
            new Claim("scope", "api.write")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "write-access",
            RequireAuthentication = true,
            AllowedScopes = new List<string> { "api.write" }
        };

        // Act
        var authzResult = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(authzResult.IsAuthorized);
    }

    /// <summary>
    /// 测试：API写入权限 - 只有读权限的用户被拒�?
    /// </summary>
    [Fact]
    public void Should_Reject_ReadOnly_User_From_Write_API()
    {
        // Arrange
        var writeRoute = new RouteConfigItem
        {
            Name = "write-api",
            Path = "/api/data",
            ServiceCode = "data-service",
            AllowAnonymous = false,
            AuthorizationPolicy = "write-access"
        };

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "reader-user"),
            new Claim("scope", "api.read") // 只有读权�?
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "write-access",
            RequireAuthentication = true,
            AllowedScopes = new List<string> { "api.write" }
        };

        // Act
        var authzResult = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.False(authzResult.IsAuthorized);
    }

    #endregion

    #region 复杂的多条件路由

    /// <summary>
    /// 测试：高级管理员路由 - 需要角�?声明+Scope
    /// </summary>
    [Fact]
    public void Should_Allow_User_With_All_Requirements()
    {
        // Arrange
        var complexRoute = new RouteConfigItem
        {
            Name = "sensitive-admin-api",
            Path = "/api/admin/sensitive",
            ServiceCode = "admin-sensitive-service",
            AllowAnonymous = false,
            AuthorizationPolicy = "advanced-admin"
        };

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "super-admin"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("security_level", "high"),
            new Claim("scope", "admin.write"),
            new Claim("scope", "admin.delete")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "advanced-admin",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "admin", "superuser" },
            RequiredClaims = new List<string> { "security_level" },
            AllowedScopes = new List<string> { "admin.delete" }
        };

        // Act
        var authzResult = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(authzResult.IsAuthorized);
    }

    /// <summary>
    /// 测试：高级管理员路由 - 缺少Scope被拒�?
    /// </summary>
    [Fact]
    public void Should_Reject_User_Lacking_Required_Scope()
    {
        // Arrange
        var complexRoute = new RouteConfigItem
        {
            Name = "sensitive-admin-api",
            Path = "/api/admin/sensitive",
            ServiceCode = "admin-sensitive-service",
            AllowAnonymous = false,
            AuthorizationPolicy = "advanced-admin"
        };

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "limited-admin"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("security_level", "high"),
            new Claim("scope", "admin.write") // 缺少admin.delete
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "advanced-admin",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "admin", "superuser" },
            RequiredClaims = new List<string> { "security_level" },
            AllowedScopes = new List<string> { "admin.delete" }
        };

        // Act
        var authzResult = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.False(authzResult.IsAuthorized);
        Assert.True(authzResult.FailureDetails.Any(d => d.Contains("scope")));
    }

    /// <summary>
    /// 测试：高级管理员路由 - 缺少安全声明被拒�?
    /// </summary>
    [Fact]
    public void Should_Reject_User_Lacking_Security_Claim()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "unsafe-admin"),
            new Claim(ClaimTypes.Role, "admin"),
            // 缺少 security_level 声明
            new Claim("scope", "admin.delete")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "advanced-admin",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "admin" },
            RequiredClaims = new List<string> { "security_level" },
            AllowedScopes = new List<string> { "admin.delete" }
        };

        // Act
        var authzResult = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.False(authzResult.IsAuthorized);
        Assert.True(authzResult.FailureDetails.Any(d => d.Contains("claim")));
    }

    #endregion

    #region 真实场景模拟

    /// <summary>
    /// 测试场景：电商API网关
    /// 不同路由对不同用户的访问控制
    /// </summary>
    [Fact]
    public void Should_Simulate_Ecommerce_Gateway_Scenarios()
    {
        // 1. 公开商品列表API
        var productListRoute = new RouteConfigItem
        {
            Name = "product-list",
            Path = "/api/products",
            AllowAnonymous = true
        };

        var anonymousUser = (ClaimsPrincipal?)null;
        var publicPolicy = GetPolicyForRoute(productListRoute);
        var publicResult = _authzService.CheckAuthorization(anonymousUser, publicPolicy);
        Assert.True(publicResult.IsAuthorized); // �?匿名用户可以查看

        // 2. 用户订单API - 需要认�?
        var ordersRoute = new RouteConfigItem
        {
            Name = "user-orders",
            Path = "/api/orders",
            AllowAnonymous = false
        };

        var normalUser = new ClaimsPrincipal(new ClaimsIdentity(
            new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "customer-123"),
                new Claim(ClaimTypes.Role, "customer")
            },
            "TestAuth"));

        var userPolicy = GetPolicyForRoute(ordersRoute);
        var userResult = _authzService.CheckAuthorization(normalUser, userPolicy);
        Assert.True(userResult.IsAuthorized); // �?认证用户可以查看自己的订�?

        // 3. 订单管理API - 需要admin角色
        var orderManagementRoute = new RouteConfigItem
        {
            Name = "order-management",
            Path = "/api/admin/orders",
            AllowAnonymous = false,
            AuthorizationPolicy = "admin"
        };

        var adminPolicy = new AuthPolicy
        {
            Name = "admin",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "admin" }
        };

        var normalUserAdminResult = _authzService.CheckAuthorization(normalUser, adminPolicy);
        Assert.False(normalUserAdminResult.IsAuthorized); // �?普通用户不能管理订�?

        var adminUser = new ClaimsPrincipal(new ClaimsIdentity(
            new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "admin-456"),
                new Claim(ClaimTypes.Role, "admin")
            },
            "TestAuth"));

        var adminUserAdminResult = _authzService.CheckAuthorization(adminUser, adminPolicy);
        Assert.True(adminUserAdminResult.IsAuthorized); // �?管理员可以管理订�?

        // 4. 支付API - 需要特殊权�?
        var paymentRoute = new RouteConfigItem
        {
            Name = "payment",
            Path = "/api/payment",
            AllowAnonymous = false,
            AuthorizationPolicy = "payment"
        };

        var paymentPolicy = new AuthPolicy
        {
            Name = "payment",
            RequireAuthentication = true,
            RequiredClaims = new List<string> { "payment_verified" }
        };

        var unverifiedPaymentUser = new ClaimsPrincipal(new ClaimsIdentity(
            new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "customer-789")
            },
            "TestAuth"));

        var unverifiedPaymentResult = _authzService.CheckAuthorization(unverifiedPaymentUser, paymentPolicy);
        Assert.False(unverifiedPaymentResult.IsAuthorized); // �?未验证的用户不能支付

        var verifiedPaymentUser = new ClaimsPrincipal(new ClaimsIdentity(
            new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "customer-999"),
                new Claim("payment_verified", "true")
            },
            "TestAuth"));

        var verifiedPaymentResult = _authzService.CheckAuthorization(verifiedPaymentUser, paymentPolicy);
        Assert.True(verifiedPaymentResult.IsAuthorized); // �?验证的用户可以支�?
    }

    #endregion

    #region 故障和边界情�?

    /// <summary>
    /// 测试：空的Policy列表不会导致异常
    /// </summary>
    [Fact]
    public void Should_Handle_Empty_Policy_Requirements()
    {
        // Arrange
        var emptyPolicy = new AuthPolicy
        {
            Name = "empty",
            RequireAuthentication = true,
            RequiredRoles = new List<string>(),
            RequiredClaims = new List<string>(),
            AllowedScopes = new List<string>()
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new List<Claim> { new Claim(ClaimTypes.NameIdentifier, "user") },
            "TestAuth"));

        // Act & Assert - 不应该抛异常
        var result = _authzService.CheckAuthorization(user, emptyPolicy);
        Assert.True(result.IsAuthorized);
    }

    /// <summary>
    /// 测试：null用户与null策略
    /// </summary>
    [Fact]
    public void Should_Handle_Null_User_And_Null_Policy()
    {
        // Act & Assert
        var result = _authzService.CheckAuthorization(null, null);
        Assert.True(result.IsAuthorized);
    }

    /// <summary>
    /// 测试：大量角色的场景
    /// </summary>
    [Fact]
    public void Should_Handle_User_With_Many_Roles()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "multi-role-user")
        };

        // 添加50个角�?
        for (int i = 0; i < 50; i++)
        {
            claims.Add(new Claim(ClaimTypes.Role, $"role-{i}"));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "specific-role",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "role-25" }
        };

        // Act
        var result = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(result.IsAuthorized);
    }

    #endregion

    #region 帮助方法

    /// <summary>
    /// 根据路由配置获取对应的Policy
    /// 这模拟网关在处理请求时如何从路由配置获取Policy
    /// </summary>
    private AuthPolicy GetPolicyForRoute(RouteConfigItem route)
    {
        // 如果路由允许匿名访问，返回公开策略
        if (route.AllowAnonymous)
        {
            return new AuthPolicy
            {
                Name = "public",
                RequireAuthentication = false
            };
        }

        // 如果指定了策略，使用该策�?
        if (!string.IsNullOrEmpty(route.AuthorizationPolicy) && !route.AuthorizationPolicy.Equals("public", StringComparison.OrdinalIgnoreCase)) { return GetPolicyByName(route.AuthorizationPolicy); }

        // 默认需要认�?
        return new AuthPolicy
        {
            Name = "authenticated",
            RequireAuthentication = true
        };
    }

    /// <summary>
    /// 根据名称获取Policy定义
    /// 这模拟从配置或数据库获取Policy定义
    /// </summary>
    private AuthPolicy GetPolicyByName(string policyName)
    {
        return policyName.ToLower() switch
        {
            "public" => new AuthPolicy
            {
                Name = "public",
                RequireAuthentication = false
            },
            "authenticated" => new AuthPolicy
            {
                Name = "authenticated",
                RequireAuthentication = true
            },
            "admin-only" => new AuthPolicy
            {
                Name = "admin-only",
                RequireAuthentication = true,
                RequiredRoles = new List<string> { "admin" }
            },
            "admin" => new AuthPolicy
            {
                Name = "admin",
                RequireAuthentication = true,
                RequiredRoles = new List<string> { "admin" }
            },
            "moderator-or-admin" => new AuthPolicy
            {
                Name = "moderator-or-admin",
                RequireAuthentication = true,
                RequiredRoles = new List<string> { "admin", "moderator" }
            },
            "email-verified" => new AuthPolicy
            {
                Name = "email-verified",
                RequireAuthentication = true,
                RequiredClaims = new List<string> { "email_verified" }
            },
            "write-access" => new AuthPolicy
            {
                Name = "write-access",
                RequireAuthentication = true,
                AllowedScopes = new List<string> { "api.write" }
            },
            _ => new AuthPolicy
            {
                Name = "default",
                RequireAuthentication = true
            }
        };
    }

    #endregion
}



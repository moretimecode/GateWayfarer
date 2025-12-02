using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Authentication;
using Domain.Services;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;

/// <summary>
/// 认证授权集成测试
/// 测试JWT验证、Token生成、权限检查等场景
/// 
/// 测试覆盖：
/// - Token生成与验证
/// - 过期Token检查
/// - 错误的密钥拒绝
/// - 角色检查
/// - 声明检查
/// - Scope检查
/// - 公开策略
/// - 多角色和多声明组合
/// - 并发Token验证
/// </summary>
[Collection("Gateway Integration Tests")]
public class AuthenticationAuthorizationTests
{
    private readonly AuthenticationOptions _authOptions;
    private readonly IAuthenticationService _authService;
    private readonly IAuthorizationService _authzService;
    private readonly ILogger<JwtAuthenticationService> _logger;

    // 测试用密钥
    private const string TestJwtSecret = "this-is-a-test-secret-key-that-is-very-long-and-secure";
    private const string TestJwtIssuer = "test-gateway";
    private const string TestJwtAudience = "test-api";

    public AuthenticationAuthorizationTests()
    {
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<JwtAuthenticationService>();

        _authOptions = new AuthenticationOptions
        {
            JwtSecret = TestJwtSecret,
            JwtIssuer = TestJwtIssuer,
            JwtAudience = TestJwtAudience,
            Enabled = true,
            TokenHeader = "Authorization",
            TokenScheme = "Bearer"
        };

        _authService = new JwtAuthenticationService(_authOptions, _logger);
        _authzService = new AuthorizationService(loggerFactory.CreateLogger<AuthorizationService>());
    }

    #region Token生成和验证

    /// <summary>
    /// 测试：生成有效的JWT Token
    /// </summary>
    [Fact]
    public void Should_Generate_Valid_JWT_Token()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.Email, "test@example.com")
        };

        // Act
        var token = _authService.GenerateToken(claims, expirationMinutes: 60);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        // Token应该是JWT格式（三部分，用.分隔）
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);
    }

    /// <summary>
    /// 测试：生成的Token可以被正确验证
    /// </summary>
    [Fact]
    public void Should_Verify_Generated_Token()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-456"),
            new Claim(ClaimTypes.Name, "Alice"),
            new Claim(ClaimTypes.Email, "alice@example.com")
        };

        var token = _authService.GenerateToken(claims, expirationMinutes: 60);

        // Act
        var result = _authService.VerifyToken(token);

        // Assert
        Assert.True(result.IsAuthenticated);
        Assert.NotNull(result.Principal);
        Assert.Null(result.FailureReason);
        Assert.Equal("user-456", result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Alice", result.Principal.FindFirst(ClaimTypes.Name)?.Value);
    }

    /// <summary>
    /// 测试：验证Token时提取所有声明
    /// </summary>
    [Fact]
    public void Should_Extract_All_Claims_From_Token()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-789"),
            new Claim(ClaimTypes.Name, "Bob"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ClaimTypes.Role, "user"),
            new Claim("scope", "api.read"),
            new Claim("scope", "api.write"),
            new Claim("custom", "value123")
        };

        var token = _authService.GenerateToken(claims);

        // Act
        var result = _authService.VerifyToken(token);

        // Assert
        Assert.True(result.IsAuthenticated);
        var principal = result.Principal!;

        // 检查基本声明
        Assert.Equal("user-789", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Bob", principal.FindFirst(ClaimTypes.Name)?.Value);

        // 检查多值声明
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains("admin", roles);
        Assert.Contains("user", roles);

        var scopes = principal.FindAll("scope").Select(c => c.Value).ToList();
        Assert.Contains("api.read", scopes);
        Assert.Contains("api.write", scopes);

        // 检查自定义声明
        Assert.Equal("value123", principal.FindFirst("custom")?.Value);
    }

    #endregion

    #region Token验证失败场景

    /// <summary>
    /// 测试：空Token被拒绝
    /// </summary>
    [Fact]
    public void Should_Reject_Empty_Token()
    {
        // Act
        var result = _authService.VerifyToken(string.Empty);

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("empty", result.FailureReason.ToLower());
    }

    /// <summary>
    /// 测试：无效格式的Token被拒绝
    /// </summary>
    [Fact]
    public void Should_Reject_Malformed_Token()
    {
        // Act
        var result = _authService.VerifyToken("not.a.valid.token.with.too.many.parts");

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.NotNull(result.FailureReason);
    }

    /// <summary>
    /// 测试：使用错误密钥签名的Token被拒绝
    /// </summary>
    [Fact]
    public void Should_Reject_Token_With_Wrong_Signature()
    {
        // Arrange - 用正确的密钥生成Token
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-sig-test")
        };

        var token = _authService.GenerateToken(claims);

        // 创建使用错误密钥的认证服务
        var wrongKeyOptions = new AuthenticationOptions
        {
            JwtSecret = "this-is-a-different-secret-key-that-is-very-long",
            JwtIssuer = TestJwtIssuer,
            JwtAudience = TestJwtAudience,
            Enabled = true,
            TokenHeader = "Authorization",
            TokenScheme = "Bearer"
        };

        var wrongAuthService = new JwtAuthenticationService(wrongKeyOptions, _logger);

        // Act - 用错误的密钥验证Token
        var result = wrongAuthService.VerifyToken(token);

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("signature", result.FailureReason.ToLower());
    }

    /// <summary>
    /// 测试：过期的Token被拒绝
    /// </summary>
    [Fact]
    public void Should_Reject_Expired_Token()
    {
        // Arrange - 生成一个已经过期的Token（过期时间为负数）
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-expired")
        };

        // 需要直接创建一个过期Token，因为 GenerateToken 不支持负数分钟
        var key = Encoding.UTF8.GetBytes(_authOptions.JwtSecret);
        var tokenHandler = new JwtSecurityTokenHandler();

        var issuedAt = DateTime.UtcNow.AddMinutes(-2); // 2分钟前发行
        var expireTime = DateTime.UtcNow.AddSeconds(-30); // 30秒前已过期（在发行之后）
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expireTime,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Issuer = _authOptions.JwtIssuer,
            Audience = _authOptions.JwtAudience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var expiredToken = tokenHandler.WriteToken(token);

        // Act
        var result = _authService.VerifyToken(expiredToken);

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("expired", result.FailureReason.ToLower());
    }

    #region Token提取

    /// <summary>
    /// 测试：从Authorization Header提取Bearer Token
    /// </summary>
    [Fact]
    public void Should_Extract_Bearer_Token_From_Header()
    {
        // Arrange
        const string authHeader = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...";

        // Act
        var token = _authService.ExtractToken(authHeader);

        // Assert
        Assert.NotNull(token);
        Assert.Equal("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", token);
    }

    /// <summary>
    /// 测试：Bearer前缀不区分大小写
    /// </summary>
    [Fact]
    public void Should_Extract_Token_With_Lowercase_Bearer()
    {
        // Arrange
        const string authHeader = "bearer my-secret-token";

        // Act
        var token = _authService.ExtractToken(authHeader);

        // Assert
        Assert.NotNull(token);
        Assert.Equal("my-secret-token", token);
    }

    /// <summary>
    /// 测试：无Bearer前缀的Header返回null
    /// </summary>
    [Fact]
    public void Should_Return_Null_For_Non_Bearer_Header()
    {
        // Arrange
        const string authHeader = "Basic dXNlcjpwYXNz";

        // Act
        var token = _authService.ExtractToken(authHeader);

        // Assert
        Assert.Null(token);
    }

    /// <summary>
    /// 测试：空Header返回null
    /// </summary>
    [Fact]
    public void Should_Return_Null_For_Empty_Header()
    {
        // Act
        var token1 = _authService.ExtractToken(string.Empty);
        var token2 = _authService.ExtractToken(null);

        // Assert
        Assert.Null(token1);
        Assert.Null(token2);
    }

    #endregion

    #region 授权 - 公开策略

    /// <summary>
    /// 测试：公开策略允许所有人访问
    /// </summary>
    [Fact]
    public void Should_Allow_Public_Policy_For_Unauthenticated_Users()
    {
        // Arrange
        var policy = new AuthPolicy
        {
            Name = "public",
            RequireAuthentication = false
        };

        // Act
        var result = _authzService.CheckAuthorization(null, policy);

        // Assert
        Assert.True(result.IsAuthorized);
    }

    /// <summary>
    /// 测试：null策略被视为公开
    /// </summary>
    [Fact]
    public void Should_Allow_Access_When_Policy_Is_Null()
    {
        // Act
        var result = _authzService.CheckAuthorization(null, null);

        // Assert
        Assert.True(result.IsAuthorized);
    }

    #endregion

    #region 授权 - 认证要求

    /// <summary>
    /// 测试：需要认证的策略拒绝未认证用户
    /// </summary>
    [Fact]
    public void Should_Reject_Unauthenticated_User_For_Auth_Required_Policy()
    {
        // Arrange
        var policy = new AuthPolicy
        {
            Name = "authenticated",
            RequireAuthentication = true
        };

        // Act
        var result = _authzService.CheckAuthorization(null, policy);

        // Assert
        Assert.False(result.IsAuthorized);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("not authenticated", result.FailureReason);
    }

    /// <summary>
    /// 测试：需要认证的策略允许已认证用户
    /// </summary>
    [Fact]
    public void Should_Allow_Authenticated_User_For_Auth_Required_Policy()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-auth")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "authenticated",
            RequireAuthentication = true
        };

        // Act
        var result = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(result.IsAuthorized);
    }

    #endregion

    #region 授权 - 角色检查

    /// <summary>
    /// 测试：用户拥有需要的角色时授权通过
    /// </summary>
    [Fact]
    public void Should_Authorize_User_With_Required_Role()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-user"),
            new Claim(ClaimTypes.Role, "admin")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "admin-only",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "admin" }
        };

        // Act
        var result = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(result.IsAuthorized);
    }

    /// <summary>
    /// 测试：用户缺少需要的角色时授权失败
    /// </summary>
    [Fact]
    public void Should_Reject_User_Without_Required_Role()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "regular-user"),
            new Claim(ClaimTypes.Role, "user")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "admin-only",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "admin" }
        };

        // Act
        var result = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.False(result.IsAuthorized);
        // 授权失败，有失败原因或失败详情
        Assert.NotNull(result.FailureReason);
    }

    /// <summary>
    /// 测试：用户拥有多个角色中的任意一个即可通过
    /// </summary>
    [Fact]
    public void Should_Authorize_User_With_Any_Of_Multiple_Required_Roles()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "moderator"),
            new Claim(ClaimTypes.Role, "moderator")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "admin-or-moderator",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "admin", "moderator" }
        };

        // Act
        var result = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(result.IsAuthorized);
    }

    /// <summary>
    /// 测试：HasRole 方法功能
    /// </summary>
    [Fact]
    public void Should_Check_User_Role_Correctly()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-with-role"),
            new Claim(ClaimTypes.Role, "editor")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act & Assert
        Assert.True(_authzService.HasRole(principal, "editor"));
        Assert.False(_authzService.HasRole(principal, "admin"));
        Assert.False(_authzService.HasRole(null, "admin"));
    }

    /// <summary>
    /// 测试：GetUserRoles 方法获取所有用户角色
    /// </summary>
    [Fact]
    public void Should_Get_All_User_Roles()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "multi-role-user"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ClaimTypes.Role, "editor"),
            new Claim(ClaimTypes.Role, "viewer")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act
        var roles = _authzService.GetUserRoles(principal).ToList();

        // Assert
        Assert.Equal(3, roles.Count);
        Assert.Contains("admin", roles);
        Assert.Contains("editor", roles);
        Assert.Contains("viewer", roles);
    }

    #endregion

    #region 授权 - 声明检查

    /// <summary>
    /// 测试：用户拥有需要的声明时授权通过
    /// </summary>
    [Fact]
    public void Should_Authorize_User_With_Required_Claims()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "claimed-user"),
            new Claim("email_verified", "true"),
            new Claim(ClaimTypes.Email, "user@example.com")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "verified-email-only",
            RequireAuthentication = true,
            RequiredClaims = new List<string> { "email_verified" }
        };

        // Act
        var result = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(result.IsAuthorized);
    }

    /// <summary>
    /// 测试：用户缺少需要的声明时授权失败
    /// </summary>
    [Fact]
    public void Should_Reject_User_Without_Required_Claims()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "unclaimed-user")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "verified-email-only",
            RequireAuthentication = true,
            RequiredClaims = new List<string> { "email_verified", "phone_verified" }
        };

        // Act
        var result = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.False(result.IsAuthorized);
        // 授权失败，有失败原因或失败详情
        Assert.NotNull(result.FailureReason);
    }

    /// <summary>
    /// 测试：HasClaim 方法功能
    /// </summary>
    [Fact]
    public void Should_Check_User_Claims_Correctly()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "claim-test-user"),
            new Claim("department", "engineering"),
            new Claim("level", "senior")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act & Assert
        // 仅检查声明类型是否存在
        Assert.True(_authzService.HasClaim(principal, "department"));
        Assert.True(_authzService.HasClaim(principal, "level"));
        Assert.False(_authzService.HasClaim(principal, "nonexistent"));

        // 同时检查声明类型和值
        Assert.True(_authzService.HasClaim(principal, "department", "engineering"));
        Assert.False(_authzService.HasClaim(principal, "department", "sales"));
    }

    /// <summary>
    /// 测试：GetClaimValues 方法获取声明值
    /// </summary>
    [Fact]
    public void Should_Get_Claim_Values()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "claim-values-user"),
            new Claim("permission", "read"),
            new Claim("permission", "write"),
            new Claim("permission", "delete")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act
        var permissions = _authzService.GetClaimValues(principal, "permission").ToList();

        // Assert
        Assert.Equal(3, permissions.Count);
        Assert.Contains("read", permissions);
        Assert.Contains("write", permissions);
        Assert.Contains("delete", permissions);
    }

    #endregion

    #region 授权 - Scope检查

    /// <summary>
    /// 测试：用户拥有需要的Scope时授权通过
    /// </summary>
    [Fact]
    public void Should_Authorize_User_With_Required_Scope()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "scoped-user"),
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
        var result = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(result.IsAuthorized);
    }

    /// <summary>
    /// 测试：用户缺少需要的Scope时授权失败
    /// </summary>
    [Fact]
    public void Should_Reject_User_Without_Required_Scope()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "readonly-user"),
            new Claim("scope", "api.read")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "write-access",
            RequireAuthentication = true,
            AllowedScopes = new List<string> { "api.write" }
        };

        // Act
        var result = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.False(result.IsAuthorized);
        // 授权失败，有失败原因或失败详情
        Assert.NotNull(result.FailureReason);
    }

    #endregion

    #region 授权 - 综合检查

    /// <summary>
    /// 测试：综合检查 - 角色+声明+Scope
    /// </summary>
    [Fact]
    public void Should_Check_Multiple_Authorization_Requirements()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "full-access-user"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("department", "security"),
            new Claim("scope", "api.admin"),
            new Claim("scope", "api.write")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "complex-policy",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "admin", "superuser" },
            RequiredClaims = new List<string> { "department" },
            AllowedScopes = new List<string> { "api.admin" }
        };

        // Act
        var result = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.True(result.IsAuthorized);
    }

    /// <summary>
    /// 测试：综合检查 - 缺少角色导致失败
    /// </summary>
    [Fact]
    public void Should_Fail_Complex_Check_When_Missing_Role()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "partial-user"),
            new Claim(ClaimTypes.Role, "user"), // 不是所需的admin或superuser
            new Claim("department", "security"),
            new Claim("scope", "api.admin")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "complex-policy",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "admin", "superuser" },
            RequiredClaims = new List<string> { "department" },
            AllowedScopes = new List<string> { "api.admin" }
        };

        // Act
        var result = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.False(result.IsAuthorized);
        Assert.True(result.FailureDetails.Any(d => d.Contains("role")));
    }

    /// <summary>
    /// 测试：综合检查 - 缺少声明导致失败
    /// </summary>
    [Fact]
    public void Should_Fail_Complex_Check_When_Missing_Claims()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "partial-user-2"),
            new Claim(ClaimTypes.Role, "admin"),
            // 缺少department声明
            new Claim("scope", "api.admin")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "complex-policy",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "admin" },
            RequiredClaims = new List<string> { "department", "email_verified" },
            AllowedScopes = new List<string> { "api.admin" }
        };

        // Act
        var result = _authzService.CheckAuthorization(principal, policy);

        // Assert
        Assert.False(result.IsAuthorized);
        Assert.True(result.FailureDetails.Any(d => d.Contains("claim")));
    }

    #endregion

    #region 异步验证

    /// <summary>
    /// 测试：异步Token验证
    /// </summary>
    [Fact]
    public async Task Should_Verify_Token_Async()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "async-user")
        };
        var token = _authService.GenerateToken(claims);

        // Act
        var result = await _authService.VerifyTokenAsync(token);

        // Assert
        Assert.True(result.IsAuthenticated);
        Assert.NotNull(result.Principal);
    }

    /// <summary>
    /// 测试：异步授权检查
    /// </summary>
    [Fact]
    public async Task Should_Check_Authorization_Async()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "async-auth-user"),
            new Claim(ClaimTypes.Role, "user")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "authenticated",
            RequireAuthentication = true
        };

        // Act
        var result = await _authzService.CheckAuthorizationAsync(principal, policy);

        // Assert
        Assert.True(result.IsAuthorized);
    }

    #endregion

    #region 并发验证

    /// <summary>
    /// 测试：并发Token验证
    /// 多个线程同时验证Token应该都成功
    /// </summary>
    [Fact]
    public void Should_Handle_Concurrent_Token_Verification()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "concurrent-user")
        };
        var token = _authService.GenerateToken(claims);

        var concurrencyLevel = 100;
        var results = new List<AuthenticationResult>();
        var lockObj = new object();

        // Act
        Parallel.For(0, concurrencyLevel, i =>
        {
            var result = _authService.VerifyToken(token);
            lock (lockObj)
            {
                results.Add(result);
            }
        });

        // Assert
        Assert.Equal(concurrencyLevel, results.Count);
        Assert.All(results, r => Assert.True(r.IsAuthenticated));
    }

    /// <summary>
    /// 测试：并发Token生成
    /// 多个线程同时生成Token应该都成功
    /// </summary>
    [Fact]
    public void Should_Handle_Concurrent_Token_Generation()
    {
        // Arrange
        var concurrencyLevel = 100;
        var tokens = new List<string>();
        var lockObj = new object();

        // Act
        Parallel.For(0, concurrencyLevel, i =>
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, $"user-{i}")
            };
            var token = _authService.GenerateToken(claims);
            lock (lockObj)
            {
                tokens.Add(token);
            }
        });

        // Assert
        Assert.Equal(concurrencyLevel, tokens.Count);
        Assert.All(tokens, t => Assert.NotNull(t));
        Assert.All(tokens, t => Assert.Equal(3, t.Split('.').Length)); // JWT格式
    }

    /// <summary>
    /// 测试：并发授权检查
    /// 多个线程同时检查授权应该都成功
    /// </summary>
    [Fact]
    public void Should_Handle_Concurrent_Authorization_Check()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "concurrent-auth-user"),
            new Claim(ClaimTypes.Role, "admin")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var policy = new AuthPolicy
        {
            Name = "admin-only",
            RequireAuthentication = true,
            RequiredRoles = new List<string> { "admin" }
        };

        var concurrencyLevel = 100;
        var results = new List<AuthorizationResult>();
        var lockObj = new object();

        // Act
        Parallel.For(0, concurrencyLevel, i =>
        {
            var result = _authzService.CheckAuthorization(principal, policy);
            lock (lockObj)
            {
                results.Add(result);
            }
        });

        // Assert
        Assert.Equal(concurrencyLevel, results.Count);
        Assert.All(results, r => Assert.True(r.IsAuthorized));
    }

    #endregion
}
#endregion
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Authentication;
using Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services;

/// <summary>
/// JWT认证服务实现
/// 使用JWT (JSON Web Token) 进行用户身份验证
/// </summary>
public class JwtAuthenticationService : IAuthenticationService
{
    private readonly AuthenticationOptions _options;
    private readonly ILogger<JwtAuthenticationService> _logger;

    public JwtAuthenticationService(
        Domain.Authentication.AuthenticationOptions options,
        ILogger<JwtAuthenticationService> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 验证JWT Token并返回用户信息
    /// </summary>
    public AuthenticationResult VerifyToken(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return AuthenticationResult.Failure("Token is empty");

            // 创建Token验证参数
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_options.JwtSecret);

            try
            {
                // 验证Token
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _options.JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = _options.JwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(10) // 允许10秒偏差
                }, out SecurityToken validatedToken);

                _logger.LogInformation("✅ Token验证成功，用户: {UserId}",
                    principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");

                return AuthenticationResult.Success(principal, token);
            }
            catch (SecurityTokenExpiredException ex)
            {
                _logger.LogWarning("⏰ Token已过期: {Message}", ex.Message);
                return AuthenticationResult.Failure("Token has expired");
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                _logger.LogWarning("❌ Token签名无效: {Message}", ex.Message);
                return AuthenticationResult.Failure("Invalid token signature");
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning("❌ Token验证失败: {Message}", ex.Message);
                return AuthenticationResult.Failure($"Token validation failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("❌ 验证Token时发生异常: {Message}", ex.Message);
            return AuthenticationResult.Failure($"Error verifying token: {ex.Message}");
        }
    }

    /// <summary>
    /// 异步版本的验证Token
    /// </summary>
    public async Task<AuthenticationResult> VerifyTokenAsync(string token)
    {
        // JWT验证通常不需要异步操作，但为了API一致性提供此方法
        return await Task.FromResult(VerifyToken(token));
    }

    /// <summary>
    /// 生成JWT Token
    /// </summary>
    public string GenerateToken(IEnumerable<Claim> claims, int expirationMinutes = 60)
    {
        try
        {
            var key = Encoding.UTF8.GetBytes(_options.JwtSecret);
            var tokenHandler = new JwtSecurityTokenHandler();

            var claimsList = claims.ToList();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claimsList),
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                Issuer = _options.JwtIssuer,
                Audience = _options.JwtAudience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            _logger.LogInformation("✅ Token生成成功，用户: {UserId}, 过期时间: {Expiration}分钟",
                claimsList.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "unknown",
                expirationMinutes);

            return tokenString;
        }
        catch (Exception ex)
        {
            _logger.LogError("❌ 生成Token时发生异常: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 从Authorization Header中提取Token
    /// 支持: "Bearer {token}" 格式
    /// </summary>
    public string? ExtractToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return null;

        const string bearerScheme = "Bearer ";

        if (authorizationHeader.StartsWith(bearerScheme, StringComparison.OrdinalIgnoreCase))
        {
            return authorizationHeader.Substring(bearerScheme.Length).Trim();
        }

        return null;
    }
}

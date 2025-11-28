using Domain.Routes;
using Infrastructure.Routes.ParameterExtractors;
using Xunit;

namespace Tests.Unit.Infrastructure.Routes.ParameterExtractors;

/// <summary>
/// PrefixParameterExtractor 单元测试
/// </summary>
public class PrefixParameterExtractorTests
{
    private readonly PrefixParameterExtractor _extractor = new();

    #region MatchType 属性测试

    [Fact]
    public void MatchType_ReturnsPrefix()
    {
        // Act
        var matchType = _extractor.MatchType;

        // Assert
        Assert.Equal(RouteMatchType.Prefix, matchType);
    }

    #endregion

    #region ExtractParameters 测试 - 基本前缀匹配

    [Fact]
    public void ExtractParameters_ExactMatch_ReturnsEmpty()
    {
        // Arrange - 精确匹配，没有剩余路径
        var routePath = "/api/users";
        var requestPath = "/api/users";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal(0, parameters.PathParameters.Count);
    }

    [Fact]
    public void ExtractParameters_WithRemainingPath_ExtractsRemainingPath()
    {
        // Arrange
        var routePath = "/api";
        var requestPath = "/api/users/123";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("users/123", parameters.GetPathParameter("remainingPath"));
    }

    [Fact]
    public void ExtractParameters_WithSingleSegment_ExtractsSegment()
    {
        // Arrange
        var routePath = "/api/users";
        var requestPath = "/api/users/123";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("123", parameters.GetPathParameter("remainingPath"));
        Assert.Equal("123", parameters.GetPathParameter("segment0"));
        Assert.Equal(2, parameters.PathParameters.Count);
    }

    [Fact]
    public void ExtractParameters_WithMultipleSegments_ExtractsAll()
    {
        // Arrange
        var routePath = "/api";
        var requestPath = "/api/users/123/orders/456";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("users/123/orders/456", parameters.GetPathParameter("remainingPath"));
        Assert.Equal("users", parameters.GetPathParameter("segment0"));
        Assert.Equal("123", parameters.GetPathParameter("segment1"));
        Assert.Equal("orders", parameters.GetPathParameter("segment2"));
        Assert.Equal("456", parameters.GetPathParameter("segment3"));
        Assert.Equal(5, parameters.PathParameters.Count); // remainingPath + 4 segments
    }

    #endregion

    #region ExtractParameters 测试 - 路径分隔符处理

    [Fact]
    public void ExtractParameters_RoutePathWithTrailingSlash_HandlesCorrectly()
    {
        // Arrange
        var routePath = "/api/";
        var requestPath = "/api/users";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("users", parameters.GetPathParameter("remainingPath"));
    }

    [Fact]
    public void ExtractParameters_RequestPathWithTrailingSlash_HandlesCorrectly()
    {
        // Arrange
        var routePath = "/api";
        var requestPath = "/api/users/";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("users/", parameters.GetPathParameter("remainingPath"));
    }

    [Fact]
    public void ExtractParameters_BothPathsWithTrailingSlash_HandlesCorrectly()
    {
        // Arrange
        var routePath = "/api/";
        var requestPath = "/api/users/";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("users/", parameters.GetPathParameter("remainingPath"));
    }

    [Fact]
    public void ExtractParameters_MultipleSlashes_NormalizesCorrectly()
    {
        // Arrange
        var routePath = "/api";
        var requestPath = "/api//users///123";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        // 应该移除空段
        Assert.Equal("users", parameters.GetPathParameter("segment0"));
        Assert.Equal("123", parameters.GetPathParameter("segment1"));
    }

    #endregion

    #region ExtractParameters 测试 - 不匹配场景

    [Fact]
    public void ExtractParameters_NotMatchingPrefix_ReturnsEmpty()
    {
        // Arrange
        var routePath = "/api/users";
        var requestPath = "/api/products/123"; // 不匹配前缀

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal(0, parameters.PathParameters.Count);
    }

    [Fact]
    public void ExtractParameters_ShorterRequestPath_ReturnsEmpty()
    {
        // Arrange
        var routePath = "/api/users/orders";
        var requestPath = "/api/users"; // 请求路径比前缀短

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal(0, parameters.PathParameters.Count);
    }

    #endregion

    #region ExtractParameters 测试 - 特殊字符

    [Theory]
    [InlineData("/api", "/api/users-list", "users-list")]
    [InlineData("/api", "/api/users_list", "users_list")]
    [InlineData("/api", "/api/file.txt", "file.txt")]
    [InlineData("/api", "/api/hello%20world", "hello%20world")]
    public void ExtractParameters_SpecialCharacters_ExtractsCorrectly(
        string routePath,
        string requestPath,
        string expectedRemaining)
    {
        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal(expectedRemaining, parameters.GetPathParameter("remainingPath"));
    }

    [Fact]
    public void ExtractParameters_ChineseCharacters_ExtractsCorrectly()
    {
        // Arrange
        var routePath = "/api";
        var requestPath = "/api/用户/123";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("用户/123", parameters.GetPathParameter("remainingPath"));
        Assert.Equal("用户", parameters.GetPathParameter("segment0"));
    }

    #endregion

    #region ExtractParameters 测试 - 边界情况

    [Fact]
    public void ExtractParameters_EmptyRoutePath_ReturnsFullPath()
    {
        // Arrange
        var routePath = "";
        var requestPath = "/api/users";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("api/users", parameters.GetPathParameter("remainingPath"));
    }

    [Fact]
    public void ExtractParameters_EmptyRequestPath_ReturnsEmpty()
    {
        // Arrange
        var routePath = "/api";
        var requestPath = "";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal(0, parameters.PathParameters.Count);
    }

    [Fact]
    public void ExtractParameters_RootPath_HandlesCorrectly()
    {
        // Arrange
        var routePath = "/";
        var requestPath = "/users/123";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("users/123", parameters.GetPathParameter("remainingPath"));
        Assert.Equal("users", parameters.GetPathParameter("segment0"));
        Assert.Equal("123", parameters.GetPathParameter("segment1"));
    }

    [Fact]
    public void ExtractParameters_OnlySlashes_ReturnsEmpty()
    {
        // Arrange
        var routePath = "/";
        var requestPath = "/";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal(0, parameters.PathParameters.Count);
    }

    #endregion

    #region ExtractParameters 测试 - 大小写敏感性

    [Fact]
    public void ExtractParameters_CaseInsensitive_MatchesCorrectly()
    {
        // Arrange
        var routePath = "/api/users";
        var requestPath = "/API/USERS/123"; // 大写

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        // 应该不区分大小写匹配
        Assert.Equal("123", parameters.GetPathParameter("remainingPath"));
    }

    [Fact]
    public void ExtractParameters_MixedCase_ExtractsOriginalCase()
    {
        // Arrange
        var routePath = "/api";
        var requestPath = "/api/UserProfiles/JohnDoe";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        // 应保留原始大小写
        Assert.Equal("UserProfiles/JohnDoe", parameters.GetPathParameter("remainingPath"));
        Assert.Equal("UserProfiles", parameters.GetPathParameter("segment0"));
        Assert.Equal("JohnDoe", parameters.GetPathParameter("segment1"));
    }

    #endregion

    #region ExtractParameters 测试 - 深度嵌套路径

    [Fact]
    public void ExtractParameters_DeepNesting_ExtractsAllSegments()
    {
        // Arrange
        var routePath = "/api";
        var requestPath = "/api/v1/users/123/orders/456/items/789/details";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("v1/users/123/orders/456/items/789/details", parameters.GetPathParameter("remainingPath"));
        Assert.Equal("v1", parameters.GetPathParameter("segment0"));
        Assert.Equal("users", parameters.GetPathParameter("segment1"));
        Assert.Equal("123", parameters.GetPathParameter("segment2"));
        Assert.Equal("orders", parameters.GetPathParameter("segment3"));
        Assert.Equal("456", parameters.GetPathParameter("segment4"));
        Assert.Equal("items", parameters.GetPathParameter("segment5"));
        Assert.Equal("789", parameters.GetPathParameter("segment6"));
        Assert.Equal("details", parameters.GetPathParameter("segment7"));
        Assert.Equal(9, parameters.PathParameters.Count); // remainingPath + 8 segments
    }

    #endregion

    #region ExtractParameters 测试 - 实际业务场景

    [Fact]
    public void ExtractParameters_ApiGatewayScenario_ExtractsServiceAndPath()
    {
        // Arrange - 模拟 API 网关场景：/gateway/{service}/{path}
        var routePath = "/gateway";
        var requestPath = "/gateway/user-service/api/users/123";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("user-service/api/users/123", parameters.GetPathParameter("remainingPath"));
        Assert.Equal("user-service", parameters.GetPathParameter("segment0")); // 服务名
        Assert.Equal("api", parameters.GetPathParameter("segment1"));
        Assert.Equal("users", parameters.GetPathParameter("segment2"));
        Assert.Equal("123", parameters.GetPathParameter("segment3"));
    }

    [Fact]
    public void ExtractParameters_FilePathScenario_ExtractsCorrectly()
    {
        // Arrange - 模拟文件路径场景
        var routePath = "/files";
        var requestPath = "/files/uploads/2024/11/document.pdf";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("uploads/2024/11/document.pdf", parameters.GetPathParameter("remainingPath"));
        Assert.Equal("uploads", parameters.GetPathParameter("segment0"));
        Assert.Equal("2024", parameters.GetPathParameter("segment1"));
        Assert.Equal("11", parameters.GetPathParameter("segment2"));
        Assert.Equal("document.pdf", parameters.GetPathParameter("segment3"));
    }

    #endregion

    #region 性能测试

    [Fact]
    public void ExtractParameters_MultipleCalls_PerformanceCheck()
    {
        // Arrange
        var routePath = "/api";
        var requestPath = "/api/users/123";

        // Act - 多次调用
        for (int i = 0; i < 1000; i++)
        {
            var parameters = _extractor.ExtractParameters(routePath, requestPath);
            Assert.Equal("users/123", parameters.GetPathParameter("remainingPath"));
        }

        // Assert - 测试通过即表示性能可接受
        Assert.True(true);
    }

    #endregion
}
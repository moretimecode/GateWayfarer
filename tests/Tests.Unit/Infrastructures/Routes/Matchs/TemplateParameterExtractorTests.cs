using Domain.Routes;

namespace Tests.Unit.Infrastructure.Routes.ParameterExtractors;

/// <summary>
/// TemplateParameterExtractor 单元测试
/// </summary>
public class TemplateParameterExtractorTests
{
    private readonly TemplateParameterExtractor _extractor = new();

    #region MatchType 属性测试

    [Fact]
    public void MatchType_ReturnsTemplate()
    {
        // Act
        var matchType = _extractor.MatchType;

        // Assert
        Assert.Equal(RouteMatchType.Template, matchType);
    }

    #endregion

    #region ExtractParameters 测试 - 单个参数

    [Fact]
    public void ExtractParameters_SingleParameter_ExtractsCorrectly()
    {
        // Arrange
        var routePath = "/api/users/{id}";
        var requestPath = "/api/users/123";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("123", parameters.GetPathParameter("id"));
    }

    [Theory]
    [InlineData("/api/users/{id}", "/api/users/123", "id", "123")]
    [InlineData("/api/users/{userId}", "/api/users/abc", "userId", "abc")]
    [InlineData("/api/{resource}", "/api/products", "resource", "products")]
    [InlineData("/v1/{version}/users", "/v1/2.0/users", "version", "2.0")]
    public void ExtractParameters_VariousPatterns_ExtractsCorrectly(
        string routePath,
        string requestPath,
        string paramName,
        string expectedValue)
    {
        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal(expectedValue, parameters.GetPathParameter(paramName));
    }

    #endregion

    #region ExtractParameters 测试 - 多个参数

    [Fact]
    public void ExtractParameters_MultipleParameters_ExtractsAll()
    {
        // Arrange
        var routePath = "/api/users/{userId}/orders/{orderId}";
        var requestPath = "/api/users/123/orders/456";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("123", parameters.GetPathParameter("userId"));
        Assert.Equal("456", parameters.GetPathParameter("orderId"));
    }

    [Fact]
    public void ExtractParameters_ThreeParameters_ExtractsAll()
    {
        // Arrange
        var routePath = "/api/{version}/users/{userId}/posts/{postId}";
        var requestPath = "/api/v2/users/789/posts/101";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("v2", parameters.GetPathParameter("version"));
        Assert.Equal("789", parameters.GetPathParameter("userId"));
        Assert.Equal("101", parameters.GetPathParameter("postId"));
    }

    #endregion

    #region ExtractParameters 测试 - 路径不匹配

    [Fact]
    public void ExtractParameters_SegmentCountMismatch_ReturnsEmpty()
    {
        // Arrange
        var routePath = "/api/users/{id}";
        var requestPath = "/api/users"; // 缺少参数段

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Null(parameters.GetPathParameter("id"));
    }

    [Fact]
    public void ExtractParameters_TooManySegments_ReturnsEmpty()
    {
        // Arrange
        var routePath = "/api/users/{id}";
        var requestPath = "/api/users/123/extra"; // 多余段

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Null(parameters.GetPathParameter("id"));
    }

    #endregion

    #region ExtractParameters 测试 - 特殊字符

    [Theory]
    [InlineData("/api/users/{id}", "/api/users/user-123", "id", "user-123")]
    [InlineData("/api/users/{id}", "/api/users/user_456", "id", "user_456")]
    [InlineData("/api/files/{filename}", "/api/files/test.pdf", "filename", "test.pdf")]
    [InlineData("/api/search/{query}", "/api/search/hello%20world", "query", "hello%20world")]
    public void ExtractParameters_SpecialCharacters_ExtractsCorrectly(
        string routePath,
        string requestPath,
        string paramName,
        string expectedValue)
    {
        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal(expectedValue, parameters.GetPathParameter(paramName));
    }

    #endregion

    #region ExtractParameters 测试 - 边界情况

    [Fact]
    public void ExtractParameters_EmptyRoutePath_ReturnsEmpty()
    {
        // Arrange
        var routePath = "";
        var requestPath = "/api/users";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.NotNull(parameters);
    }

    [Fact]
    public void ExtractParameters_EmptyRequestPath_ReturnsEmpty()
    {
        // Arrange
        var routePath = "/api/users/{id}";
        var requestPath = "";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Null(parameters.GetPathParameter("id"));
    }

    [Fact]
    public void ExtractParameters_RootPath_HandlesCorrectly()
    {
        // Arrange
        var routePath = "/{resource}";
        var requestPath = "/users";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("users", parameters.GetPathParameter("resource"));
    }

    [Fact]
    public void ExtractParameters_TrailingSlash_HandlesCorrectly()
    {
        // Arrange
        var routePath = "/api/users/{id}/";
        var requestPath = "/api/users/123/";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("123", parameters.GetPathParameter("id"));
    }

    #endregion

    #region ExtractParameters 测试 - 混合场景

    [Fact]
    public void ExtractParameters_MixedStaticAndDynamic_ExtractsOnlyDynamic()
    {
        // Arrange
        var routePath = "/api/users/{userId}/orders/{orderId}/details";
        var requestPath = "/api/users/123/orders/456/details";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("123", parameters.GetPathParameter("userId"));
        Assert.Equal("456", parameters.GetPathParameter("orderId"));
        Assert.Equal(2, parameters.PathParameters.Count); // 只有2个参数
    }

    [Fact]
    public void ExtractParameters_AllDynamicSegments_ExtractsAll()
    {
        // Arrange
        var routePath = "/{a}/{b}/{c}";
        var requestPath = "/1/2/3";

        // Act
        var parameters = _extractor.ExtractParameters(routePath, requestPath);

        // Assert
        Assert.Equal("1", parameters.GetPathParameter("a"));
        Assert.Equal("2", parameters.GetPathParameter("b"));
        Assert.Equal("3", parameters.GetPathParameter("c"));
    }

    #endregion
}
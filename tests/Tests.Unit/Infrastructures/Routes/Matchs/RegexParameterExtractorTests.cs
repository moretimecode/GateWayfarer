// filepath: [RegexParameterExtractorTests.cs](http://_vscodecontentref_/1)
using Domain.Routes;
using Infrastructure.Routes.ParameterExtractors;
using Xunit;

namespace Tests.Unit.Infrastructure.Routes.ParameterExtractors;

public class RegexParameterExtractorTests
{
    private readonly RegexParameterExtractor _extractor = new();

    #region MatchType 属性测试

    [Fact]
    public void MatchType_ReturnsRegex()
    {
        // Act
        var matchType = _extractor.MatchType;

        // Assert
        Assert.Equal(RouteMatchType.Regex, matchType);
    }

    #endregion

    #region ExtractParameters 测试 - 命名捕获组

    [Fact]
    public void ExtractParameters_SingleNamedGroup_ExtractsCorrectly()
    {
        // Arrange
        var pattern = @"^/api/users/(?<id>\d+)$";
        var requestPath = "/api/users/123";

        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        Assert.Equal("123", parameters.GetPathParameter("id"));
        // 命名组也会作为 group1 存在
        Assert.Equal("123", parameters.GetPathParameter("group1"));
        Assert.Equal(2, parameters.PathParameters.Count); // id 和 group1
    }

    [Fact]
    public void ExtractParameters_MultipleNamedGroups_ExtractsAll()
    {
        // Arrange
        var pattern = @"^/api/users/(?<userId>\d+)/orders/(?<orderId>\d+)$";
        var requestPath = "/api/users/456/orders/789";

        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        Assert.Equal("456", parameters.GetPathParameter("userId"));
        Assert.Equal("789", parameters.GetPathParameter("orderId"));
        Assert.Equal("456", parameters.GetPathParameter("group1"));
        Assert.Equal("789", parameters.GetPathParameter("group2"));
        Assert.Equal(4, parameters.PathParameters.Count); // userId, orderId, group1, group2
    }

    [Theory]
    [InlineData(@"^/api/users/(?<id>\d+)$", "/api/users/123", "id", "123")]
    [InlineData(@"^/api/users/(?<name>\w+)$", "/api/users/john", "name", "john")]
    [InlineData(@"^/api/v(?<version>\d+)/users$", "/api/v2/users", "version", "2")]
    [InlineData(@"^/files/(?<filename>[^/]+)$", "/files/test.pdf", "filename", "test.pdf")]
    public void ExtractParameters_VariousPatterns_ExtractsCorrectly(
        string pattern,
        string requestPath,
        string paramName,
        string expectedValue)
    {
        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        Assert.Equal(expectedValue, parameters.GetPathParameter(paramName));
    }

    #endregion

    #region ExtractParameters 测试 - 数字捕获组

    [Fact]
    public void ExtractParameters_NumberedGroups_ExtractsWithBothKeys()
    {
        // Arrange - 使用数字编号的捕获组（非命名组）
        var pattern = @"^/api/users/(\d+)/orders/(\d+)$";
        var requestPath = "/api/users/123/orders/456";

        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        // 提取为 "1", "2" 和 "group1", "group2"
        Assert.Equal(4, parameters.PathParameters.Count);
        Assert.Equal("123", parameters.GetPathParameter("1"));
        Assert.Equal("456", parameters.GetPathParameter("2"));
        Assert.Equal("123", parameters.GetPathParameter("group1"));
        Assert.Equal("456", parameters.GetPathParameter("group2"));
    }

    [Fact]
    public void ExtractParameters_MixedNumberedAndNamedGroups_ExtractsAll()
    {
        // Arrange - 混合数字组和命名组
        var pattern = @"^/api/(\w+)/users/(?<userId>\d+)$";
        var requestPath = "/api/v1/users/789";

        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        // 第一个是数字组，第二个是命名组
        Assert.Equal(4, parameters.PathParameters.Count);
        Assert.Equal("v1", parameters.GetPathParameter("1"));
        Assert.Equal("789", parameters.GetPathParameter("userId"));
        Assert.Equal("v1", parameters.GetPathParameter("group1"));
        Assert.Equal("789", parameters.GetPathParameter("group2"));
    }

    #endregion

    #region ExtractParameters 测试 - 不匹配场景

    [Fact]
    public void ExtractParameters_NoMatch_ReturnsEmpty()
    {
        // Arrange
        var pattern = @"^/api/users/(?<id>\d+)$";
        var requestPath = "/api/products/123"; // 不匹配

        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        Assert.Equal(0, parameters.PathParameters.Count);
    }

    [Fact]
    public void ExtractParameters_PatternNotMatch_ReturnsEmpty()
    {
        // Arrange
        var pattern = @"^/api/users/(?<id>\d+)$"; // 要求数字
        var requestPath = "/api/users/abc"; // 提供字母

        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        Assert.Equal(0, parameters.PathParameters.Count);
    }

    #endregion

    #region ExtractParameters 测试 - 复杂正则表达式

    [Fact]
    public void ExtractParameters_ComplexPattern_ExtractsCorrectly()
    {
        // Arrange
        var pattern = @"^/api/(?<version>v\d+)/(?<resource>\w+)/(?<id>[a-f0-9-]+)$";
        var requestPath = "/api/v2/users/abc123-def456";

        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        Assert.Equal("v2", parameters.GetPathParameter("version"));
        Assert.Equal("users", parameters.GetPathParameter("resource"));
        Assert.Equal("abc123-def456", parameters.GetPathParameter("id"));
    }

    [Fact]
    public void ExtractParameters_OptionalGroups_HandlesCorrectly()
    {
        // Arrange - 可选的捕获组
        var pattern = @"^/api/users/(?<id>\d+)(/orders/(?<orderId>\d+))?$";

        // Act & Assert - 匹配完整路径
        var params1 = _extractor.ExtractParameters(pattern, "/api/users/123/orders/456");
        Assert.Equal("123", params1.GetPathParameter("id"));
        Assert.Equal("456", params1.GetPathParameter("orderId"));

        // Act & Assert - 匹配部分路径（orderId 未捕获）
        var params2 = _extractor.ExtractParameters(pattern, "/api/users/123");
        Assert.Equal("123", params2.GetPathParameter("id"));
        // orderId 组存在但未成功匹配，不会被添加
        Assert.Null(params2.GetPathParameter("orderId"));
    }

    #endregion

    #region ExtractParameters 测试 - 特殊字符

    [Theory]
    [InlineData(@"^/api/files/(?<filename>.+\..+)$", "/api/files/test.pdf", "filename", "test.pdf")]
    [InlineData(@"^/api/files/(?<path>.+)$", "/api/files/folder/subfolder/file.txt", "path", "folder/subfolder/file.txt")]
    [InlineData(@"^/api/search/(?<query>.+)$", "/api/search/hello%20world", "query", "hello%20world")]
    public void ExtractParameters_SpecialCharacters_ExtractsCorrectly(
        string pattern,
        string requestPath,
        string paramName,
        string expectedValue)
    {
        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        Assert.Equal(expectedValue, parameters.GetPathParameter(paramName));
    }

    #endregion

    #region ExtractParameters 测试 - 边界情况

    [Fact]
    public void ExtractParameters_EmptyPattern_ReturnsEmpty()
    {
        // Arrange
        var pattern = "";
        var requestPath = "/api/users";

        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        Assert.Equal(0, parameters.PathParameters.Count);
    }

    [Fact]
    public void ExtractParameters_EmptyRequestPath_ReturnsEmpty()
    {
        // Arrange
        var pattern = @"^/api/users/(?<id>\d+)$";
        var requestPath = "";

        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        Assert.Equal(0, parameters.PathParameters.Count);
    }

    [Fact]
    public void ExtractParameters_InvalidRegex_ReturnsErrorParameter()
    {
        // Arrange - 无效的正则表达式
        var pattern = @"^/api/users/(?<id>\d+"; // 缺少右括号
        var requestPath = "/api/users/123";

        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        // catch 块会添加 error 参数
        Assert.Equal(1, parameters.PathParameters.Count);
        Assert.NotNull(parameters.GetPathParameter("error"));
    }

    #endregion

    #region ExtractParameters 测试 - 大小写敏感性

    [Fact]
    public void ExtractParameters_CaseInsensitive_MatchesCorrectly()
    {
        // Arrange
        var pattern = @"^/api/users/(?<id>\d+)$";
        var requestPath = "/API/USERS/123"; // 大写路径

        // Act
        var parameters = _extractor.ExtractParameters(pattern, requestPath);

        // Assert
        // 使用了 RegexOptions.IgnoreCase，应该匹配
        Assert.Equal("123", parameters.GetPathParameter("id"));
    }

    #endregion
}
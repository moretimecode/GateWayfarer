/// <summary>
/// xUnit Collection Fixture 定义
/// 将 GatewayTestFixture 与集合名称关联
/// </summary>
[CollectionDefinition("Gateway Integration Tests")]
public class GatewayTestCollection : ICollectionFixture<GatewayTestFixture>
{
    // 这个类没有代码
    // 它只是定义了 Collection 和 Fixture 的关联关系
}

[CollectionDefinition("Gateway Integration")]
public class GatewayIntegrationCollection : ICollectionFixture<GatewayTestFixture>
{
    // 这个类没有代码
    // 它只是定义了 Collection 和 Fixture 的关联关系
}

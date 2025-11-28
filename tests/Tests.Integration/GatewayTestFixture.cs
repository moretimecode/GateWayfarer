using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Tests.Integration;

/// <summary>
/// 网关集成测试夹具（简化版）
/// 使用 EF Core SQLite 内存数据库，避免 WebApplicationFactory 的复杂性
/// </summary>
public class GatewayTestFixture : IAsyncLifetime
{
    private SqliteConnection _connection;
    private TestApiGatewayDbContext _dbContext;

    public async Task InitializeAsync()
    {
        try
        {
            // 创建持久化连接（保持 :memory: 数据库存活）
            _connection = new SqliteConnection("Data Source=:memory:");
            await _connection.OpenAsync();

            // 创建 SQLite 内存数据库上下文
            var options = new DbContextOptionsBuilder<TestApiGatewayDbContext>()
                .UseSqlite(_connection)
                .EnableSensitiveDataLogging()
                .Options;

            _dbContext = new TestApiGatewayDbContext(options);

            // 创建数据库表
            await _dbContext.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("集成测试初始化失败", ex);
        }
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            try
            {
                await _dbContext.DisposeAsync();
            }
            catch { }
        }

        if (_connection != null)
        {
            try
            {
                await _connection.DisposeAsync();
            }
            catch { }
        }
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public async Task ClearAllDataAsync()
    {
        _dbContext.Routes.RemoveRange(_dbContext.Routes);
        _dbContext.Services.RemoveRange(_dbContext.Services);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// 获取 DbContext
    /// </summary>
    public TestApiGatewayDbContext GetDbContext()
    {
        return _dbContext;
    }
}

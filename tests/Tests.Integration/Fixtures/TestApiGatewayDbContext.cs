using Domain.Routes;
using Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Tests.Integration;

/// <summary>
/// 测试用的简化 DbContext - 避免复杂的 JSON 配置
/// </summary>
public class TestApiGatewayDbContext : DbContext
{
    public TestApiGatewayDbContext(DbContextOptions<TestApiGatewayDbContext> options) : base(options)
    {
    }

    public DbSet<Route> Routes { get; set; } = null!;
    public DbSet<Service> Services { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 简化的 Route 配置 - 只配置基本属性
        modelBuilder.Entity<Route>(entity =>
        {
            entity.ToTable("Routes");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Path)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.ServiceCode)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.HttpMethods);
            entity.Property(e => e.Priority);
            entity.Property(e => e.IsEnabled);
            entity.Property(e => e.MatchType);
            entity.Property(e => e.LoadBalanceType);

            // 忽略复杂的配置对象，以避免 JSON 问题
            entity.Ignore(e => e.BatchingConfig);
            entity.Ignore(e => e.ForwardingConfig);
        });

        // 简化的 Service 配置 - 只配置基本属性
        modelBuilder.Entity<Service>(entity =>
        {
            entity.ToTable("Services");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("IX_Services_Code");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            // 忽略复杂的配置对象
            entity.Ignore(e => e.HealthCheck);
            entity.Ignore(e => e.CircuitBreaker);
            entity.Ignore(e => e.RetryPolicy);
            entity.Ignore(e => e.FallbackStrategy);
            entity.Ignore(e => e.TimeoutConfig);
            entity.Ignore(e => e.ProtocolConfig);
            entity.Ignore(e => e.Destinations);
        });
    }
}

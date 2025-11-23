using System.Text.Json;
using Domain.Routes;
using Domain.Routes.Configuration;
using Infrastructure.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DB;


public class RouteConfiguration : IEntityTypeConfiguration<Domain.Routes.Route>
{
    public void Configure(EntityTypeBuilder<Domain.Routes.Route> builder)
    {
        builder.ToTable("Routes");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasDatabaseName("IX_Routes_Name");

        builder.Property(r => r.Path)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.HttpMethods)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.Priority)
            .IsRequired();

        builder.Property(r => r.IsEnabled)
            .IsRequired();

        builder.Property(r => r.MatchType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.LoadBalanceType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.ServiceCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(r => r.ServiceCode)
            .HasDatabaseName("IX_Routes_ServiceCode");

        builder.HasIndex(r => new { r.IsEnabled, r.Priority })
            .HasDatabaseName("IX_Routes_Enabled_Priority");

        // 新增：请求批处理配置 - JSON存储
        builder.Property(r => r.BatchingConfig)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<RequestBatchingConfig>(v, JsonSerializerOptions.Default)!)
            .HasColumnType("JSON")
            .HasColumnName("BatchingConfig");

        // 新增：请求转发配置 - JSON存储
        builder.Property(r => r.ForwardingConfig)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<RequestForwardingConfig>(v, JsonSerializerOptions.Default)!)
            .HasColumnType("JSON")
            .HasColumnName("ForwardingConfig");
    }
}
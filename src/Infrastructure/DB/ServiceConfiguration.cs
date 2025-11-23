using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Services;
using Domain.Services.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DB
{
  public class ServiceConfiguration : IEntityTypeConfiguration<Service>
    {
        public void Configure(EntityTypeBuilder<Service> builder)
        {
            builder.ToTable("Services");

            builder.HasKey(s => s.Id);

            builder.Property(s => s.Code)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(s => s.Code)
                .IsUnique()
                .HasDatabaseName("IX_Services_Code");

            builder.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(s => s.HealthCheck)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<HealthCheckConfig>(v, JsonSerializerOptions.Default)!)
                .HasColumnType("JSON")
                .HasColumnName("HealthCheckConfig");

            builder.Property(s => s.Destinations)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<List<Destination>>(v, JsonSerializerOptions.Default) ?? new List<Destination>())
                .HasColumnType("JSON")
                .HasColumnName("Destinations");

            // 新增：熔断器配置 - JSON存储
            builder.Property(s => s.CircuitBreaker)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<CircuitBreakerConfig>(v, JsonSerializerOptions.Default)!)
                .HasColumnType("JSON")
                .HasColumnName("CircuitBreakerConfig");

            // 新增：重试策略配置 - JSON存储
            builder.Property(s => s.RetryPolicy)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<RetryPolicy>(v, JsonSerializerOptions.Default)!)
                .HasColumnType("JSON")
                .HasColumnName("RetryPolicyConfig");

            // 新增：降级策略配置 - JSON存储
            builder.Property(s => s.FallbackStrategy)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<FallbackStrategy>(v, JsonSerializerOptions.Default)!)
                .HasColumnType("JSON")
                .HasColumnName("FallbackStrategyConfig");

            // 新增：超时配置 - JSON存储
            builder.Property(s => s.TimeoutConfig)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<TimeoutConfig>(v, JsonSerializerOptions.Default)!)
                .HasColumnType("JSON")
                .HasColumnName("TimeoutConfig");

            builder.Property(s => s.TimeoutConfig)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<TimeoutConfig>(v, JsonSerializerOptions.Default)!)
                .HasColumnType("JSON")
                .HasColumnName("TimeoutConfig");
            
        builder.OwnsOne(s => s.ProtocolConfig, pc =>
        {
            pc.ToJson("ProtocolConfig"); 
        });
        }
    }
}
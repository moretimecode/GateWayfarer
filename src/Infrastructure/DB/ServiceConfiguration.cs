using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Services;
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
        }
    }
}
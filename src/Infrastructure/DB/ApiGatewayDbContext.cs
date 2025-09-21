using Domain.Routes;
using Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DB;

public class ApiGatewayDbContext(DbContextOptions<ApiGatewayDbContext> options) : DbContext(options)
{

    public DbSet<Route> Routes { get; set; } = null!;
    public DbSet<Service> Services { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new RouteConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceConfiguration());
    }
}
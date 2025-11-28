using Api;
using Infrastructure.DB;
using Infrastructure.Routes;
using Microsoft.EntityFrameworkCore;
using NLog.Extensions.Logging;
using Route = Domain.Routes.Route;
using Domain.Routes.Configuration;
using Domain.Services;
using Domain.Services.Configuration;
using Infrastructure.Services;
using Application.Configuration;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.ConfigureKestrel(kestrel =>
            {
                kestrel.ListenAnyIP(80);
            });
            webBuilder.UseStartup<Startup>();
        }).ConfigureAppConfiguration((ctx, builder) =>
        {
            var connectionString = builder.Build().GetConnectionString("DBConnection");

            if (!string.IsNullOrEmpty(connectionString))
            {
                builder.AddEFConfiguration<ApiGatewayDbContext, Route, RouteConfigItem>(options =>
                   options.WithDbContextFactory(() => CreateDbContext(connectionString))
                          .WithQuery(db => db.Routes
                              .Where(r => r.IsEnabled)
                              .OrderByDescending(r => r.Priority))
                          .WithAutoMapper(typeof(RouteConfigurationProfile).Assembly)
                          .WithSection("ApiGateway:Routes")
                          .WithRefreshInterval(TimeSpan.FromSeconds(10))
                );

                builder.AddEFConfiguration<ApiGatewayDbContext, Service, ServiceConfigItem>(options =>
                   options.WithDbContextFactory(() => CreateDbContext(connectionString))
                          .WithQuery(db => db.Services)
                          .WithAutoMapper(typeof(ServiceConfigurationProfile).Assembly)
                          .WithSection("ApiGateway:Services")
                          .WithRefreshInterval(TimeSpan.FromSeconds(10))
                );
            }
        })
        .ConfigureServices((context, services) =>
        {
            services.Configure<ApiGatewayOptions>(context.Configuration.GetSection(ApiGatewayOptions.SectionName));
            services.Configure<Domain.Routes.Configuration.RouteOptions>(context.Configuration.GetSection(Domain.Routes.Configuration.RouteOptions.SectionName));
            services.Configure<ServiceOptions>(context.Configuration.GetSection(ServiceOptions.SectionName));
        })
        .ConfigureLogging((context, logging) =>
        {
            logging.AddNLog("Configs/nlog.config");
            logging.AddSimpleConsole();
        });

    private static ApiGatewayDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApiGatewayDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;
        return new ApiGatewayDbContext(options);
    }
}
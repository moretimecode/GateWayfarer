using Infrastructure.DB;
using Infrastructure.Routes;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var assemblies = AssembiesEx.GetSolutionAssemblies().ToArray();
            services.AddMemoryCache();
            services.AddAutoMapper(assemblies);
            services.AddEfCore<ApiGatewayDbContext>([.. assemblies], options =>
            {
                var conn = configuration.GetConnectionString("DBConnection");
                options
                 .UseMySql(conn, ServerVersion.AutoDetect(conn))
                 .EnableSensitiveDataLogging();
            });

            services.AddRabbitMQEventBus(b =>
            {
                b.UseConnectionString(configuration.GetConnectionString("RawRabbit"));

                b.ScanFrom(assemblies);
            });

            services.AddRouteServices();
            services.AddServiceService(configuration);
            return services;
        }
    }
}

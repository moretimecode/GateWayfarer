using Infrastructure.DB;
using Infrastructure.EndPoints;
using Infrastructure.Protocol;
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
            services.AddEndpointServices();
            services.AddProtocolSupport(options =>
            {
                options.DefaultTimeout = TimeSpan.FromSeconds(30);
                options.EnableProtocolNegotiation = true;
                options.LogProtocolDowngrade = true;
            });
                    services.AddStreamingForwarding(options =>
        {
            options.BufferSize = 81920; // 80KB
            options.StreamTimeout = TimeSpan.FromMinutes(30);
            options.StreamingThreshold = 10 * 1024 * 1024; // 10MB
            options.MaxConcurrentStreams = 100;
            options.EnableStrictHeaderFiltering = true;
            options.AddProxyHeaders = true;
            options.ValidateContentLength = true;
            options.EnableRangeSupport = true;
        });

            return services;
        }
    }
}

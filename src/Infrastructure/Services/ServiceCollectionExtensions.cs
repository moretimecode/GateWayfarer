using Domain.Services.Configuration;
using Domain.Services.HealthChecking;
using Domain.Services.HealthChecking.Services;
using Domain.Services.Services;
using Infrastructure.Services.HealthPolicyCalculators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Infrastructure.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServiceService(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ServiceOptions>(configuration.GetSection(ServiceOptions.SectionName));

            services.Configure<HealthCheckOptions>(
                configuration.GetSection(HealthCheckOptions.SectionName)
            );

            services.ConfigureAll<HttpClientFactoryOptions>(options =>
            {
                options.HttpClientActions.Add(client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "ApiGateway-HealthChecker/1.0");
                    client.DefaultRequestHeaders.Add("X-Health-Check", "true");
                });

                options.HttpMessageHandlerBuilderActions.Add(builder =>
                {
                    if (builder.PrimaryHandler is HttpClientHandler handler)
                    {
                        handler.MaxConnectionsPerServer = 10;
                    }
                    builder.PrimaryHandler = new SocketsHttpHandler
                    {
                        MaxConnectionsPerServer = 10,
                        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                        EnableMultipleHttp2Connections = true
                    };
                });
            });

            services.AddSingleton<IHealthMetricsStorage>(provider =>
            {
                var destinationCodes = Array.Empty<string>();
                var bufferSize = 100;
                return new MemoryHealthMetricsStorage(destinationCodes, bufferSize);
            });

            services.AddSingleton<IHealthPolicyCalculator, StopOnFirstFailureCalculator>();
            services.AddSingleton<IHealthPolicyCalculator, ConsecutiveFailuresCalculator>();
            services.AddSingleton<IHealthPolicyCalculator, ContinueOnFailureCalculator>();

            services.AddSingleton<IHealthPolicyCalculatorFactory, HealthPolicyCalculatorFactory>();

            services.AddSingleton<IHealthCalculationService, HealthCalculationService>();

            services.AddSingleton<IHealthMetricsCollector, HealthMetricsCollector>();

            services.AddHostedService<HealthMetricsCollector>(provider =>
                (HealthMetricsCollector)provider.GetRequiredService<IHealthMetricsCollector>()
            );
            return services;
        }
    }
}
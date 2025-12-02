using Api.Middleware;
using Api.Swaggers;
using Application;
using Droplet.AppConfiguration;
using Droplet.AspNetCore.Filters;
using Infrastructure;
using Infrastructure.Endpoints;
using Infrastructure.OpenTelemetry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Shared;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json;

namespace Api
{
    public class Startup(IConfiguration configuration, IWebHostEnvironment hostingEnvironment)
    {
        public IConfiguration Configuration { get; } = configuration;
        public IWebHostEnvironment HostingEnvironment { get; } = hostingEnvironment;
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationLayer(Configuration);
            services.AddInfrastructure(Configuration);
            services.AddAppConfiguration<MyAppConfiguration>(Configuration);
            AddCommonAspNetCode(services);
        }

        public IServiceCollection AddCommonAspNetCode(IServiceCollection services)
        {
            var assemblies = AssembiesEx.GetSolutionAssemblies().ToArray();
            services.AddAutoMapper(assemblies);
            services.AddControllers().ConfigureApiBehaviorOptions(options =>
            {
            }).AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.WriteIndented = true;
            }).AddMvcOptions((MvcOptions mvcOptions) =>
            {
                // mvcOptions.Filters.Add<AccessLogActionFilter>(1);
                // mvcOptions.Filters.Add<UowActionActionFilter>(3);
            });
            services.AddRouting(delegate (RouteOptions options)
            {
                options.LowercaseUrls = true;
            });
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("CA", new OpenApiInfo { Title = "CA", Version = "v1" });
                options.SwaggerDoc("background", new OpenApiInfo { Title = "BackGround", Version = "v1" });
            });
            services.PostConfigure(delegate (SwaggerGenOptions options)
            {
                options.SwaggerGeneratorOptions.DescribeAllParametersInCamelCase = true;
                OpenApiSecurityRequirement securityRequirement = new()
            {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new List<string>()
            } };
                options.AddSecurityRequirement(securityRequirement);
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey
                });
                options.OperationFilter<SwaggerHeaderParameter>([]);
                options.SchemaFilter<EnumSchemaFilter>([]);
                for (int i = 0; i < assemblies.Length; i++)
                {
                    string text = assemblies[i].Location.Replace(".dll", ".xml");
                    if (File.Exists(text))
                    {
                        options.IncludeXmlComments(text);
                    }
                }
            });


            return services;
        }



        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // 添加 OpenTelemetry 中间件（在其他中间件之前）
            app.UseOpenTelemetryMiddleware();

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/ca/swagger.json", "getWay CA Api");
                options.SwaggerEndpoint("/swagger/background/swagger.json", "getWay  BackGround Api");
            });

            // 暴露 Prometheus 指标端点
            app.UseOpenTelemetryPrometheusScrapingEndpoint();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                var dynamicEndpointManager = endpoints.ServiceProvider.GetRequiredService<DynamicEndpointManager>();
                endpoints.DataSources.Add(dynamicEndpointManager);

                // 注册静态路由
                var endpointRegistrar = endpoints.ServiceProvider.GetRequiredService<IStartupEndpointRegistrar>();
                endpointRegistrar.RegisterAllEndpoints(endpoints);

                // 订阅配置变更事件
                var eventPublisher = endpoints.ServiceProvider.GetRequiredService<RouteConfigurationEventPublisher>();
                var hotUpdateCoordinator = endpoints.ServiceProvider.GetRequiredService<IHotUpdateCoordinator>();
                var logger = endpoints.ServiceProvider.GetRequiredService<ILogger<Startup>>();

                eventPublisher.ConfigurationChanged += async (sender, args) =>
                {
                    logger.LogInformation("🚀 接收到路由配置变更事件，开始热更新");

                    var result = await hotUpdateCoordinator.ApplyChangesAsync(args.Changes);

                    if (result.Success)
                    {
                        logger.LogInformation("✅ 热更新成功完成，耗时: {Duration}ms，影响路由: {Count}个",
                            result.Duration.TotalMilliseconds, result.AffectedRoutesCount);
                    }
                    else
                    {
                        logger.LogError("❌ 热更新失败: {Message}", result.Message);
                        foreach (var error in result.Errors)
                        {
                            logger.LogError("  - {Error}", error);
                        }
                    }
                };

                logger.LogInformation("📡 路由配置监听器已就绪，动态路由系统已启动");

                endpoints.MapControllers();
            });

            MyAppConfigurationManager.Initialize(app.ApplicationServices.GetRequiredService<IAppConfigurationManager<MyAppConfiguration>>());
        }
    }
}
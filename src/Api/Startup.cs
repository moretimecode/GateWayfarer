using Api.Swaggers;
using Application;
using Droplet.AppConfiguration;
using Droplet.AspNetCore.Filters;
using Infrastructure;
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
            services.AddHttpClient();
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
                mvcOptions.Filters.Add<AccessLogActionFilter>(1);
                mvcOptions.Filters.Add<UowActionActionFilter>(3);
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
            // Web API
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/ca/swagger.json", "getWay CA Api");
                options.SwaggerEndpoint("/swagger/background/swagger.json", "getWay  BackGround Api");
            });
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            MyAppConfigurationManager.Initialize(app.ApplicationServices.GetRequiredService<IAppConfigurationManager<MyAppConfiguration>>());
        }
    }
}
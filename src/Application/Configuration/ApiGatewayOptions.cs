using Domain.Routes.Configuration;
using Domain.Services.Configuration;

namespace Application.Configuration
{
     /// <summary>
    /// API网关应用配置选项
    /// </summary>
    public class ApiGatewayOptions
    {
        public const string SectionName = "ApiGateway";
        
        /// <summary>
        /// 路由配置列表
        /// </summary>
        public List<RouteConfigItem> Routes { get; set; } = new();
        
        /// <summary>
        /// 服务配置列表
        /// </summary>
        public List<ServiceConfigItem> Services { get; set; } = new();
    }
}
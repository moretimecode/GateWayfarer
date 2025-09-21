using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Services.Configuration
{
    public class ServiceOptions
    {
        public const string SectionName = "ApiGateway:Services";
        public List<ServiceConfigItem> Services { get; set; } = [];
    }
    
    public class ServiceConfigItem
    {
        /// <summary>
        /// 服务业务代码
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 服务名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 健康检查配置
        /// </summary>
        public HealthCheckConfigItem HealthCheck { get; set; } = new();

        /// <summary>
        /// 目标实例列表
        /// </summary>
        public List<DestinationConfigItem> Destinations { get; set; } = new();
    }
}